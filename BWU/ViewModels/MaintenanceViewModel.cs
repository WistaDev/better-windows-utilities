using System.Collections.ObjectModel;
using System.Windows.Input;
using BWU.Services;
using BWU.Utilities;

namespace BWU.ViewModels;

public sealed class MaintenanceViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;
    private bool _busy, _indeterminate;
    private double _progress;
    private string _status = "Choose an operation. Output appears below exactly as Windows reports it.";

    public IReadOnlyList<MaintenanceOperation> Operations { get; }
    public IReadOnlyList<(string Title, string Uri)> UpdateShortcuts => TweakCatalog.WindowsUpdateShortcuts;
    public LogBuffer Log { get; } = new();
    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenUriCommand { get; }
    public ICommand OpenRestoreCommand { get; }
    public ICommand ClearLogCommand { get; }

    public MaintenanceViewModel(MaintenanceService svc)
    {
        Operations = svc.Operations;
        RunCommand = new AsyncRelayCommand(p => RunAsync((MaintenanceOperation)p!), p => !IsBusy && p is MaintenanceOperation);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        OpenUriCommand = new RelayCommand(p => { if (p is string u) CustomizationService.OpenUri(u); });
        OpenRestoreCommand = new RelayCommand(() => CustomizationService.OpenUri("rstrui.exe"));
        ClearLogCommand = new RelayCommand(Log.Clear);
    }

    public bool IsBusy { get => _busy; private set { if (SetProperty(ref _busy, value)) CommandManager.InvalidateRequerySuggested(); } }
    public bool IsIndeterminate { get => _indeterminate; private set => SetProperty(ref _indeterminate, value); }
    public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    private async Task RunAsync(MaintenanceOperation op)
    {
        var message = op.Description + (op.ConfirmText != null ? "\n\n" + op.ConfirmText : "") + "\n\nRun now?";
        if (!Dialogs.Confirm(op.Title, message)) return;
        if (op.RequiresAdmin && !AdminHelper.EnsureAdmin($"'{op.Title}' needs administrator rights.")) return;

        _cts = new CancellationTokenSource();
        IsBusy = true; IsIndeterminate = true; Progress = 0;
        Status = "Running: " + op.Title;
        Log.Append("");
        Log.Append($"=== {op.Title} — {DateTime.Now:T} ===");

        int exit;
        try
        {
            exit = await op.Run(line =>
            {
                var pct = ProcessRunner.TryParsePercent(line);
                if (pct.HasValue && line.Length < 100)
                {
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => { IsIndeterminate = false; Progress = pct.Value; });
                    // Log only every 10 % to keep the output readable (SFC prints every percent).
                    if (!ProcessRunner.IsNoise(line) && ((int)pct.Value % 10 == 0)) Log.Append(line.Trim());
                    return;
                }
                if (!ProcessRunner.IsNoise(line)) Log.Append(line.Trim());
            }, _cts.Token);
        }
        catch (Exception ex) { Log.Append("Error: " + ex.Message); exit = -1; }

        var cancelled = _cts.IsCancellationRequested;
        IsBusy = false; IsIndeterminate = false;
        _cts.Dispose(); _cts = null;
        Status = cancelled ? $"{op.Title}: cancelled." : exit == 0 ? $"{op.Title}: finished (exit code 0)." : $"{op.Title}: finished with problems (exit code {exit}). See the output.";
    }
}
