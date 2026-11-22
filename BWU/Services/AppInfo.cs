using System.Reflection;

namespace BWU.Services;

public static class AppInfo
{
    public const string Name = "BWU";
    public const string FullName = "Better Windows Utilities";
    public const string Slogan = "Make Windows Better.";
    public const string Developer = "BWU Project";   // edit here to brand your own build
    public const string Copyright = "© BWU Project";

    public static string Version
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "1.0" : $"{v.Major}.{v.Minor}";
        }
    }

    public static Version VersionObject => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0);
}
