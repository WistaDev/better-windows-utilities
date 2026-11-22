using Microsoft.Win32;

namespace BWU.Services.MoreSpaces;

public sealed record PrefetchInfo(string Path, bool Exists, bool AccessDenied, int FileCount, int PfFiles, long TotalBytes, string PrefetcherState);

/// <summary>Read-only analysis of C:\Windows\Prefetch. BWU never deletes Prefetch data: Windows uses it to speed up app startup.</summary>
public sealed class PrefetchService
{
    public string Path => System.IO.Path.Combine(WindowsService.WindowsDirectory, "Prefetch");

    public Task<PrefetchInfo> AnalyzeAsync(CancellationToken ct = default) => Task.Run(() =>
    {
        var state = ReadPrefetcherState();
        if (!Directory.Exists(Path)) return new PrefetchInfo(Path, false, false, 0, 0, 0, state);
        try
        {
            long total = 0; int count = 0, pf = 0;
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = false, AttributesToSkip = FileAttributes.ReparsePoint };
            foreach (var fi in new DirectoryInfo(Path).EnumerateFiles("*", options))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    total += fi.Length; count++;
                    if (fi.Extension.Equals(".pf", StringComparison.OrdinalIgnoreCase)) pf++;
                }
                catch { }
            }
            return new PrefetchInfo(Path, true, false, count, pf, total, state);
        }
        catch (UnauthorizedAccessException) { return new PrefetchInfo(Path, true, true, 0, 0, 0, state); }
    }, ct);

    private static string ReadPrefetcherState()
    {
        try
        {
            using var bk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var k = bk.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters");
            return k?.GetValue("EnablePrefetcher") switch
            {
                0 => "Disabled", 1 => "Application launch prefetching", 2 => "Boot prefetching", 3 => "Application launch and boot prefetching",
                _ => "Unknown"
            };
        }
        catch { return "Unknown"; }
    }
}
