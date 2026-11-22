using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using BWU.Models;
using BWU.Services;
using BWU.Utilities;

namespace BWU.ViewModels;

public sealed class CustomizationViewModel : ObservableObject
{
    private readonly CustomizationService _svc;
    private string _owner = "", _org = "", _manufacturer = "", _model = "", _phone = "", _hours = "", _url = "", _logoPath = "";
    private string _winverResult = "", _oemResult = "";
    private BitmapSource? _logoPreview;

    public ObservableCollection<TweakItemViewModel> Tweaks { get; } = new();
    public IReadOnlyList<(string Title, string Uri)> Shortcuts => TweakCatalog.PersonalizationShortcuts;

    public ICommand SaveWinverCommand { get; }
    public ICommand RestoreWinverCommand { get; }
    public ICommand OpenWinverCommand { get; }
    public ICommand SaveOemCommand { get; }
    public ICommand RestoreOemCommand { get; }
    public ICommand PickLogoCommand { get; }
    public ICommand OpenSystemPropertiesCommand { get; }
    public ICommand OpenUriCommand { get; }

    public CustomizationViewModel(CustomizationService svc, TweakService tweaks)
    {
        _svc = svc;
        foreach (var t in TweakCatalog.Personalization) Tweaks.Add(new TweakItemViewModel(t, tweaks));

        SaveWinverCommand = new RelayCommand(SaveWinver);
        RestoreWinverCommand = new RelayCommand(() => { WinverResult = Format(_svc.RestoreWinver()); LoadValues(); });
        OpenWinverCommand = new RelayCommand(() => CustomizationService.OpenUri("winver.exe"));
        SaveOemCommand = new RelayCommand(SaveOem);
        RestoreOemCommand = new RelayCommand(() => { OemResult = Format(_svc.RestoreOem()); LoadValues(); });
        PickLogoCommand = new RelayCommand(PickLogo);
        OpenSystemPropertiesCommand = new RelayCommand(() => CustomizationService.OpenUri("sysdm.cpl"));
        OpenUriCommand = new RelayCommand(p => { if (p is string uri) CustomizationService.OpenUri(uri); });
        LoadValues();
    }

    private static string Format(OperationResult r) => (r.Success ? "✔ " : "✖ ") + r.Message;

    public string Owner { get => _owner; set => SetProperty(ref _owner, value); }
    public string Organization { get => _org; set => SetProperty(ref _org, value); }
    public string Manufacturer { get => _manufacturer; set => SetProperty(ref _manufacturer, value); }
    public string Model { get => _model; set => SetProperty(ref _model, value); }
    public string SupportPhone { get => _phone; set => SetProperty(ref _phone, value); }
    public string SupportHours { get => _hours; set => SetProperty(ref _hours, value); }
    public string SupportUrl { get => _url; set => SetProperty(ref _url, value); }
    public string LogoPath { get => _logoPath; set => SetProperty(ref _logoPath, value); }
    public string WinverResult { get => _winverResult; private set => SetProperty(ref _winverResult, value); }
    public string OemResult { get => _oemResult; private set => SetProperty(ref _oemResult, value); }
    public BitmapSource? LogoPreview { get => _logoPreview; private set => SetProperty(ref _logoPreview, value); }
    public string AdminNote => AdminHelper.IsAdministrator
        ? "Running as administrator: the fields below can be saved."
        : "Saving winver and OEM information needs administrator rights. BWU will offer to restart elevated when you press Save.";

    private void LoadValues()
    {
        var w = _svc.ReadWinver();
        Owner = w.RegisteredOwner; Organization = w.RegisteredOrganization;
        var o = _svc.ReadOem();
        Manufacturer = o.Manufacturer; Model = o.Model; SupportPhone = o.SupportPhone;
        SupportHours = o.SupportHours; SupportUrl = o.SupportUrl; LogoPath = o.LogoPath;
        LoadPreview(LogoPath);
    }

    private void LoadPreview(string path)
    {
        try
        {
            if (!File.Exists(path)) { LogoPreview = null; return; }
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            LogoPreview = bmp;
        }
        catch { LogoPreview = null; }
    }

    private void SaveWinver()
    {
        if (!Dialogs.Confirm("Save winver information",
            "BWU will change the 'registered owner' and 'organization' values that winver shows in its 'licensed to' lines.\n\n" +
            "BWU does not change the Windows version or build number shown by winver, because that can break Windows servicing and updates. " +
            "The original values are backed up first, and Windows updates may overwrite your changes.\n\nContinue?")) return;
        if (!AdminHelper.EnsureAdmin("Changing the licensing information in the registry needs administrator rights.")) return;
        WinverResult = Format(_svc.SaveWinver(new WinverInfo(Owner.Trim(), Organization.Trim())));
    }

    private void SaveOem()
    {
        if (!Dialogs.Confirm("Save OEM information",
            "BWU will write the manufacturer, model, support details and logo shown in Settings > System > About and in System Properties.\n\n" +
            "The original values are backed up first, and Windows updates may overwrite your changes.\n\nContinue?")) return;
        if (!AdminHelper.EnsureAdmin("Changing OEM information in the registry needs administrator rights.")) return;
        OemResult = Format(_svc.SaveOem(new OemInfo(Manufacturer.Trim(), Model.Trim(), SupportPhone.Trim(), SupportHours.Trim(), SupportUrl.Trim(), LogoPath.Trim())));
    }

    private void PickLogo()
    {
        var file = Dialogs.PickImage();
        if (file == null) return;
        var (result, path) = _svc.ConvertLogo(file);
        if (!result.Success || path == null) { OemResult = Format(result); return; }
        LogoPath = path;
        LoadPreview(path);
        OemResult = "Logo prepared (120x120 BMP). Press Save OEM information to apply it.";
    }
}
