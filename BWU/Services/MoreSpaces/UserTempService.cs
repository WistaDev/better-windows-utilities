namespace BWU.Services.MoreSpaces;

public sealed class UserTempService
{
    public string Path
    {
        get
        {
            var temp = Environment.GetEnvironmentVariable("TEMP");
            if (string.IsNullOrWhiteSpace(temp)) temp = System.IO.Path.GetTempPath();
            return System.IO.Path.GetFullPath(temp).TrimEnd('\\');
        }
    }

    public Task<FolderAnalysis> AnalyzeAsync(CancellationToken ct = default)
        => Task.Run(() => TempFolderCleaner.Analyze(Path, ct), ct);

    public Task<CleanupResult> CleanAsync(IProgress<ProgressInfo>? progress, CancellationToken ct)
        => Task.Run(() => TempFolderCleaner.Clean(Path, progress, ct), CancellationToken.None);
}
