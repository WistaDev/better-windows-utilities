using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BWU.Utilities;

public sealed record ProcessResult(int ExitCode, bool Started, string? Error);

public static class ProcessRunner
{
    /// <summary>Console code page of the system (what cmd-style tools such as compact.exe and DISM write).</summary>
    public static Encoding OemEncoding
    {
        get
        {
            try { return Encoding.GetEncoding((int)NativeMethods.GetOEMCP()); }
            catch { return Encoding.UTF8; }
        }
    }

    /// <summary>
    /// Runs a program without a window and streams every output line to <paramref name="onLine"/>
    /// (called from a worker thread; lines keep their leading spaces so tables can be parsed).
    /// Cancelling the token terminates the process.
    /// </summary>
    public static async Task<ProcessResult> RunAsync(string fileName, string arguments,
        Action<string>? onLine = null, Encoding? encoding = null, CancellationToken ct = default)
    {
        var enc = encoding ?? OemEncoding;
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = enc,
            StandardErrorEncoding = enc
        };

        using var process = new Process { StartInfo = psi };
        try { process.Start(); }
        catch (Exception ex) { return new ProcessResult(-1, false, ex.Message); }

        using var registration = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(true); } catch { /* already gone */ }
        });

        var pumps = Task.WhenAll(Pump(process.StandardOutput, onLine), Pump(process.StandardError, onLine));
        await pumps.ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        return new ProcessResult(process.ExitCode, true, ct.IsCancellationRequested ? "Cancelled" : null);
    }

    public static Task<ProcessResult> RunPowerShellAsync(string script, Action<string>? onLine = null, CancellationToken ct = default)
    {
        var full = "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; $ProgressPreference='SilentlyContinue'; " + script;
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
        // Bypass applies to this process only; it is needed to import signed modules on systems set to "Restricted".
        return RunAsync("powershell.exe",
            $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            onLine, Encoding.UTF8, ct);
    }

    private static async Task Pump(StreamReader reader, Action<string>? onLine)
    {
        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
        {
            line = line.Replace("\0", "").TrimEnd();
            if (onLine != null && line.Length > 0) onLine(line);
        }
    }

    private static readonly Regex PercentRegex = new(@"(\d{1,3}(?:[.,]\d+)?)\s*%", RegexOptions.Compiled);

    public static double? TryParsePercent(string line)
    {
        var m = PercentRegex.Match(line);
        if (!m.Success) return null;
        return double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
               && v is >= 0 and <= 100 ? v : null;
    }

    /// <summary>True for spinner frames and progress-bar redraws that would only clutter a log.</summary>
    public static bool IsNoise(string line)
    {
        var t = line.Trim();
        if (t.Length == 0) return true;
        if (t.Length <= 1 && "-\\|/".Contains(t[0])) return true;
        if (t.Contains('█') || t.Contains('▒') || t.Contains('░')) return true;
        return Regex.IsMatch(t, @"^\[[= ]*\s*\d{1,3}(\.\d+)?%\s*[= ]*\]$"); // DISM progress bar
    }
}
