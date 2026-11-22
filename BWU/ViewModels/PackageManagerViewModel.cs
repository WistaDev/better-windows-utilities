using System.Collections.ObjectModel;
using System.Windows.Input;
using BWU.Models;
using BWU.Services;
using BWU.Utilities;

namespace BWU.ViewModels;

public sealed class PackageManagerViewModel : ObservableObject
{
    private readonly PackageService _svc;
    private CancellationTokenSource? _cts;
    private bool _wingetInstalled, _busy, _indeterminate, _showInstalled;
    private string _wingetStatus = "Checking for WinGet…";
    private string _query = "";
    private double _progress;
    private PackageItem? _selected;

    public ObservableCollection<PackageItem> Items { get; } = new();
    public LogBuffer Log { get; } = new();

    public ICommand SearchCommand { get; }
    public ICommand ShowInstalledCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand RepairCommand { get; }
    public ICommand OpenStoreCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RecheckCommand { get; }

    public PackageManagerViewModel(PackageService svc)
    {
        _svc = svc;
        SearchCommand = new AsyncRelayCommand(SearchAsync, () => !IsBusy && WingetInstalled && Query.Trim().Length > 0);
        ShowInstalledCommand = new AsyncRelayCommand(LoadInstalledAsync, () => !IsBusy && WingetInstalled);
        InstallCommand = new AsyncRelayCommand(InstallAsync, () => !IsBusy && WingetInstalled && Selected != null && !ShowInstalled);
        UninstallCommand = new AsyncRelayCommand(UninstallAsync, () => !IsBusy && WingetInstalled && Selected != null && ShowInstalled);
        RepairCommand = new AsyncRelayCommand(RepairAsync, () => !IsBusy);
        OpenStoreCommand = new RelayCommand(() => { try { PackageService.OpenStorePage(); } catch (Exception ex) { Dialogs.Error("Could not open the Store", ex.Message); } });
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        RecheckCommand = new AsyncRelayCommand(DetectAsync, () => !IsBusy);
        _ = DetectAsync();
    }

    public bool WingetInstalled { get => _wingetInstalled; private set { if (SetProperty(ref _wingetInstalled, value)) CommandManager.InvalidateRequerySuggested(); } }
    public string WingetStatus { get => _wingetStatus; private set => SetProperty(ref _wingetStatus, value); }
    public bool IsBusy { get => _busy; private set { if (SetProperty(ref _busy, value)) CommandManager.InvalidateRequerySuggested(); } }
    public bool IsIndeterminate { get => _indeterminate; private set => SetProperty(ref _indeterminate, value); }
    public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    public string Query { get => _query; set { if (SetProperty(ref _query, value)) CommandManager.InvalidateRequerySuggested(); } }
    public PackageItem? Selected { get => _selected; set { if (SetProperty(ref _selected, value)) CommandManager.InvalidateRequerySuggested(); } }
    public bool ShowInstalled { get => _showInstalled; private set { if (SetProperty(ref _showInstalled, value)) { OnPropertyChanged(nameof(ListTitle)); CommandManager.InvalidateRequerySuggested(); } } }
    public string ListTitle => ShowInstalled ? "Installed packages" : "Search results";

    private async Task DetectAsync()
    {
        var (installed, version) = await _svc.DetectAsync();
        WingetInstalled = installed;
        WingetStatus = installed ? $"Windows Package Manager is installed (version {version})."
                                 : "Windows Package Manager (winget) was not found. Use Repair / install below.";
    }

    private CancellationToken Begin(bool indeterminate = true)
    {
        _cts = new CancellationTokenSource();
        IsBusy = true; IsIndeterminate = indeterminate; Progress = 0;
        return _cts.Token;
    }

    private void End() { IsBusy = false; IsIndeterminate = false; _cts?.Dispose(); _cts = null; }

    private async Task SearchAsync()
    {
        var ct = Begin();
        try
        {
            Log.Append($"> winget search \"{Query.Trim()}\"");
            var found = await _svc.SearchAsync(Query.Trim(), Log.Append, ct);
            ShowInstalled = false;
            Items.Clear();
            foreach (var p in found) Items.Add(p);
            Log.Append(found.Count == 0 ? "No packages found." : $"{found.Count} package(s) found.");
        }
        finally { End(); }
    }

    private async Task LoadInstalledAsync()
    {
        var ct = Begin();
        try
        {
            Log.Append("> winget list");
            var found = await _svc.ListInstalledAsync(Log.Append, ct);
            ShowInstalled = true;
            Items.Clear();
            foreach (var p in found) Items.Add(p);
            Log.Append($"{found.Count} installed package(s) listed.");
        }
        finally { End(); }
    }

    private void OnLine(string line)
    {
        if (ProcessRunner.IsNoise(line))
        {
            // Progress redraw: use it for the progress bar, keep it out of the log.
            var pct = ProcessRunner.TryParsePercent(line);
            if (pct.HasValue) UiPost(() => { IsIndeterminate = false; Progress = pct.Value; });
            return;
        }
        Log.Append(line.Trim());
    }

    private static void UiPost(Action a) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(a);

    private async Task InstallAsync()
    {
        var pkg = Selected!;
        if (!Dialogs.Confirm("Install package",
            $"Install '{pkg.Name}' ({pkg.Id})?\n\nThe package is downloaded through Windows Package Manager from its official sources. Windows may ask for administrator permission.")) return;
        var ct = Begin();
        try
        {
            Log.Append($"> winget install --id {pkg.Id}");
            var r = await _svc.InstallAsync(pkg.Id, OnLine, ct);
            Log.Append(Describe(r, "Installation"));
        }
        finally { End(); }
    }

    private async Task UninstallAsync()
    {
        var pkg = Selected!;
        if (!Dialogs.Confirm("Uninstall package", $"Uninstall '{pkg.Name}' ({pkg.Id})? This removes the program from this PC.")) return;
        var ct = Begin();
        try
        {
            Log.Append($"> winget uninstall --id {pkg.Id}");
            var r = await _svc.UninstallAsync(pkg.Id, OnLine, ct);
            Log.Append(Describe(r, "Uninstall"));
            if (r.ExitCode == 0) Items.Remove(pkg);
        }
        finally { End(); }
    }

    private async Task RepairAsync()
    {
        if (!Dialogs.Confirm("Install / repair WinGet",
            "BWU will use Microsoft's official 'Microsoft.WinGet.Client' PowerShell module (from the PowerShell Gallery) to install or repair Windows Package Manager.\n\n" +
            "If you prefer, cancel and use 'Open Store page' to get App Installer from the Microsoft Store. Continue?")) return;
        if (!AdminHelper.EnsureAdmin("Repairing WinGet for all users needs administrator rights.")) return;
        var ct = Begin();
        try
        {
            var r = await _svc.RepairAsync(Log.Append, ct);
            Log.Append(Describe(r, "Repair"));
        }
        finally { End(); }
        await DetectAsync();
    }

    private static string Describe(ProcessResult r, string what)
    {
        if (!r.Started) return $"{what} could not start: {r.Error}";
        if (r.Error == "Cancelled") return $"{what} was cancelled.";
        return r.ExitCode == 0 ? $"{what} finished successfully." : $"{what} FAILED (exit code {r.ExitCode}). See the output above.";
    }
}
