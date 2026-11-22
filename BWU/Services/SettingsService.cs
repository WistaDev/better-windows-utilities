using System.Text.Json;
using BWU.Models;
using Microsoft.Win32;

namespace BWU.Services;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string Language { get; set; } = "en";
    public string UpdateManifestUrl { get; set; } = "";
}

public sealed class SettingsService
{
    public static SettingsService Instance { get; } = new();

    private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BWU");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public AppSettings Settings { get; private set; } = new();
    public static string DataDirectory => Dir;

    public void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { Settings = new AppSettings(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* settings are a convenience; failing to save must not crash the app */ }
    }

    public bool IsStartWithWindows
    {
        get
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(AppInfo.Name) != null;
        }
    }

    public OperationResult SetStartWithWindows(bool enable)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey, true);
            if (enable) k.SetValue(AppInfo.Name, "\"" + Environment.ProcessPath + "\"", RegistryValueKind.String);
            else k.DeleteValue(AppInfo.Name, false);
            return OperationResult.Ok(enable ? "BWU will start with Windows." : "BWU will no longer start with Windows.");
        }
        catch (Exception ex) { return OperationResult.Fail(ex.Message); }
    }
}
