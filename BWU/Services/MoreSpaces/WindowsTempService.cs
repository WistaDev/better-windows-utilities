namespace BWU.Services.MoreSpaces;

public sealed class WindowsTempService
{
    public string Path => System.IO.Path.Combine(WindowsService.WindowsDirectory, "Temp");

    public Task<FolderAnalysis> AnalyzeAsync(CancellationToken ct = default)
        => Task.Run(() => TempFolderCleaner.Analyze(Path, ct), ct);

    public Task<CleanupResult> CleanAsync(IProgress<ProgressInfo>? progress, CancellationToken ct)
        => Task.Run(() => TempFolderCleaner.Clean(Path, progress, ct), CancellationToken.None);
}
