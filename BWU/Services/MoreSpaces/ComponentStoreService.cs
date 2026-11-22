using BWU.Models;
using BWU.Utilities;

namespace BWU.Services.MoreSpaces;

/// <summary>DISM component store cleanup (no /ResetBase, so installed updates stay uninstallable).</summary>
public sealed class ComponentStoreService
{
    public async Task<OperationResult> CleanupAsync(Action<string> log, Action<double> progress)
    {
        log("> Dism.exe /Online /Cleanup-Image /StartComponentCleanup");
        var r = await ProcessRunner.RunAsync("Dism.exe", "/Online /Cleanup-Image /StartComponentCleanup", l =>
        {
            if (ProcessRunner.TryParsePercent(l) is { } p) progress(p);
            if (!ProcessRunner.IsNoise(l)) log(l);
        });
        if (!r.Started) return OperationResult.Fail("DISM could not be started: " + r.Error);
        return r.ExitCode == 0
            ? OperationResult.Ok("Component store cleanup completed.")
            : OperationResult.Fail($"DISM failed with exit code {r.ExitCode}. See the log for details.");
    }
}
