using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BWU.Models;
using BWU.Utilities;
using Microsoft.Win32;

namespace BWU.Services;

public sealed record OemInfo(string Manufacturer, string Model, string SupportPhone, string SupportHours, string SupportUrl, string LogoPath);
public sealed record WinverInfo(string RegisteredOwner, string RegisteredOrganization);

public sealed class CustomizationService
{
    private const string NtKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private const string OemKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\OEMInformation";
    private const RegistryHive LM = RegistryHive.LocalMachine;

    private readonly RegistryService _reg;
    public CustomizationService(RegistryService reg) => _reg = reg;

    public static string LogoDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BWU");
    public static string LogoPath => Path.Combine(LogoDirectory, "oem-logo.bmp");

    private string Read(string path, string name) => _reg.Read(LM, path, name)?.ToString() ?? "";

    public WinverInfo ReadWinver() => new(Read(NtKey, "RegisteredOwner"), Read(NtKey, "RegisteredOrganization"));

    public OemInfo ReadOem() => new(Read(OemKey, "Manufacturer"), Read(OemKey, "Model"), Read(OemKey, "SupportPhone"),
        Read(OemKey, "SupportHours"), Read(OemKey, "SupportURL"), Read(OemKey, "Logo"));

    public OperationResult SaveWinver(WinverInfo info)
    {
        if (!AdminHelper.IsAdministrator) return OperationResult.Fail("Administrator rights are required.");
        try
        {
            _reg.SetWithBackup(LM, NtKey, "RegisteredOwner", info.RegisteredOwner, RegistryValueKind.String);
            _reg.SetWithBackup(LM, NtKey, "RegisteredOrganization", info.RegisteredOrganization, RegistryValueKind.String);
            return OperationResult.Ok("Saved. Open winver to see the 'licensed to' lines. Windows updates may overwrite these values.");
        }
        catch (Exception ex) { return OperationResult.Fail(ex.Message); }
    }

    public OperationResult RestoreWinver()
    {
        if (!AdminHelper.IsAdministrator) return OperationResult.Fail("Administrator rights are required.");
        try
        {
            var a = _reg.RestoreFromBackup(LM, NtKey, "RegisteredOwner");
            var b = _reg.RestoreFromBackup(LM, NtKey, "RegisteredOrganization");
            return a || b ? OperationResult.Ok("Original values restored.") : OperationResult.Fail("No BWU backup exists for these values.");
        }
        catch (Exception ex) { return OperationResult.Fail(ex.Message); }
    }

    public OperationResult SaveOem(OemInfo info)
    {
        if (!AdminHelper.IsAdministrator) return OperationResult.Fail("Administrator rights are required.");
        try
        {
            void Set(string name, string value) => _reg.SetWithBackup(LM, OemKey, name, value, RegistryValueKind.String);
            Set("Manufacturer", info.Manufacturer);
            Set("Model", info.Model);
            Set("SupportPhone", info.SupportPhone);
            Set("SupportHours", info.SupportHours);
            Set("SupportURL", info.SupportUrl);
            if (!string.IsNullOrWhiteSpace(info.LogoPath)) Set("Logo", info.LogoPath);
            return OperationResult.Ok("OEM information saved. It appears in Settings > System > About and in System Properties. Windows updates may overwrite it.");
        }
        catch (Exception ex) { return OperationResult.Fail(ex.Message); }
    }

    public OperationResult RestoreOem()
    {
        if (!AdminHelper.IsAdministrator) return OperationResult.Fail("Administrator rights are required.");
        try
        {
            var any = false;
            foreach (var n in new[] { "Manufacturer", "Model", "SupportPhone", "SupportHours", "SupportURL", "Logo" })
                any |= _reg.RestoreFromBackup(LM, OemKey, n);
            return any ? OperationResult.Ok("Original OEM values restored.") : OperationResult.Fail("No BWU backup exists for the OEM values.");
        }
        catch (Exception ex) { return OperationResult.Fail(ex.Message); }
    }

    /// <summary>Converts any image to the 120x120 BMP that Windows expects for the OEM logo. Must run on the UI thread.</summary>
    public (OperationResult Result, string? Path) ConvertLogo(string sourcePath)
    {
        try
        {
            var src = new BitmapImage();
            src.BeginInit();
            src.CacheOption = BitmapCacheOption.OnLoad;
            src.UriSource = new Uri(sourcePath);
            src.EndInit();
            src.Freeze();

            const int size = 120;
            var scale = Math.Min((double)size / src.PixelWidth, (double)size / src.PixelHeight);
            var w = src.PixelWidth * scale;
            var h = src.PixelHeight * scale;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, size, size));
                dc.DrawImage(src, new Rect((size - w) / 2, (size - h) / 2, w, h));
            }
            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            Directory.CreateDirectory(LogoDirectory);
            var encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(LogoPath)) encoder.Save(fs);
            return (OperationResult.Ok("Logo converted to 120x120 BMP."), LogoPath);
        }
        catch (Exception ex) { return (OperationResult.Fail("Could not convert the image: " + ex.Message), null); }
    }

    public static void OpenUri(string uri)
    {
        try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
        catch (Exception ex) { Dialogs.Error("Could not open", ex.Message); }
    }
}
