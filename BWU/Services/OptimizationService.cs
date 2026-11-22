using System.ServiceProcess;
using BWU.Models;
using BWU.Utilities;
using Microsoft.Win32;

namespace BWU.Services;

public sealed class OptimizationService
{
    private readonly BackupStore _backups;
    public OptimizationService(BackupStore backups) => _backups = backups;

    // ---------- Startup entries (Run keys, enabled/disabled the same way Task Manager does) ----------

    private static readonly (string Needle, string Explanation)[] KnownOptional =
    {
        ("spotify", "Music player; it does not need to start with Windows to work."),
        ("discord", "Chat app; can be opened manually when needed."),
        ("steam", "Game launcher; can be opened manually."),
        ("teams", "Chat/meeting app; can be opened manually."),
        ("skype", "Messaging app; can be opened manually."),
        ("epicgames", "Game launcher; can be opened manually."),
        ("googleupdate", "Background updater; the app also updates when opened."),
        ("adobe", "Adobe helper/updater; usually not needed at startup."),
        ("jusched", "Java updater; usually not needed at startup."),
        ("zoom", "Meeting app; can be opened manually."),
        ("dropbox", "Cloud sync client; disable only if you do not need constant syncing."),
        ("ccleaner", "Cleaner tool; does not need to run at startup."),
    };

    public IReadOnlyList<StartupEntry> GetStartupEntries()
    {
        var list = new List<StartupEntry>();
        ReadRunKey(RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run", "Run", false, "HKCU\\...\\Run", list);
        ReadRunKey(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Run", true, "HKLM\\...\\Run", list);
        ReadRunKey(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Run32", true, "HKLM\\...\\Run (32-bit)", list);
        return list.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void ReadRunKey(RegistryHive hive, RegistryView view, string path, string approvedName,
        bool machine, string location, List<StartupEntry> list)
    {
        try
        {
            using var bk = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(path);
            if (key == null) return;
            using var approved = bk.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\" + approvedName);

            foreach (var name in key.GetValueNames())
            {
                var command = key.GetValue(name)?.ToString() ?? "";
                var enabled = true;
                if (approved?.GetValue(name) is byte[] state && state.Length > 0) enabled = (state[0] & 1) == 0;
                list.Add(new StartupEntry
                {
                    Name = name, Command = command, Location = location, IsMachine = machine,
                    ApprovedKeyName = approvedName, IsEnabled = enabled, Hint = BuildHint(name, command)
                });
            }
        }
        catch { /* unreadable key: skip */ }
    }

    private static string? BuildHint(string name, string command)
    {
        var exe = ExtractExecutable(command);
        if (exe != null && !File.Exists(exe)) return "Possibly unnecessary: the target file no longer exists, so this entry is probably broken.";
        var hay = (name + " " + command).ToLowerInvariant();
        foreach (var (needle, explanation) in KnownOptional)
            if (hay.Contains(needle)) return "Commonly non-essential at startup: " + explanation;
        return null;
    }

    private static string? ExtractExecutable(string command)
    {
        var c = Environment.ExpandEnvironmentVariables(command.Trim());
        if (c.Length == 0) return null;
        if (c[0] == '"')
        {
            var end = c.IndexOf('"', 1);
            return end > 1 ? c[1..end] : null;
        }
        var idx = c.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return idx > 0 ? c[..(idx + 4)] : null;
    }

    public OperationResult SetStartupEnabled(StartupEntry entry, bool enable)
    {
        if (entry.IsMachine && !AdminHelper.IsAdministrator)
            return OperationResult.Fail("This entry is for all users. Administrator rights are required.");
        try
        {
            var hive = entry.IsMachine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
            using var bk = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var k = bk.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\" + entry.ApprovedKeyName, true);
            var data = new byte[12];
            if (enable) data[0] = 2;
            else
            {
                data[0] = 3;
                BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
            }
            k.SetValue(entry.Name, data, RegistryValueKind.Binary);
            entry.IsEnabled = enable;
            return OperationResult.Ok(enable ? "Startup entry enabled." : "Startup entry disabled. It was not deleted and can be enabled again.");
        }
        catch (Exception ex) { return OperationResult.Fail(ex.Message); }
    }

    // ---------- Optional background services (curated list only; critical services are never touched) ----------

    private static readonly (string Name, string Title, string Explanation)[] OptionalServices =
    {
        ("DiagTrack", "Connected User Experiences and Telemetry", "Collects and sends diagnostic data to Microsoft. Setting it to Manual reduces telemetry and does not affect normal use."),
        ("SysMain", "SysMain (Superfetch)", "Preloads frequently used apps into memory. Helpful on hard drives, little benefit on SSDs."),
        ("WSearch", "Windows Search", "Builds the search index. Manual start makes file search slower and less complete."),
        ("MapsBroker", "Downloaded Maps Manager", "Only needed if you use offline maps in the Maps app."),
        ("Fax", "Fax", "Only needed if you send faxes from this PC."),
        ("RetailDemo", "Retail Demo Service", "Used only by store demo units."),
        ("WerSvc", "Windows Error Reporting", "Sends crash reports to Microsoft. Disabling it stops automatic crash reporting."),
        ("PhoneSvc", "Phone Service", "Telephony state management; rarely needed on desktops."),
        ("lfsvc", "Geolocation Service", "Provides location to apps. Apps that need your location may stop working."),
        ("XblAuthManager", "Xbox Live Auth Manager", "Needed for Xbox sign-in in games and the Xbox app. Safe to leave on Manual if you do not play Xbox/Store games."),
        ("XblGameSave", "Xbox Live Game Save", "Cloud saves for Xbox/Store games."),
        ("XboxNetApiSvc", "Xbox Live Networking Service", "Xbox Live multiplayer networking."),
    };

    public IReadOnlyList<ServiceInfo> GetServices()
    {
        var result = new List<ServiceInfo>();
        Dictionary<string, ServiceController> installed;
        try { installed = ServiceController.GetServices().ToDictionary(s => s.ServiceName, StringComparer.OrdinalIgnoreCase); }
        catch { return result; }

        foreach (var (name, title, explanation) in OptionalServices)
        {
            if (!installed.TryGetValue(name, out var sc)) continue;
            try
            {
                var delayed = IsDelayed(name);
                var start = sc.StartType == ServiceStartMode.Automatic && delayed ? "Automatic (Delayed)" : sc.StartType.ToString();
                result.Add(new ServiceInfo
                {
                    Name = name, Title = title, Explanation = explanation, StartType = start,
                    Status = sc.Status.ToString(), IsAutomatic = sc.StartType == ServiceStartMode.Automatic,
                    HasBackup = _backups.Has("service:" + name)
                });
            }
            catch { /* service not readable */ }
        }
        return result;
    }

    private static bool IsDelayed(string service)
    {
        using var bk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var k = bk.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + service);
        return k?.GetValue("DelayedAutostart") is int d && d == 1;
    }

    public async Task<OperationResult> SetServiceManualAsync(string name)
    {
        if (!OptionalServices.Any(s => s.Name == name)) return OperationResult.Fail("This service is not in BWU's reviewed list.");
        if (!AdminHelper.IsAdministrator) return OperationResult.Fail("Administrator rights are required.");
        try
        {
            using var sc = new ServiceController(name);
            if (sc.StartType != ServiceStartMode.Automatic)
                return OperationResult.Fail($"The service is already set to {sc.StartType}; nothing to change.");
            var original = IsDelayed(name) ? "delayed-auto" : "auto";
            _backups.AddIfMissing("service:" + name, new BackupEntry(true, "ServiceStart", original));
        }
        catch (Exception ex) { return OperationResult.Fail(ex.Message); }
        return await ConfigureAsync(name, "demand", "Set to Manual. The service keeps running until the next restart, then starts only when needed.");
    }

    public async Task<OperationResult> RestoreServiceAsync(string name)
    {
        if (!AdminHelper.IsAdministrator) return OperationResult.Fail("Administrator rights are required.");
        var entry = _backups.Get("service:" + name);
        if (entry?.Data == null) return OperationResult.Fail("No BWU backup exists for this service.");
        var result = await ConfigureAsync(name, entry.Data, "Original startup type restored.");
        if (result.Success) _backups.Remove("service:" + name);
        return result;
    }

    private static async Task<OperationResult> ConfigureAsync(string name, string startValue, string okMessage)
    {
        var lines = new List<string>();
        var r = await ProcessRunner.RunAsync("sc.exe", $"config \"{name}\" start= {startValue}", l => { lock (lines) lines.Add(l); });
        return r.Started && r.ExitCode == 0
            ? OperationResult.Ok(okMessage)
            : OperationResult.Fail("sc.exe failed: " + (r.Error ?? string.Join(" ", lines)));
    }
}
