using System.Runtime.InteropServices;
using BWU.Models;
using BWU.Utilities;
using Microsoft.Win32;

namespace BWU.Services;

public sealed class SystemInfoService
{
    private const string Unavailable = "Unavailable";

    public Task<IReadOnlyList<InfoItem>> GetDashboardAsync() => Task.Run<IReadOnlyList<InfoItem>>(() =>
    {
        var items = new List<InfoItem>
        {
            new("Windows edition", WindowsService.ProductName),
            new("Windows version", WindowsService.DisplayVersion ?? Unavailable),
            new("Build number", WindowsService.FullBuild),
            new("Processor", GetCpuName() ?? Unavailable),
            new("Installed RAM", GetInstalledRam() ?? Unavailable),
            new("Architecture", RuntimeInformation.OSArchitecture.ToString())
        };

        try
        {
            var drive = new DriveInfo(WindowsService.SystemDriveRoot);
            items.Add(new($"Storage capacity ({drive.Name.TrimEnd('\\')})", FormatHelper.Bytes(drive.TotalSize)));
            items.Add(new($"Available disk space ({drive.Name.TrimEnd('\\')})", FormatHelper.Bytes(drive.AvailableFreeSpace)));
        }
        catch
        {
            items.Add(new("Storage capacity", Unavailable));
            items.Add(new("Available disk space", Unavailable));
        }

        items.Add(new("BWU version", AppInfo.Version));
        return items;
    });

    public Task<IReadOnlyList<InfoSection>> GetSectionsAsync() => Task.Run<IReadOnlyList<InfoSection>>(() => new List<InfoSection>
    {
        WindowsSection(), ProcessorSection(), MemorySection(), StorageSection(), BiosSection(), ComputerSection()
    });

    private static string? GetCpuName()
        => Wmi.Query("SELECT Name FROM Win32_Processor").FirstOrDefault()?.Str("Name");

    private static string? GetInstalledRam()
    {
        if (NativeMethods.GetPhysicallyInstalledSystemMemory(out var kb) && kb > 0)
            return FormatHelper.Bytes(kb * 1024);
        var total = Wmi.Query("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem").FirstOrDefault()?.Num("TotalPhysicalMemory");
        return total.HasValue ? FormatHelper.Bytes((long)total.Value) : null;
    }

    private static InfoSection WindowsSection()
    {
        var boot = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount64);
        var items = new List<InfoItem>
        {
            new("Product", WindowsService.ProductName),
            new("Edition ID", WindowsService.EditionId ?? Unavailable),
            new("Version", WindowsService.DisplayVersion ?? Unavailable),
            new("Build", WindowsService.FullBuild),
            new("Installed on", WindowsService.InstallDate?.ToString("g") ?? Unavailable),
            new("Windows folder", WindowsService.WindowsDirectory),
            new("Computer name", Environment.MachineName),
            new("Last boot (approx.)", boot.ToString("g")),
            new("Architecture", RuntimeInformation.OSArchitecture.ToString()),
        };
        return new InfoSection("Windows installation", items);
    }

    private static InfoSection ProcessorSection()
    {
        var items = new List<InfoItem>();
        var cpu = Wmi.Query("SELECT * FROM Win32_Processor").FirstOrDefault();
        if (cpu == null) return new InfoSection("Processor", new[] { new InfoItem("Processor", Unavailable) });
        items.Add(new("Name", cpu.Str("Name") ?? Unavailable));
        items.Add(new("Manufacturer", cpu.Str("Manufacturer") ?? Unavailable));
        items.Add(new("Cores", cpu.Num("NumberOfCores")?.ToString() ?? Unavailable));
        items.Add(new("Logical processors", cpu.Num("NumberOfLogicalProcessors")?.ToString() ?? Unavailable));
        items.Add(new("Maximum clock", cpu.Num("MaxClockSpeed") is { } mhz ? $"{mhz / 1000.0:0.00} GHz" : Unavailable));
        if (cpu.Num("L3CacheSize") is { } l3 && l3 > 0) items.Add(new("L3 cache", FormatHelper.Bytes((long)l3 * 1024)));
        return new InfoSection("Processor", items);
    }

    private static InfoSection MemorySection()
    {
        var items = new List<InfoItem> { new("Installed (physical)", GetInstalledRam() ?? Unavailable) };
        var status = new NativeMethods.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>() };
        if (NativeMethods.GlobalMemoryStatusEx(ref status))
        {
            items.Add(new("Usable by Windows", FormatHelper.Bytes((long)status.ullTotalPhys)));
            items.Add(new("Available now", FormatHelper.Bytes((long)status.ullAvailPhys)));
            items.Add(new("Memory load", status.dwMemoryLoad + " %"));
        }
        foreach (var m in Wmi.Query("SELECT * FROM Win32_PhysicalMemory"))
        {
            var cap = m.Num("Capacity");
            var speed = m.Num("ConfiguredClockSpeed") ?? m.Num("Speed");
            items.Add(new(m.Str("DeviceLocator") ?? "Module",
                $"{(cap.HasValue ? FormatHelper.Bytes((long)cap.Value) : "?")}" +
                $"{(speed is > 0 ? $" • {speed} MT/s" : "")}" +
                $"{(m.Str("Manufacturer") is { Length: > 0 } mf && mf != "Unknown" ? " • " + mf : "")}"));
        }
        return new InfoSection("Memory", items);
    }

    private static InfoSection StorageSection()
    {
        var items = new List<InfoItem>();
        foreach (var d in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            try
            {
                items.Add(new($"Volume {d.Name.TrimEnd('\\')}",
                    $"{d.DriveFormat} • {FormatHelper.Bytes(d.TotalSize)} total • {FormatHelper.Bytes(d.AvailableFreeSpace)} free" +
                    (string.IsNullOrWhiteSpace(d.VolumeLabel) ? "" : $" • {d.VolumeLabel}")));
            }
            catch { /* volume vanished while reading */ }
        }

        var physical = Wmi.Query("SELECT * FROM MSFT_PhysicalDisk", @"root\Microsoft\Windows\Storage");
        if (physical.Count > 0)
        {
            foreach (var p in physical)
            {
                var media = p.Num("MediaType") switch { 3 => "HDD", 4 => "SSD", 5 => "SCM", _ => "Unspecified type" };
                var bus = p.Num("BusType") switch { 17 => "NVMe", 11 => "SATA", 7 => "USB", 8 => "RAID", 3 => "ATA", 1 => "SCSI", _ => "Other bus" };
                var health = p.Num("HealthStatus") switch { 0 => "Healthy", 1 => "Warning", 2 => "Unhealthy", _ => "Health unknown" };
                var size = p.Num("Size");
                items.Add(new("Disk: " + (p.Str("FriendlyName") ?? "Unknown"),
                    $"{(size.HasValue ? FormatHelper.Bytes((long)size.Value) : "?")} • {media} • {bus} • {health}"));
            }
        }
        else
        {
            foreach (var p in Wmi.Query("SELECT * FROM Win32_DiskDrive"))
            {
                var size = p.Num("Size");
                items.Add(new("Disk: " + (p.Str("Model") ?? "Unknown"),
                    $"{(size.HasValue ? FormatHelper.Bytes((long)size.Value) : "?")} • {p.Str("InterfaceType") ?? "?"}"));
            }
        }

        if (items.Count == 0) items.Add(new("Storage", Unavailable));
        return new InfoSection("Storage devices", items);
    }

    private static InfoSection BiosSection()
    {
        var bios = Wmi.Query("SELECT * FROM Win32_BIOS").FirstOrDefault();
        var items = new List<InfoItem>();
        if (bios == null) items.Add(new("BIOS", Unavailable));
        else
        {
            items.Add(new("Vendor", bios.Str("Manufacturer") ?? Unavailable));
            items.Add(new("Version", bios.Str("SMBIOSBIOSVersion") ?? Unavailable));
            items.Add(new("Release date", bios.Date("ReleaseDate")?.ToString("d") ?? Unavailable));
            if (bios.Num("SMBIOSMajorVersion") is { } maj && bios.Num("SMBIOSMinorVersion") is { } min)
                items.Add(new("SMBIOS version", $"{maj}.{min}"));
        }

        uint fw = 0;
        string mode = Unavailable;
        try { if (NativeMethods.GetFirmwareType(ref fw)) mode = fw switch { 1 => "Legacy BIOS", 2 => "UEFI", _ => Unavailable }; } catch { }
        items.Add(new("Firmware mode", mode));

        try
        {
            using var bk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var k = bk.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            items.Add(new("Secure Boot", k?.GetValue("UEFISecureBootEnabled") is int sb ? (sb == 1 ? "Enabled" : "Disabled") : Unavailable));
        }
        catch { items.Add(new("Secure Boot", Unavailable)); }

        return new InfoSection("BIOS / firmware", items);
    }

    private static InfoSection ComputerSection()
    {
        var cs = Wmi.Query("SELECT * FROM Win32_ComputerSystem").FirstOrDefault();
        var items = new List<InfoItem>();
        if (cs == null) items.Add(new("Computer", Unavailable));
        else
        {
            items.Add(new("Manufacturer", cs.Str("Manufacturer") ?? Unavailable));
            items.Add(new("Model", cs.Str("Model") ?? Unavailable));
            if (!string.IsNullOrWhiteSpace(cs.Str("SystemFamily"))) items.Add(new("Family", cs.Str("SystemFamily")!));
            items.Add(new("System type", cs.Str("SystemType") ?? Unavailable));
        }
        return new InfoSection("Computer", items);
    }
}
