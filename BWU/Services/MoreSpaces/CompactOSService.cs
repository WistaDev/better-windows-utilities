using BWU.Models;
using BWU.Utilities;

namespace BWU.Services.MoreSpaces;

public enum CompactState { Unknown, Compressed, NotCompressed }

/// <summary>Compact OS through the supported compact.exe /CompactOS switches.</summary>
public sealed class CompactOSService
{
    public async Task<(CompactState State, string Raw)> QueryAsync(CancellationToken ct = default)
    {
        var lines = new List<string>();
        var r = await ProcessRunner.RunAsync("compact.exe", "/CompactOS:query", l => { lock (lines) lines.Add(l); }, null, ct);
        var raw = string.Join(" ", lines).Trim();
        if (!r.Started) return (CompactState.Unknown, "compact.exe could not be started: " + r.Error);
        return (Parse(raw), raw);
    }

    // compact.exe localises its message; anything unrecognised is reported as Unknown and the raw text is shown.
    private static CompactState Parse(string text)
    {
        var t = text.ToLowerInvariant();
        if (t.Contains("not in the compact") || t.Contains("não está no estado compact") || t.Contains("nao esta no estado compact"))
            return CompactState.NotCompressed;
        if (t.Contains("in the compact state") || t.Contains("está no estado compact") || t.Contains("esta no estado compact"))
            return CompactState.Compressed;
        return CompactState.Unknown;
    }

    public async Task<OperationResult> SetAsync(bool enable, Action<string> log)
    {
        if (!WindowsService.IsSupportedOs) return OperationResult.Fail("Compact OS requires Windows 10 or later.");
        var (before, raw) = await QueryAsync();
        log("Current state: " + (before == CompactState.Unknown ? "unknown (" + raw + ")" : before.ToString()));

        if (enable && before == CompactState.Compressed) return OperationResult.Ok("Compact OS is already enabled. Nothing to do.");
        if (!enable && before == CompactState.NotCompressed) return OperationResult.Ok("Compact OS is already disabled. Nothing to do.");

        var args = enable ? "/CompactOS:always" : "/CompactOS:never";
        log($"> compact.exe {args}");
        var r = await ProcessRunner.RunAsync("compact.exe", args, l => { if (!ProcessRunner.IsNoise(l)) log(l); });
        if (!r.Started) return OperationResult.Fail("compact.exe could not be started: " + r.Error);
        if (r.ExitCode != 0) return OperationResult.Fail($"compact.exe failed with exit code {r.ExitCode}. See the log for details.");

        var (after, afterRaw) = await QueryAsync();
        var expected = enable ? CompactState.Compressed : CompactState.NotCompressed;
        if (after == expected) return OperationResult.Ok(enable ? "Compact OS is now enabled." : "Compact OS is now disabled.");
        if (after == CompactState.Unknown)
            return OperationResult.Fail("compact.exe finished, but BWU could not confirm the new state: " + afterRaw);
        return OperationResult.Fail("compact.exe finished, but Windows still reports the previous state: " + afterRaw);
    }
}
