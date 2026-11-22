using System.Runtime.InteropServices;
using System.Windows.Input;
using BWU.Services;
using BWU.Utilities;

namespace BWU.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings = SettingsService.Instance;
    private readonly UpdateService _updates;
    private string _updateUrl;
    private string _updateStatus = "";
    private string? _downloadUrl;
    private bool _startWithWindows;

    public ICommand CheckUpdatesCommand { get; }
    public ICommand OpenDownloadCommand { get; }
    public ICommand RestartAdminCommand { get; }
    public ICommand OpenDataFolderCommand { get; }

    public SettingsViewModel(UpdateService updates)
    {
        _updates = updates;
        _updateUrl = _settings.Settings.UpdateManifestUrl;
        _startWithWindows = _settings.IsStartWithWindows;
        CheckUpdatesCommand = new AsyncRelayCommand(CheckAsync);
        OpenDownloadCommand = new RelayCommand(() => { if (_downloadUrl != null) CustomizationService.OpenUri(_downloadUrl); }, () => _downloadUrl != null);
        RestartAdminCommand = new RelayCommand(() => AdminHelper.RestartAsAdmin(), () => !AdminHelper.IsAdministrator);
        OpenDataFolderCommand = new RelayCommand(() => { Directory.CreateDirectory(SettingsService.DataDirectory); CustomizationService.OpenUri(SettingsService.DataDirectory); });
    }

    public bool IsDark
    {
        get => _settings.Settings.Theme != "Light";
        set { if (value) SetTheme("Dark"); }
    }
    public bool IsLight
    {
        get => _settings.Settings.Theme == "Light";
        set { if (value) SetTheme("Light"); }
    }
    private void SetTheme(string theme)
    {
        _settings.Settings.Theme = theme; _settings.Save(); ThemeService.Apply(theme);
        OnPropertyChanged(nameof(IsDark)); OnPropertyChanged(nameof(IsLight));
    }

    public bool IsEnglish { get => Loc.Language == "en"; set { if (value) SetLanguage("en"); } }
    public bool IsPortuguese { get => Loc.Language == "pt-BR"; set { if (value) SetLanguage("pt-BR"); } }
    private void SetLanguage(string lang)
    {
        _settings.Settings.Language = lang; _settings.Save(); Loc.SetLanguage(lang);
        OnPropertyChanged(nameof(IsEnglish)); OnPropertyChanged(nameof(IsPortuguese));
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (value == _startWithWindows) return;
            var r = _settings.SetStartWithWindows(value);
            if (!r.Success) Dialogs.Error("Startup behavior", r.Message);
            _startWithWindows = _settings.IsStartWithWindows;   // always reflect the real registry state
            OnPropertyChanged();
        }
    }

    public string UpdateUrl
    {
        get => _updateUrl;
        set { if (SetProperty(ref _updateUrl, value)) { _settings.Settings.UpdateManifestUrl = value.Trim(); _settings.Save(); } }
    }
    public string UpdateStatus { get => _updateStatus; private set => SetProperty(ref _updateStatus, value); }

    private async Task CheckAsync()
    {
        UpdateStatus = "Checking…";
        var r = await _updates.CheckAsync(UpdateUrl.Trim());
        _downloadUrl = r.UpdateAvailable ? r.DownloadUrl : null;
        UpdateStatus = r.Message;
        CommandManager.InvalidateRequerySuggested();
    }

    public bool IsAdmin => AdminHelper.IsAdministrator;
    public string AdminText => IsAdmin ? "Yes — BWU is running with administrator rights." : "No — BWU runs as a standard user and asks for elevation only when needed.";
    public string AppName => $"{AppInfo.Name} — {AppInfo.FullName}";
    public string Slogan => AppInfo.Slogan;
    public string Version => AppInfo.Version;
    public string Developer => AppInfo.Developer;
    public string Copyright => AppInfo.Copyright;
    public string Runtime => RuntimeInformation.FrameworkDescription;
    public string DataFolder => SettingsService.DataDirectory;
}
