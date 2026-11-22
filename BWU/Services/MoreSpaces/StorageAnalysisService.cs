namespace BWU.Services.MoreSpaces;

public sealed class StorageAnalysisService
{
    public StorageInfo? GetSystemDrive()
    {
        try
        {
            var d = new DriveInfo(WindowsService.SystemDriveRoot);
            return new StorageInfo(d.Name, d.TotalSize, d.AvailableFreeSpace);
        }
        catch { return null; }
    }
}
