using System.Text.Json;
using BWU.Models;
using BWU.Utilities;

namespace BWU.Services;

public sealed class DebloaterService
{
    // Optional/preinstalled apps BWU knows are safe to remove (exact package names).
    private static readonly Dictionary<string, (string Title, string Note)> OptionalApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.BingWeather"] = ("Weather", "Weather app."),
        ["Microsoft.BingNews"] = ("News", "News app."),
        ["Microsoft.BingFinance"] = ("Money", "Finance app."),
        ["Microsoft.BingSports"] = ("Sports", "Sports app."),
        ["Microsoft.BingSearch"] = ("Bing Search", "Search integration app."),
        ["Microsoft.GetHelp"] = ("Get Help", "Support app. Settings can still contact support."),
        ["Microsoft.Getstarted"] = ("Tips", "Windows tips app."),
        ["Microsoft.MicrosoftOfficeHub"] = ("Microsoft 365 (Office hub)", "Launcher for Office web apps."),
        ["Microsoft.MicrosoftSolitaireCollection"] = ("Solitaire Collection", "Card games with ads."),
        ["Microsoft.People"] = ("People", "Contacts app."),
        ["Microsoft.SkypeApp"] = ("Skype", "Consumer Skype app."),
        ["Microsoft.YourPhone"] = ("Phone Link", "Phone integration. Remove only if you do not link a phone."),
        ["Microsoft.WindowsFeedbackHub"] = ("Feedback Hub", "Sends feedback to Microsoft."),
        ["Microsoft.MixedReality.Portal"] = ("Mixed Reality Portal", "Only for VR headsets."),
        ["Microsoft.Microsoft3DViewer"] = ("3D Viewer", "3D model viewer."),
        ["Microsoft.Print3D"] = ("Print 3D", "3D printing app."),
        ["Microsoft.MSPaint"] = ("Paint 3D", "Paint 3D (the classic Paint is a separate component)."),
        ["Microsoft.OneConnect"] = ("Mobile Plans", "Mobile data plans app."),
        ["Microsoft.Messaging"] = ("Messaging", "Legacy messaging app."),
        ["Microsoft.Wallet"] = ("Microsoft Pay", "Legacy wallet app."),
        ["Microsoft.WindowsMaps"] = ("Maps", "Maps app."),
        ["Microsoft.Todos"] = ("Microsoft To Do", "Task list app."),
        ["Microsoft.PowerAutomateDesktop"] = ("Power Automate", "Automation tool."),
        ["Microsoft.ZuneVideo"] = ("Movies & TV", "Video player."),
        ["Microsoft.XboxApp"] = ("Xbox Console Companion", "Legacy Xbox app."),
        ["Microsoft.GamingApp"] = ("Xbox app", "Xbox/PC Game Pass launcher."),
        ["Microsoft.XboxGameOverlay"] = ("Xbox Game Bar plugin", "Game overlay component."),
        ["Microsoft.XboxGamingOverlay"] = ("Xbox Game Bar", "Game Bar overlay."),
        ["Microsoft.XboxSpeechToTextOverlay"] = ("Xbox speech to text", "Game chat transcription."),
        ["Clipchamp.Clipchamp"] = ("Clipchamp", "Video editor."),
        ["MicrosoftTeams"] = ("Microsoft Teams (personal)", "Consumer chat app."),
        ["MSTeams"] = ("Microsoft Teams (personal)", "Consumer chat app."),
        ["Microsoft.549981C3F5F10"] = ("Cortana", "Legacy voice assistant app."),
        ["Microsoft.Copilot"] = ("Copilot", "AI assistant app."),
        ["Microsoft.OutlookForWindows"] = ("Outlook (new)", "New Outlook app."),
        ["MicrosoftCorporationII.MicrosoftFamily"] = ("Microsoft Family", "Family safety app."),
    };

    private static readonly string[] PromoPrefixes =
    {
        "king.com.", "SpotifyAB.", "Disney", "Facebook", "Instagram", "TikTok", "Netflix", "AmazonAlexa", "Duolingo", "HULU", "Pandora", "Twitter"
    };

    // Components other parts of Windows depend on: never selectable.
    private static readonly string[] ProtectedPrefixes =
    {
        "Microsoft.Windows.", "Windows.", "MicrosoftWindows.", "Microsoft.UI.Xaml", "Microsoft.VCLibs", "Microsoft.NET.",
        "Microsoft.WindowsStore", "Microsoft.StorePurchaseApp", "Microsoft.DesktopAppInstaller", "Microsoft.SecHealthUI",
        "Microsoft.AAD.", "Microsoft.AccountsControl", "Microsoft.LockApp", "Microsoft.Win32WebViewHost", "Microsoft.MicrosoftEdge",
        "Microsoft.Services.Store", "Microsoft.WebpImageExtension", "Microsoft.HEIFImageExtension", "Microsoft.VP9VideoExtensions",
        "Microsoft.WebMediaExtensions", "Microsoft.AV1VideoExtension", "Microsoft.HEVCVideoExtension", "Microsoft.CredDialogHost",
        "Microsoft.ECApp", "Microsoft.AsyncTextService", "Microsoft.BioEnrollment", "Microsoft.XboxIdentityProvider",
        "Microsoft.Xbox.TCUI", "Microsoft.WidgetsPlatformRuntime", "MicrosoftCorporationII.WindowsSubsystem", "NcsiUwpApp"
    };

    public async Task<(IReadOnlyList<AppxItem> Items, string? Error)> ListAsync(CancellationToken ct = default)
    {
        const string script = """
            Get-AppxPackage | Select-Object Name, PackageFullName, Publisher, Version, IsFramework, NonRemovable,
              @{n='Sig';e={"$($_.SignatureKind)"}} | ConvertTo-Json -Compress
            """;
        var lines = new List<string>();
        var r = await ProcessRunner.RunPowerShellAsync(script, l => { lock (lines) lines.Add(l); }, ct);
        if (!r.Started) return (Array.Empty<AppxItem>(), "Could not start PowerShell: " + r.Error);
        if (r.ExitCode != 0) return (Array.Empty<AppxItem>(), "PowerShell failed: " + string.Join(" ", lines));

        try
        {
            using var doc = JsonDocument.Parse(string.Join("", lines));
            var elements = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().ToList()
                : new List<JsonElement> { doc.RootElement };

            var items = new List<AppxItem>();
            foreach (var e in elements)
            {
                string Get(string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
                bool Flag(string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.True;

                var name = Get("Name");
                if (name.Length == 0) continue;
                var (category, note, display) = Classify(name, Flag("IsFramework"), Flag("NonRemovable"), Get("Sig"));
                items.Add(new AppxItem
                {
                    Name = name, DisplayName = display, PackageFullName = Get("PackageFullName"),
                    Publisher = Get("Publisher"), Version = Get("Version"), Category = category, Note = note
                });
            }
            return (items.OrderBy(i => i.Category).ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(), null);
        }
        catch (Exception ex) { return (Array.Empty<AppxItem>(), "Could not read the app list: " + ex.Message); }
    }

    private static (AppCategory, string, string) Classify(string name, bool framework, bool nonRemovable, string signature)
    {
        if (framework) return (AppCategory.Protected, "Framework used by other apps.", name);
        if (nonRemovable) return (AppCategory.Protected, "Marked non-removable by Windows.", name);
        if (signature.Equals("System", StringComparison.OrdinalIgnoreCase))
            return (AppCategory.Protected, "Part of Windows.", name);
        if (ProtectedPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return (AppCategory.Protected, "Required by Windows or by other components.", name);
        if (OptionalApps.TryGetValue(name, out var known))
            return (AppCategory.Optional, known.Note, known.Title);
        if (PromoPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return (AppCategory.Optional, "Promotional app that is often preinstalled.", name);
        return (AppCategory.Other, "Installed Store app. Remove only if you do not use it.", name);
    }

    public async Task<OperationResult> RemoveAsync(AppxItem item, CancellationToken ct = default)
    {
        if (item.Category == AppCategory.Protected)
            return OperationResult.Fail("Protected component: BWU never removes it.");
        if (item.PackageFullName.Contains('\''))
            return OperationResult.Fail("Unexpected characters in the package name.");

        var script = $$"""
            try { Remove-AppxPackage -Package '{{item.PackageFullName}}' -ErrorAction Stop; exit 0 }
            catch { Write-Output $_.Exception.Message; exit 1 }
            """;
        var lines = new List<string>();
        var r = await ProcessRunner.RunPowerShellAsync(script, l => { lock (lines) lines.Add(l); }, ct);
        return r.Started && r.ExitCode == 0
            ? OperationResult.Ok("Removed for the current user.")
            : OperationResult.Fail(string.Join(" ", lines).Trim() is { Length: > 0 } m ? m : (r.Error ?? "Removal failed."));
    }
}
