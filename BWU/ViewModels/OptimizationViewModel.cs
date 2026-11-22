using System.Collections.ObjectModel;
using System.Windows.Input;
using BWU.Models;
using BWU.Services;
using BWU.Utilities;

namespace BWU.ViewModels;

public sealed class ProfileItemViewModel
{
    public OptimizationProfile Profile { get; }
    public ICommand ApplyCommand { get; }
    public ICommand RevertCommand { get; }
    public string Name => Profile.Name;
    public string Description => Profile.Description;

    public ProfileItemViewModel(OptimizationProfile profile, Func<OptimizationProfile, Task> apply, Func<OptimizationProfile, Task> revert)
    {
        Profile = profile;
        ApplyCommand = new AsyncRelayCommand(() => apply(profile));
        RevertCommand = new AsyncRelayCommand(() => revert(profile));
    }
}

public sealed class StartupItemViewModel : ObservableObject
{
    private readonly OptimizationService _svc;
    private string _result = "";
    public StartupEntry Entry { get; }
    public ICommand ToggleCommand { get; }

    public StartupItemViewModel(StartupEntry entry, OptimizationService svc)
    {
        Entry = entry; _svc = svc;
        ToggleCommand = new AsyncRelayCommand(ToggleAsync);
    }

    public string Name => Entry.Name;
    public string Command => Entry.Command;
    public string Location => Entry.Location + (Entry.IsMachine ? " (all users)" : " (current user)");
    public string? Hint => Entry.Hint;
    public string StateText => Entry.IsEnabled ? "Enabled" : "Disabled";
    public string ToggleText => Entry.IsEnabled ? "Disable" : "Enable";
    public string Result { get => _result; private set => SetProperty(ref _result, value); }

    private async Task ToggleAsync()
    {
        var enable = !Entry.IsEnabled;
        if (!enable)
        {
            if (!Dialogs.Confirm("Disable startup entry",
                $"'{Entry.Name}' will no longer start when you sign in.\n\nThe entry is not deleted; you can enable it again here at any time. Continue?")) return;
        }
        if (Entry.IsMachine && !AdminHelper.EnsureAdmin("This entry applies to all users, so changing it needs administrator rights.")) return;

        var r = await Task.Run(() => _svc.SetStartupEnabled(Entry, enable));
        Result = (r.Success ? "✔ " : "✖ ") + r.Message;
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(ToggleText));
    }
}

public sealed class ServiceItemViewModel : ObservableObject
{
    private readonly OptimizationService _svc;
    private readonly Action _refresh;
    private string _result = "";
    public ServiceInfo Info { get; }
    public ICommand ManualCommand { get; }
    public ICommand RestoreCommand { get; }

    public ServiceItemViewModel(ServiceInfo info, OptimizationService svc, Action refresh)
    {
        Info = info; _svc = svc; _refresh = refresh;
        ManualCommand = new AsyncRelayCommand(ManualAsync, () => Info.IsAutomatic);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync, () => Info.HasBackup);
    }

    public string Title => Info.Title;
    public string Explanation => Info.Explanation;
    public string StateText => $"Startup: {Info.StartType} • Status: {Info.Status}";
    public string Result { get => _result; private set => SetProperty(ref _result, value); }

    private async Task ManualAsync()
    {
        if (!Dialogs.Confirm("Set service to Manual",
            $"{Info.Title}\n\n{Info.Explanation}\n\nThe service will be changed from Automatic to Manual (it is not disabled and can still start when needed). " +
            "The original startup type is saved so you can restore it. Continue?")) return;
        if (!AdminHelper.EnsureAdmin("Changing a service's startup type needs administrator rights.")) return;

        var r = await _svc.SetServiceManualAsync(Info.Name);
        Result = (r.Success ? "✔ " : "✖ ") + r.Message;
        _refresh();
    }

    private async Task RestoreAsync()
    {
        if (!AdminHelper.EnsureAdmin("Restoring a service's startup type needs administrator rights.")) return;
        var r = await _svc.RestoreServiceAsync(Info.Name);
        Result = (r.Success ? "✔ " : "✖ ") + r.Message;
        _refresh();
    }
}

public sealed class OptimizationViewModel : ObservableObject
{
    private readonly OptimizationService _opt;
    private readonly TweakService _tweaks;
    private string _profileMessage = "";

    public ObservableCollection<TweakItemViewModel> Tweaks { get; } = new();
    public ObservableCollection<ProfileItemViewModel> Profiles { get; } = new();
    public ObservableCollection<StartupItemViewModel> StartupItems { get; } = new();
    public ObservableCollection<ServiceItemViewModel> Services { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand OpenMoreSpacesCommand { get; }
    public string ProfileMessage { get => _profileMessage; private set => SetProperty(ref _profileMessage, value); }

    public OptimizationViewModel(OptimizationService opt, TweakService tweaks, Action<string> navigate)
    {
        _opt = opt; _tweaks = tweaks;
        foreach (var t in TweakCatalog.Performance) Tweaks.Add(new TweakItemViewModel(t, tweaks));
        foreach (var p in TweakCatalog.Profiles) Profiles.Add(new ProfileItemViewModel(p, ApplyProfileAsync, RevertProfileAsync));
        RefreshCommand = new RelayCommand(Load);
        OpenMoreSpacesCommand = new RelayCommand(() => navigate("morespaces"));
        Load();
    }

    private void Load()
    {
        StartupItems.Clear();
        foreach (var e in _opt.GetStartupEntries()) StartupItems.Add(new StartupItemViewModel(e, _opt));
        Services.Clear();
        foreach (var s in _opt.GetServices()) Services.Add(new ServiceItemViewModel(s, _opt, Load));
        foreach (var t in Tweaks) t.Refresh();
    }

    private async Task ApplyProfileAsync(OptimizationProfile p)
    {
        var items = Tweaks.Where(t => p.TweakIds.Contains(t.Tweak.Id) && t.IsSupported && !t.IsApplied).ToList();
        if (items.Count == 0) { ProfileMessage = $"{p.Name}: every setting in this profile is already applied."; return; }

        var msg = p.Description + "\n\nThis profile will apply:\n" +
                  string.Join("\n", items.Select(i => $"• {i.Title} — {i.Consequence}")) +
                  "\n\nCurrent values are backed up first; you can revert the profile at any time. Continue?";
        if (!Dialogs.Confirm("Apply profile: " + p.Name, msg)) return;

        int ok = 0; var errors = new List<string>();
        foreach (var i in items)
        {
            var r = await Task.Run(() => _tweaks.Apply(i.Tweak));
            if (r.Success) ok++; else errors.Add($"{i.Title}: {r.Message}");
            i.Refresh();
        }
        ProfileMessage = errors.Count == 0 ? $"{p.Name}: {ok} setting(s) applied." : $"{p.Name}: {ok} applied, {errors.Count} failed — {string.Join(" | ", errors)}";
    }

    private async Task RevertProfileAsync(OptimizationProfile p)
    {
        var items = Tweaks.Where(t => p.TweakIds.Contains(t.Tweak.Id) && t.HasBackup).ToList();
        if (items.Count == 0) { ProfileMessage = $"{p.Name}: there is nothing to revert (no BWU backups for these settings)."; return; }
        if (!Dialogs.Confirm("Revert profile: " + p.Name, $"{items.Count} setting(s) will be restored to the values saved before BWU changed them. Continue?")) return;

        int ok = 0; var errors = new List<string>();
        foreach (var i in items)
        {
            var r = await Task.Run(() => _tweaks.Revert(i.Tweak));
            if (r.Success) ok++; else errors.Add($"{i.Title}: {r.Message}");
            i.Refresh();
        }
        ProfileMessage = errors.Count == 0 ? $"{p.Name}: {ok} setting(s) reverted." : $"{p.Name}: {ok} reverted, {errors.Count} failed — {string.Join(" | ", errors)}";
    }
}
