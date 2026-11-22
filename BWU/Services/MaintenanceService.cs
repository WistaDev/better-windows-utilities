using System.Text;
using BWU.Utilities;
using Microsoft.Win32;

namespace BWU.Services;

public sealed class MaintenanceOperation
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public bool RequiresAdmin { get; init; }
    public bool ChangesSystem { get; init; }
    public string? ConfirmText { get; init; }
    public required Func<Action<string>, CancellationToken, Task<int>> Run { get; init; }
    public string AdminBadge => RequiresAdmin ? "Administrator" : "";
}

public sealed class MaintenanceService
{
    private static readonly Encoding Utf16 = Encoding.Unicode;   // sfc.exe writes UTF-16 even when redirected

    private static string Drive => WindowsService.SystemDriveRoot.TrimEnd('\\');

    private static async Task<int> Exec(string file, string args, Action<string> log, Encoding? enc, CancellationToken ct)
    {
        log($"> {file} {args}");
        var r = await ProcessRunner.RunAsync(file, args, log, enc, ct);
        if (!r.Started) { log("Could not start the program: " + r.Error); return -1; }
        log($"Finished with exit code {r.ExitCode}.");
        return r.ExitCode;
    }

    public IReadOnlyList<MaintenanceOperation> Operations { get; }

    public MaintenanceService()
    {
        Operations = new List<MaintenanceOperation>
        {
            new()
            {
                Title = "Verify system integrity (read-only)",
                Description = "sfc /verifyonly checks protected system files and reports problems without repairing anything.",
                RequiresAdmin = true,
                Run = (log, ct) => Exec("sfc.exe", "/verifyonly", log, Utf16, ct)
            },
            new()
            {
                Title = "System File Checker – scan and repair",
                Description = "sfc /scannow scans protected system files and replaces damaged ones from the local component store. It can take 10–30 minutes.",
                RequiresAdmin = true, ChangesSystem = true,
                ConfirmText = "SFC will repair damaged Windows system files if it finds any. This can take a long time; do not shut down the PC meanwhile.",
                Run = (log, ct) => Exec("sfc.exe", "/scannow", log, Utf16, ct)
            },
            new()
            {
                Title = "DISM – CheckHealth",
                Description = "Quickly checks whether the Windows image has been flagged as corrupted. Makes no changes.",
                RequiresAdmin = true,
                Run = (log, ct) => Exec("Dism.exe", "/Online /Cleanup-Image /CheckHealth", log, null, ct)
            },
            new()
            {
                Title = "DISM – ScanHealth",
                Description = "Performs a deeper scan of the Windows image for corruption. Makes no changes. May take several minutes.",
                RequiresAdmin = true,
                Run = (log, ct) => Exec("Dism.exe", "/Online /Cleanup-Image /ScanHealth", log, null, ct)
            },
            new()
            {
                Title = "DISM – RestoreHealth (repair image)",
                Description = "Repairs the Windows image using Windows Update as the source. Requires an internet connection and can take a long time.",
                RequiresAdmin = true, ChangesSystem = true,
                ConfirmText = "DISM will download and replace damaged components of the Windows image. Do not shut down the PC meanwhile.",
                Run = (log, ct) => Exec("Dism.exe", "/Online /Cleanup-Image /RestoreHealth", log, null, ct)
            },
            new()
            {
                Title = "Online disk scan (read-only)",
                Description = $"chkdsk {Drive} /scan checks the file system while Windows keeps running. It does not fix errors.",
                RequiresAdmin = true,
                Run = (log, ct) => Exec("chkdsk.exe", $"{Drive} /scan", log, null, ct)
            },
            new()
            {
                Title = "Create a restore point",
                Description = "Creates a System Restore point so you can roll back Windows settings and drivers if something goes wrong.",
                RequiresAdmin = true, ChangesSystem = true,
                ConfirmText = "A restore point named 'BWU restore point' will be created. Windows allows only one automatic restore point every 24 hours and System Protection must be enabled.",
                Run = CreateRestorePointAsync
            },
            new()
            {
                Title = "Basic diagnostics",
                Description = "Reports uptime, memory load, free disk space, pending restarts, disk health and Defender status. Makes no changes.",
                Run = DiagnosticsAsync
            },
        };
    }

    private static async Task<int> CreateRestorePointAsync(Action<string> log, CancellationToken ct)
    {
        const string script = """
            try {
              Checkpoint-Computer -Description 'BWU restore point' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop
            } catch { Write-Output ('ERROR: ' + $_.Exception.Message); exit 1 }
            $rp = Get-ComputerRestorePoint | Sort-Object SequenceNumber | Select-Object -Last 1
            if ($rp -and $rp.Description -eq 'BWU restore point') {
              $t = [Management.ManagementDateTimeConverter]::ToDateTime($rp.CreationTime)
              if (((Get-Date) - $t).TotalMinutes -lt 5) { Write-Output ('VERIFIED: restore point created at ' + $t); exit 0 }
            }
            Write-Output 'FAILED: Windows did not create a new restore point. System Protection may be turned off for the system drive, or one was already created in the last 24 hours.'
            exit 2
            """;
        log("Creating restore point...");
        var r = await ProcessRunner.RunPowerShellAsync(script, log, ct);
        if (!r.Started) { log("Could not start PowerShell: " + r.Error); return -1; }
        log(r.ExitCode == 0 ? "Result: success (verified)." : "Result: FAILED.");
        return r.ExitCode;
    }

    private static async Task<int> DiagnosticsAsync(Action<string> log, CancellationToken ct)
    {
        log("=== Basic diagnostics ===");
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        log($"Uptime: {(int)uptime.TotalDays} d {uptime.Hours} h {uptime.Minutes} min (Fast Startup can make this longer than the last full restart).");

        var mem = new NativeMethods.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>() };
        if (NativeMethods.GlobalMemoryStatusEx(ref mem))
            log($"Memory: {mem.dwMemoryLoad}% in use ({FormatHelper.Bytes((long)(mem.ullTotalPhys - mem.ullAvailPhys))} of {FormatHelper.Bytes((long)mem.ullTotalPhys)}).");

        try
        {
            var d = new DriveInfo(WindowsService.SystemDriveRoot);
            var pct = 100.0 * d.AvailableFreeSpace / d.TotalSize;
            log($"System drive {d.Name.TrimEnd('\\')}: {FormatHelper.Bytes(d.AvailableFreeSpace)} free of {FormatHelper.Bytes(d.TotalSize)} ({pct:0}% free)." +
                (pct < 10 ? "  WARNING: low free space." : ""));
        }
        catch (Exception ex) { log("Disk space: unavailable (" + ex.Message + ")"); }

        var pending = new List<string>();
        using (var bk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
        {
            if (bk.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") != null) pending.Add("component servicing");
            if (bk.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") != null) pending.Add("Windows Update");
            using var sm = bk.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            if (sm?.GetValue("PendingFileRenameOperations") != null) pending.Add("pending file operations");
        }
        log(pending.Count == 0 ? "Pending restart: none detected." : "Pending restart detected: " + string.Join(", ", pending) + ".");

        log("--- Physical disk health ---");
        await ProcessRunner.RunPowerShellAsync(
            "Get-PhysicalDisk | ForEach-Object { '{0}: health={1}, status={2}' -f $_.FriendlyName,$_.HealthStatus,$_.OperationalStatus }", log, ct);

        log("--- Microsoft Defender ---");
        await ProcessRunner.RunPowerShellAsync(
            "try { $s=Get-MpComputerStatus -ErrorAction Stop; 'Real-time protection: ' + $s.RealTimeProtectionEnabled; 'Signatures updated: ' + $s.AntivirusSignatureLastUpdated } catch { 'Defender status unavailable (another antivirus may be active).' }",
            log, ct);

        log("=== End of diagnostics ===");
        return 0;
    }
}
