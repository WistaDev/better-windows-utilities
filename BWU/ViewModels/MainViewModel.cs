using System.Collections.ObjectModel;
using System.Windows.Input;
using BWU.Services;
using BWU.Services.MoreSpaces;
using BWU.Utilities;

namespace BWU.ViewModels;

public sealed class NavItem : ObservableObject
{
    private bool _isSelected;
    private readonly string _key;
    public NavItem(string key, string glyph, Func<ObservableObject> factory) { _key = key; Glyph = glyph; Factory = factory; }

    public string Key => _key;
    public string Glyph { get; }
    public Func<ObservableObject> Factory { get; }
    public ObservableObject? Instance { get; set; }
    public string Title => Loc.T(_key);
    public string Subtitle => Loc.T(_key + ".sub");
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public void RefreshText() { OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(Subtitle)); }
}

public sealed class MainViewModel : ObservableObject
{
    private NavItem? _current;
    private ObservableObject? _currentViewModel;
    private bool _showLabels = true;

    public ObservableCollection<NavItem> NavItems { get; } = new();
    public ICommand NavigateCommand { get; }

    public MainViewModel()
    {
        // Services are created once and shared. System operations live in Services, never in the views.
        var backups = new BackupStore();
        var registry = new RegistryService(backups);
        var tweaks = new TweakService(registry);
        var systemInfo = new SystemInfoService();
        var optimization = new OptimizationService(backups);

        NavItems.Add(new("dashboard", "\uE80F", () => new DashboardViewModel(systemInfo, NavigateTo)));
        NavItems.Add(new("sysinfo", "\uE946", () => new SystemInfoViewModel(systemInfo)));
        NavItems.Add(new("optimization", "\uE945", () => new OptimizationViewModel(optimization, tweaks, NavigateTo)));
        NavItems.Add(new("debloater", "\uE74D", () => new DebloaterViewModel(new DebloaterService())));
        NavItems.Add(new("customization", "\uE790", () => new CustomizationViewModel(new CustomizationService(registry), tweaks)));
        NavItems.Add(new("packages", "\uE7B8", () => new PackageManagerViewModel(new PackageService())));
        NavItems.Add(new("maintenance", "\uE90F", () => new MaintenanceViewModel(new MaintenanceService())));
        NavItems.Add(new("morespaces", "\uEDA2", () => new MoreSpacesViewModel(new CompactOSService(), new WindowsTempService(),
            new UserTempService(), new PrefetchService(), new StorageAnalysisService(), new ComponentStoreService())));
        NavItems.Add(new("privacy", "\uE72E", () => new PrivacyViewModel(tweaks)));
        NavItems.Add(new("settings", "\uE713", () => new SettingsViewModel(new UpdateService())));

        NavigateCommand = new RelayCommand(p => { if (p is NavItem n) Select(n); });
        Loc.Changed += () => { foreach (var n in NavItems) n.RefreshText(); OnPropertyChanged(nameof(CurrentTitle)); OnPropertyChanged(nameof(CurrentSubtitle)); };
        Select(NavItems[0]);
    }

    public bool ShowLabels { get => _showLabels; set { if (SetProperty(ref _showLabels, value)) OnPropertyChanged(nameof(SidebarWidth)); } }
    public double SidebarWidth => ShowLabels ? 248 : 68;
    public string CurrentTitle => _current?.Title ?? "";
    public string CurrentSubtitle => _current?.Subtitle ?? "";
    public ObservableObject? CurrentViewModel { get => _currentViewModel; private set => SetProperty(ref _currentViewModel, value); }

    public void NavigateTo(string key)
    {
        var item = NavItems.FirstOrDefault(n => n.Key == key);
        if (item != null) Select(item);
    }

    private void Select(NavItem item)
    {
        if (_current != null) _current.IsSelected = false;
        _current = item;
        item.IsSelected = true;
        item.Instance ??= item.Factory();      // pages are created on first visit and keep their state afterwards
        CurrentViewModel = item.Instance;
        OnPropertyChanged(nameof(CurrentTitle));
        OnPropertyChanged(nameof(CurrentSubtitle));
    }
}
