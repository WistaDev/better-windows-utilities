namespace BWU.Services.MoreSpaces;

public sealed record FolderAnalysis(string Path, bool Exists, bool AccessDenied, long TotalBytes, int FileCount,
    long EligibleBytes, int EligibleCount, string? Note);

public sealed record CleanupResult(long BytesFreed, int FilesDeleted, int FilesSkipped, int DirectoriesRemoved,
    TimeSpan Elapsed, bool Cancelled, string? Note);

public sealed record ProgressInfo(double Percent, string Message);

public sealed record StorageInfo(string DriveRoot, long Total, long Free)
{
    public long Used => Total - Free;
    public double UsedPercent => Total <= 0 ? 0 : 100.0 * Used / Total;
}
