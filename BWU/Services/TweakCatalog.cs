using BWU.Models;
using Microsoft.Win32;

namespace BWU.Services;

/// <summary>All registry settings BWU can change, each with a plain-language explanation.</summary>
public static class TweakCatalog
{
    private const string Explorer = @"Software\Microsoft\Windows\CurrentVersion\Explorer";
    private const string Advanced = Explorer + @"\Advanced";
    private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ContentDelivery = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    private const string ClassicMenuClsid = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";

    private static RegistryValueSpec Dw(RegistryHive h, string path, string name, int value)
        => new(h, path, name, RegistryValueKind.DWord, value);
    private static RegistryValueSpec Str(RegistryHive h, string path, string name, string value)
        => new(h, path, name, RegistryValueKind.String, value);

    private const RegistryHive CU = RegistryHive.CurrentUser;
    private const RegistryHive LM = RegistryHive.LocalMachine;

    public static readonly IReadOnlyList<RegistryTweak> Performance = new[]
    {
        new RegistryTweak("perf.gamedvr", "Turn off Game DVR background capture",
            "Stops the Xbox Game Bar feature that can record gameplay in the background.",
            "Background recording and 'record that' clips stop working. Manual recording with the Game Bar is not affected.",
            new[] { Dw(CU, @"System\GameConfigStore", "GameDVR_Enabled", 0),
                    Dw(CU, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0) }),
        new RegistryTweak("perf.startupdelay", "Remove the delay before startup apps launch",
            "Windows waits several seconds after sign-in before launching startup apps. This removes that wait.",
            "Startup apps begin loading immediately, so the first seconds after sign-in can feel busier.",
            new[] { Dw(CU, Explorer + @"\Serialize", "StartupDelayInMSec", 0) },
            Note: "Takes effect at the next sign-in."),
        new RegistryTweak("perf.menudelay", "Faster menu opening",
            "Reduces the delay before sub-menus open from the default of 400 ms to 100 ms.",
            "Menus react faster. Purely cosmetic; no risk to system stability.",
            new[] { Str(CU, @"Control Panel\Desktop", "MenuShowDelay", "100") },
            Note: "Sign out and back in to apply."),
        new RegistryTweak("perf.transparency", "Turn off transparency effects",
            "Disables the translucent look of the taskbar, Start menu and some windows.",
            "Surfaces become solid. Slightly less GPU work; the look is flatter.",
            new[] { Dw(CU, Personalize, "EnableTransparency", 0) }),
        new RegistryTweak("perf.minanimate", "Turn off window minimize/maximize animation",
            "Windows appear and disappear instantly instead of animating.",
            "The interface feels snappier but less smooth. No impact on stability.",
            new[] { Str(CU, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0") },
            Note: "Sign out and back in to apply."),
    };

    public static readonly IReadOnlyList<OptimizationProfile> Profiles = new[]
    {
        new OptimizationProfile("Light tuning",
            "Safe, low-impact changes: faster startup apps and menus, and no background game capture.",
            new[] { "perf.gamedvr", "perf.startupdelay", "perf.menudelay" }),
        new OptimizationProfile("Responsive interface",
            "Everything in Light tuning, plus no transparency and no window animations for the snappiest interface.",
            new[] { "perf.gamedvr", "perf.startupdelay", "perf.menudelay", "perf.transparency", "perf.minanimate" }),
    };

    public static readonly IReadOnlyList<RegistryTweak> Privacy = new[]
    {
        new RegistryTweak("priv.adid", "Turn off the advertising ID",
            "Stops apps from using a unique ID to show you personalised ads.",
            "You will still see ads, but they will be less relevant. Apps can no longer track you across apps with this ID.",
            new[] { Dw(CU, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0) }),
        new RegistryTweak("priv.tailored", "Turn off tailored experiences from diagnostic data",
            "Stops Microsoft from using your diagnostic data to suggest tips, ads and recommendations.",
            "Tips and recommendations become generic. Windows keeps working normally.",
            new[] { Dw(CU, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0) }),
        new RegistryTweak("priv.suggestions", "Turn off suggested content and tips",
            "Hides 'suggested' apps and tips in Start and Settings.",
            "You will no longer see promotional suggestions. Nothing is uninstalled.",
            new[] { Dw(CU, ContentDelivery, "SystemPaneSuggestionsEnabled", 0),
                    Dw(CU, ContentDelivery, "SubscribedContent-338389Enabled", 0),
                    Dw(CU, ContentDelivery, "SubscribedContent-338388Enabled", 0) }),
        new RegistryTweak("priv.feedback", "Stop feedback prompts",
            "Prevents Windows from asking you for feedback.",
            "You will no longer be asked to rate Windows features.",
            new[] { Dw(CU, @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0) }),
        new RegistryTweak("priv.telemetry", "Limit diagnostic data to the minimum",
            "Sets the diagnostic data policy to the lowest level your edition allows (needs administrator).",
            "On Home and Pro editions Windows still sends the 'required' minimum, which keeps updates and security working. " +
            "Settings may show 'some settings are managed by your organization'.",
            new[] { Dw(LM, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0) },
            RequiresAdmin: true),
        new RegistryTweak("priv.activity", "Turn off activity history",
            "Stops Windows from collecting your activity history (apps and files used) on this PC (needs administrator).",
            "The Timeline-style history of recent activities is no longer kept or synced.",
            new[] { Dw(LM, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0) },
            RequiresAdmin: true),
        new RegistryTweak("priv.background", "Stop Store apps from running in the background",
            "Applies a policy that prevents Microsoft Store apps from running in the background (needs administrator).",
            "Some apps will not update tiles or send notifications until opened. Classic desktop programs are not affected.",
            new[] { Dw(LM, @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground", 2) },
            RequiresAdmin: true),
    };

    public static readonly IReadOnlyList<(string Title, string Uri)> PrivacyShortcuts = new[]
    {
        ("General privacy", "ms-settings:privacy-general"),
        ("Diagnostics & feedback", "ms-settings:privacy-feedback"),
        ("Location", "ms-settings:privacy-location"),
        ("Camera", "ms-settings:privacy-webcam"),
        ("Microphone", "ms-settings:privacy-microphone"),
        ("Background apps", "ms-settings:privacy-backgroundapps"),
        ("Activity history", "ms-settings:privacy-activityhistory"),
    };

    public static readonly IReadOnlyList<RegistryTweak> Personalization = new[]
    {
        new RegistryTweak("cust.fileext", "Show file name extensions",
            "Always shows extensions such as .pdf or .exe in File Explorer.",
            "Helps spot disguised files. File Explorer lists are slightly wider.",
            new[] { Dw(CU, Advanced, "HideFileExt", 0) }),
        new RegistryTweak("cust.hidden", "Show hidden files and folders",
            "Makes hidden files visible in File Explorer.",
            "You will see files Windows hides on purpose. Be careful when deleting them.",
            new[] { Dw(CU, Advanced, "Hidden", 1) }),
        new RegistryTweak("cust.darkmode", "Use dark mode for Windows and apps",
            "Switches the Windows and app colour mode to dark.",
            "Windows and most apps use dark colours. You can switch back at any time.",
            new[] { Dw(CU, Personalize, "AppsUseLightTheme", 0), Dw(CU, Personalize, "SystemUsesLightTheme", 0) }),
        new RegistryTweak("cust.classicmenu", "Classic right-click menu (Windows 11)",
            "Brings back the full right-click menu without 'Show more options'.",
            "The context menu looks like Windows 10. Windows updates may reset this.",
            new[] { Str(CU, ClassicMenuClsid + @"\InprocServer32", "", "") },
            MinBuild: 22000,
            DeleteKeyOnRevert: ClassicMenuClsid,
            Note: "Restart File Explorer or sign out to apply."),
    };

    public static readonly IReadOnlyList<(string Title, string Uri)> PersonalizationShortcuts = new[]
    {
        ("Personalization", "ms-settings:personalization"),
        ("Background", "ms-settings:personalization-background"),
        ("Colors", "ms-settings:colors"),
        ("Themes", "ms-settings:themes"),
        ("Lock screen", "ms-settings:lockscreen"),
        ("Start", "ms-settings:personalization-start"),
        ("Taskbar", "ms-settings:taskbar"),
        ("Fonts", "ms-settings:fonts"),
    };

    public static readonly IReadOnlyList<(string Title, string Uri)> WindowsUpdateShortcuts = new[]
    {
        ("Windows Update", "ms-settings:windowsupdate"),
        ("Update history", "ms-settings:windowsupdate-history"),
        ("Optional updates", "ms-settings:windowsupdate-optionalupdates"),
        ("Advanced options", "ms-settings:windowsupdate-options"),
    };
}
