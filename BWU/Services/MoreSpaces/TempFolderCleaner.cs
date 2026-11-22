using System.Diagnostics;

namespace BWU.Services.MoreSpaces;

/// <summary>
/// Shared, deliberately conservative logic for the Windows Temp and User Temp cleaners.
/// A file is only deleted when ALL of these hold:
///   - it is older than <see cref="MinAge"/> (recent files may belong to a running installer),
///   - it is not read-only or a system file,
///   - it is not a reparse point (symlink/junction) and is not reached through one,
///   - it can be opened exclusively right now (i.e. no other process is using it).
/// Nothing is ever force-deleted and no attributes are changed.
/// </summary>
internal static class TempFolderCleaner
{
    public static readonly TimeSpan MinAge = TimeSpan.FromHours(1);

    private static readonly EnumerationOptions Recurse = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        ReturnSpecialDirectories = false
    };

    /// <summary>Refuses folders that do not look like a real temp folder (guards against a misconfigured %TEMP%).</summary>
    public static string? ValidateTarget(string path)
    {
        try
        {
            var full = Path.GetFullPath(path).TrimEnd('\\');
            var name = Path.GetFileName(full);
            if (Path.GetPathRoot(full)?.TrimEnd('\\').Equals(full, StringComparison.OrdinalIgnoreCase) == true)
                return "Refused: the folder is a drive root.";
            if (!name.Equals("Temp", StringComparison.OrdinalIgnoreCase) && !name.Equals("Tmp", StringComparison.OrdinalIgnoreCase))
                return "Refused: this does not look like a temporary folder (its name is not 'Temp' or 'Tmp').";
            return null;
        }
        catch (Exception ex) { return "Refused: " + ex.Message; }
    }

    public static FolderAnalysis Analyze(string path, CancellationToken ct)
    {
        var refusal = ValidateTarget(path);
        if (refusal != null) return new(path, Directory.Exists(path), false, 0, 0, 0, 0, refusal);
        if (!Directory.Exists(path)) return new(path, false, false, 0, 0, 0, 0, "Folder not found.");

        try { _ = Directory.EnumerateFileSystemEntries(path).FirstOrDefault(); }
        catch (UnauthorizedAccessException) { return new(path, true, true, 0, 0, 0, 0, "Access denied. Run BWU as administrator to analyze this folder."); }

        long total = 0, eligible = 0;
        int count = 0, eligibleCount = 0;
        var cutoff = DateTime.UtcNow - MinAge;
        foreach (var fi in new DirectoryInfo(path).EnumerateFiles("*", Recurse))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var len = fi.Length;
                total += len; count++;
                if (IsCandidate(fi, cutoff)) { eligible += len; eligibleCount++; }
            }
            catch { /* file vanished or unreadable */ }
        }
        return new(path, true, false, total, count, eligible, eligibleCount, null);
    }

    private static bool IsCandidate(FileInfo fi, DateTime cutoffUtc)
        => fi.LastWriteTimeUtc < cutoffUtc
           && (fi.Attributes & (FileAttributes.ReadOnly | FileAttributes.System)) == 0;

    public static CleanupResult Clean(string path, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var refusal = ValidateTarget(path);
        if (refusal != null) return new(0, 0, 0, 0, sw.Elapsed, false, refusal);
        if (!Directory.Exists(path)) return new(0, 0, 0, 0, sw.Elapsed, false, "Folder not found.");

        var cutoff = DateTime.UtcNow - MinAge;
        var candidates = new List<(string Path, long Length)>();
        foreach (var fi in new DirectoryInfo(path).EnumerateFiles("*", Recurse))
        {
            try { if (IsCandidate(fi, cutoff)) candidates.Add((fi.FullName, fi.Length)); } catch { }
        }

        long freed = 0; int deleted = 0, skipped = 0, dirs = 0;
        for (var i = 0; i < candidates.Count; i++)
        {
            if (ct.IsCancellationRequested) return new(freed, deleted, skipped, dirs, sw.Elapsed, true, "Cancelled; files already deleted stay deleted.");
            var (file, length) = candidates[i];
            try
            {
                // Exclusive open succeeds only if no other process currently has the file open.
                using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                File.Delete(file);
                freed += length; deleted++;
            }
            catch { skipped++; }   // locked, protected or already gone: leave it alone

            if (i % 25 == 0 || i == candidates.Count - 1)
                progress?.Report(new ProgressInfo(100.0 * (i + 1) / candidates.Count, $"Cleaning… {i + 1} of {candidates.Count} files"));
        }

        // Remove folders that are now empty (deepest first). Non-empty or in-use folders simply fail and are skipped.
        try
        {
            var directories = new DirectoryInfo(path).EnumerateDirectories("*", Recurse)
                .OrderByDescending(d => d.FullName.Length).ToList();
            foreach (var d in directories)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    if (d.LastWriteTimeUtc >= cutoff) continue;
                    Directory.Delete(d.FullName, false);
                    dirs++;
                }
                catch { /* not empty or in use */ }
            }
        }
        catch { }

        progress?.Report(new ProgressInfo(100, "Done"));
        return new(freed, deleted, skipped, dirs, sw.Elapsed, false, null);
    }
}
