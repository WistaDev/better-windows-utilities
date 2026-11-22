using Microsoft.Win32;

namespace BWU.Services;

/// <summary>Windows version detection and compatibility helpers.</summary>
public static class WindowsService
{
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    public static int BuildNumber { get; } = ReadBuildNumber();
    public static bool IsWindows11 => BuildNumber >= 22000;
    public static bool IsSupportedOs => Environment.OSVersion.Version.Major >= 10;
    public static string WindowsDirectory => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    public static string SystemDriveRoot => Path.GetPathRoot(WindowsDirectory) ?? @"C:\";

    public static string? ReadString(string name)
    {
        using var bk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var k = bk.OpenSubKey(CurrentVersionKey);
        return k?.GetValue(name)?.ToString();
    }

    public static int? ReadDword(string name)
    {
        using var bk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var k = bk.OpenSubKey(CurrentVersionKey);
        return k?.GetValue(name) is int i ? i : null;
    }

    public static string ProductName
    {
        get
        {
            var name = ReadString("ProductName") ?? "Windows";
            // Windows 11 still reports "Windows 10 ..." in ProductName on many builds.
            if (IsWindows11 && name.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase))
                name = "Windows 11" + name[10..];
            return name;
        }
    }

    public static string? EditionId => ReadString("EditionID");
    public static string? DisplayVersion => ReadString("DisplayVersion") ?? ReadString("ReleaseId");

    public static string FullBuild
    {
        get
        {
            var build = ReadString("CurrentBuildNumber") ?? BuildNumber.ToString();
            var ubr = ReadDword("UBR");
            return ubr.HasValue ? $"{build}.{ubr}" : build;
        }
    }

    public static DateTime? InstallDate
    {
        get
        {
            var seconds = ReadDword("InstallDate");
            return seconds.HasValue ? DateTimeOffset.FromUnixTimeSeconds(seconds.Value).LocalDateTime : null;
        }
    }

    private static int ReadBuildNumber()
    {
        if (int.TryParse(ReadString("CurrentBuildNumber"), out var b)) return b;
        return Environment.OSVersion.Version.Build;
    }
}
