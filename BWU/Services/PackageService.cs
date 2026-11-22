using System.Diagnostics;
using System.Text.RegularExpressions;
using BWU.Models;
using BWU.Utilities;

namespace BWU.Services;

/// <summary>Thin wrapper around the official Windows Package Manager (winget.exe). Only winget's own sources are used.</summary>
public sealed class PackageService
{
    public async Task<(bool Installed, string? Version)> DetectAsync()
    {
        var lines = new List<string>();
        var r = await ProcessRunner.RunAsync("winget", "--version", l => { lock (lines) lines.Add(l); }, System.Text.Encoding.UTF8);
        if (!r.Started || r.ExitCode != 0) return (false, null);
        return (true, lines.FirstOrDefault()?.Trim());
    }

    public async Task<IReadOnlyList<PackageItem>> SearchAsync(string query, Action<string> log, CancellationToken ct)
    {
        var lines = await CaptureAsync($"search \"{query.Replace("\"", "")}\" --accept-source-agreements", log, ct);
        return ParseTable(lines);
    }

    public async Task<IReadOnlyList<PackageItem>> ListInstalledAsync(Action<string> log, CancellationToken ct)
    {
        var lines = await CaptureAsync("list --accept-source-agreements", log, ct);
        return ParseTable(lines);
    }

    public Task<ProcessResult> InstallAsync(string id, Action<string> onLine, CancellationToken ct)
        => ProcessRunner.RunAsync("winget",
            $"install --id \"{id}\" --exact --accept-package-agreements --accept-source-agreements",
            onLine, System.Text.Encoding.UTF8, ct);

    public Task<ProcessResult> UninstallAsync(string id, Action<string> onLine, CancellationToken ct)
        => ProcessRunner.RunAsync("winget",
            $"uninstall --id \"{id}\" --exact --accept-source-agreements",
            onLine, System.Text.Encoding.UTF8, ct);

    /// <summary>Repairs/installs winget through Microsoft's official Microsoft.WinGet.Client PowerShell module.</summary>
    public Task<ProcessResult> RepairAsync(Action<string> onLine, CancellationToken ct)
    {
        const string script = """
            Write-Output 'Installing the NuGet provider (needed by PowerShellGet)...'
            Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force | Out-Null
            Write-Output 'Installing Microsoft.WinGet.Client from the PowerShell Gallery...'
            Install-Module -Name Microsoft.WinGet.Client -Force -Repository PSGallery | Out-Null
            Write-Output 'Running Repair-WinGetPackageManager...'
            Repair-WinGetPackageManager -AllUsers
            Write-Output 'Done.'
            """;
        return ProcessRunner.RunPowerShellAsync(script, onLine, ct);
    }

    public static void OpenStorePage()
        => Process.Start(new ProcessStartInfo("ms-windows-store://pdp/?productid=9NBLGGH4NNS1") { UseShellExecute = true });

    private static async Task<List<string>> CaptureAsync(string args, Action<string> log, CancellationToken ct)
    {
        var lines = new List<string>();
        var r = await ProcessRunner.RunAsync("winget", args, l => { lock (lines) lines.Add(l); }, System.Text.Encoding.UTF8, ct);
        if (!r.Started) log("Could not start winget: " + r.Error);
        else if (r.ExitCode != 0)
            foreach (var l in lines.Where(l => !ProcessRunner.IsNoise(l))) log(l);
        return lines;
    }

    /// <summary>Parses winget's text table using the dashed separator line and the header column positions (language independent).</summary>
    private static IReadOnlyList<PackageItem> ParseTable(List<string> lines)
    {
        var result = new List<PackageItem>();
        var sep = lines.FindIndex(l => l.Length >= 5 && l.All(c => c == '-'));
        if (sep < 1) return result;

        var starts = Regex.Matches(lines[sep - 1], @"\S+(?: \S+)*").Select(m => m.Index).ToList();
        if (starts.Count < 2) return result;

        static string Slice(string s, int from, int to)
        {
            if (from >= s.Length) return "";
            to = Math.Min(to, s.Length);
            return s[from..to].Trim();
        }

        for (var i = sep + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length <= starts[1]) continue;
            var name = Slice(line, 0, starts[1]);
            var id = Slice(line, starts[1], starts.Count > 2 ? starts[2] : line.Length);
            var version = starts.Count > 2 ? Slice(line, starts[2], starts.Count > 3 ? starts[3] : line.Length) : "";
            if (id.Length == 0 || id.Contains(' ')) continue;
            result.Add(new PackageItem { Name = name, Id = id, Version = version });
        }
        return result;
    }
}
