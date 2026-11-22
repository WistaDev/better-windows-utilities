using System.Collections.ObjectModel;
using System.Windows.Input;
using BWU.Models;
using BWU.Services;
using BWU.Services.MoreSpaces;
using BWU.Utilities;

namespace BWU.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly SystemInfoService _svc;
    private string _status = "Reading system information…";
    private double _usedPercent;
    private string _storageSummary = "";

    public ObservableCollection<InfoItem> Items { get; } = new();
    public IReadOnlyList<ShortcutItem> Shortcuts { get; } = new[]
    {
        new ShortcutItem("morespaces", "More Spaces", "Recover disk space safely", "\uEDA2"),
        new ShortcutItem("optimization", "Optimization", "Startup apps and performance", "\uE945"),
        new ShortcutItem("debloater", "Debloater", "Remove optional apps", "\uE74D"),
        new ShortcutItem("packages", "Package Manager", "Install software with WinGet", "\uE7B8"),
        new ShortcutItem("maintenance", "Maintenance", "SFC, DISM and restore points", "\uE90F"),
        new ShortcutItem("privacy", "Privacy", "Control Windows privacy settings", "\uE72E"),
    };

    public ICommand OpenCommand { get; }
    public ICommand RefreshCommand { get; }

    public DashboardViewModel(SystemInfoService svc, Action<string> navigate)
    {
        _svc = svc;
        OpenCommand = new RelayCommand(p => { if (p is string key) navigate(key); });
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        _ = LoadAsync();
    }

    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public double UsedPercent { get => _usedPercent; private set => SetProperty(ref _usedPercent, value); }
    public string StorageSummary { get => _storageSummary; private set => SetProperty(ref _storageSummary, value); }
    public string AdminText => AdminHelper.IsAdministrator ? "Running as administrator" : "Running as a standard user (BWU asks for elevation only when an action needs it)";

    private async Task LoadAsync()
    {
        try
        {
            Status = "Reading system information…";
            var items = await _svc.GetDashboardAsync();
            Items.Clear();
            foreach (var i in items) Items.Add(i);

            var drive = new StorageAnalysisService().GetSystemDrive();
            if (drive != null)
            {
                UsedPercent = drive.UsedPercent;
                StorageSummary = $"{drive.DriveRoot.TrimEnd('\\')}  {FormatHelper.Bytes(drive.Used)} used of {FormatHelper.Bytes(drive.Total)} • {FormatHelper.Bytes(drive.Free)} free";
            }
            Status = "Information read from this computer.";
        }
        catch (Exception ex) { Status = "Could not read system information: " + ex.Message; }
    }
}
