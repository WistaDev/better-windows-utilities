using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using BWU.Models;
using BWU.Services;
using BWU.Services.MoreSpaces;
using BWU.Utilities;

namespace BWU.ViewModels;

public sealed class MoreSpacesViewModel : ObservableObject
{
    private readonly CompactOSService _compact;
    private readonly WindowsTempService _winTemp;
    private readonly UserTempService _userTemp;
    private readonly PrefetchService _prefetch;
    private readonly StorageAnalysisService _storage;
    private readonly ComponentStoreService _components;

    private CancellationTokenSource? _cts;
    private FolderAnalysis? _winAnalysis, _userAnalysis;
    private CompactState _compactState = CompactState.Unknown;
    private bool _busy, _indeterminate, _canCancel;
    private double _progress;
    private string _progressText = "Idle";
    private string _compactStatus = "Checking…", _winText = "Analyzing…", _userText = "Analyzing…", _prefetchText = "Analyzing…";
    private string _total = "—", _used = "—", _free = "—", _tempTotal = "—", _recoverable = "—", _summaryTotal = "";
    private double _usedPercent;
    private long _totalFreed;

    public ObservableCollection<SummaryItem> Summary { get; } = new();
    public LogBuffer Log { get; } = new();

    public ICommand AnalyzeCommand { get; }
    public ICommand EnableCompactCommand { get; }
    public ICommand DisableCompactCommand { get; }
    public ICommand CleanWindowsTempCommand { get; }
    public ICommand CleanUserTempCommand { get; }
    public ICommand CleanAllTempCommand { get; }
    public ICommand ComponentCleanupCommand { get; }
    public ICommand CancelCommand { get; }

    public MoreSpacesViewModel(CompactOSService compact, WindowsTempService winTemp, UserTempService userTemp,
        PrefetchService prefetch, StorageAnalysisService storage, ComponentStoreService components)
    {
        _compact = compact; _winTemp = winTemp; _userTemp = userTemp; _prefetch = prefetch; _storage = storage; _components = components;

        Func<bool> idle = () => !IsBusy;
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, idle);
        EnableCompactCommand = new AsyncRelayCommand(() => SetCompactAsync(true), idle);
        DisableCompactCommand = new AsyncRelayCommand(() => SetCompactAsync(false), idle);
        CleanWindowsTempCommand = new AsyncRelayCommand(() => CleanAsync(windows: true, user: false), idle);
        CleanUserTempCommand = new AsyncRelayCommand(() => CleanAsync(windows: false, user: true), idle);
        CleanAllTempCommand = new AsyncRelayCommand(() => CleanAsync(windows: true, user: true), idle);
        ComponentCleanupCommand = new AsyncRelayCommand(ComponentCleanupAsync, idle);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy && _canCancel);
        _ = AnalyzeAsync();
    }

    // ----- bindable state -----
    public bool IsBusy { get => _busy; private set { if (SetProperty(ref _busy, value)) CommandManager.InvalidateRequerySuggested(); } }
    public bool IsIndeterminate { get => _indeterminate; private set => SetProperty(ref _indeterminate, value); }
    public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    public string ProgressText { get => _progressText; private set => SetProperty(ref _progressText, value); }
    public string CompactStatus { get => _compactStatus; private set => SetProperty(ref _compactStatus, value); }
    public string WindowsTempText { get => _winText; private set => SetProperty(ref _winText, value); }
    public string UserTempText { get => _userText; private set => SetProperty(ref _userText, value); }
    public string PrefetchText { get => _prefetchText; private set => SetProperty(ref _prefetchText, value); }
    public string TotalText { get => _total; private set => SetProperty(ref _total, value); }
    public string UsedText { get => _used; private set => SetProperty(ref _used, value); }
    public string FreeText { get => _free; private set => SetProperty(ref _free, value); }
    public double UsedPercent { get => _usedPercent; private set => SetProperty(ref _usedPercent, value); }
    public string TempTotalText { get => _tempTotal; private set => SetProperty(ref _tempTotal, value); }
    public string RecoverableText { get => _recoverable; private set => SetProperty(ref _recoverable, value); }
    public string SummaryTotal { get => _summaryTotal; private set => SetProperty(ref _summaryTotal, value); }
    public string AdminNote => AdminHelper.IsAdministrator ? "" :
        "BWU is not running as administrator. Compact OS, Windows Temp, Prefetch and DISM actions will offer to restart elevated.";

    // ----- helpers -----
    private void Begin(string text, bool indeterminate, bool cancellable)
    {
        _cts = new CancellationTokenSource();
        _canCancel = cancellable;
        IsBusy = true; IsIndeterminate = indeterminate; Progress = 0; ProgressText = text;
    }

    private void End(string text = "Idle")
    {
        IsBusy = false; IsIndeterminate = false; Progress = 0; ProgressText = text;
        _cts?.Dispose(); _cts = null;
    }

    private void AddSummary(string op, bool ok, string detail)
    {
        Summary.Insert(0, new SummaryItem($"{DateTime.Now:t}  {op}", ok, detail));
        SummaryTotal = _totalFreed > 0 ? $"Temporary files removed in this session: {FormatHelper.Bytes(_totalFreed)}" : "";
    }

    private static string Describe(FolderAnalysis a)
    {
        if (a.Note != null) return a.Note;
        return $"{a.FileCount:N0} files • {FormatHelper.Bytes(a.TotalBytes)} total • about {FormatHelper.Bytes(a.EligibleBytes)} ({a.EligibleCount:N0} files) look eligible for cleanup (estimate)";
    }

    // ----- analysis -----
    private async Task AnalyzeAsync()
    {
        Begin("Analyzing…", true, false);
        try
        {
            var compactTask = _compact.QueryAsync();
            var winTask = _winTemp.AnalyzeAsync();
            var userTask = _userTemp.AnalyzeAsync();
            var prefetchTask = _prefetch.AnalyzeAsync();
            await Task.WhenAll(compactTask, winTask, userTask, prefetchTask);

            var (state, raw) = compactTask.Result;
            _compactState = state;
            CompactStatus = state switch
            {
                CompactState.Compressed => "Enabled — Windows system files are compressed.",
                CompactState.NotCompressed => "Disabled — Windows system files are not compressed.",
                _ => "Could not determine the state. compact.exe reported: " + (string.IsNullOrWhiteSpace(raw) ? "(no output)" : raw)
            };

            _winAnalysis = winTask.Result; _userAnalysis = userTask.Result;
            WindowsTempText = $"{_winAnalysis.Path}\n{Describe(_winAnalysis)}";
            UserTempText = $"{_userAnalysis.Path}\n{Describe(_userAnalysis)}";

            var pf = prefetchTask.Result;
            PrefetchText = !pf.Exists ? $"{pf.Path}\nFolder not found (prefetching: {pf.PrefetcherState})."
                : pf.AccessDenied ? $"{pf.Path}\nAdministrator rights are required to read this folder."
                : $"{pf.Path}\n{pf.FileCount:N0} files ({pf.PfFiles:N0} .pf) • {FormatHelper.Bytes(pf.TotalBytes)} • prefetching: {pf.PrefetcherState}\n" +
                  "BWU only reports on this folder. Windows uses it to start apps faster, so it is never deleted.";

            RefreshStorage();
        }
        finally { End(); }
    }

    private void RefreshStorage()
    {
        var s = _storage.GetSystemDrive();
        if (s != null)
        {
            TotalText = FormatHelper.Bytes(s.Total);
            UsedText = FormatHelper.Bytes(s.Used);
            FreeText = FormatHelper.Bytes(s.Free);
            UsedPercent = s.UsedPercent;
        }
        var temp = (_winAnalysis?.TotalBytes ?? 0) + (_userAnalysis?.TotalBytes ?? 0);
        var eligible = (_winAnalysis?.EligibleBytes ?? 0) + (_userAnalysis?.EligibleBytes ?? 0);
        TempTotalText = FormatHelper.Bytes(temp);
        RecoverableText = "about " + FormatHelper.Bytes(eligible) + " (estimate; actual result depends on which files are in use)";
    }

    // ----- Compact OS -----
    private async Task SetCompactAsync(bool enable)
    {
        if (!AdminHelper.EnsureAdmin("Compact OS changes system file compression and needs administrator rights.")) return;

        var text = enable
            ? "Compact OS compresses Windows system files to free disk space. The amount varies by system; no specific saving is guaranteed.\n\n" +
              "• It can take several minutes. Do not shut down or sleep the PC meanwhile — BWU does not interrupt it once started.\n" +
              "• Windows must decompress files when it reads them, which can slightly increase CPU usage, mostly noticeable on older or slower processors.\n" +
              "• It is reversible: you can disable Compact OS here later.\n\nEnable Compact OS?"
            : "This decompresses the Windows system files. It needs enough free disk space to expand them and can take several minutes. Do not shut down the PC meanwhile.\n\nDisable Compact OS?";
        if (!Dialogs.Confirm(enable ? "Enable Compact OS" : "Disable Compact OS", text)) return;

        var before = _storage.GetSystemDrive()?.Free;
        Begin(enable ? "Enabling Compact OS… (cannot be cancelled)" : "Disabling Compact OS… (cannot be cancelled)", true, false);
        Log.Append($"--- {(enable ? "Enable" : "Disable")} Compact OS — {DateTime.Now:T} ---");
        try
        {
            var r = await _compact.SetAsync(enable, Log.Append);
            Log.Append(r.Message);
            var after = _storage.GetSystemDrive()?.Free;
            var delta = before.HasValue && after.HasValue ? after.Value - before.Value : (long?)null;
            var detail = r.Message + (r.Success && delta.HasValue ? $" Measured change in free space: {(delta >= 0 ? "+" : "-")}{FormatHelper.Bytes(Math.Abs(delta.Value))}." : "");
            AddSummary(enable ? "Enable Compact OS" : "Disable Compact OS", r.Success, detail);
        }
        finally { End(); }
        await AnalyzeAsync();
    }

    // ----- Temp cleaning -----
    private async Task CleanAsync(bool windows, bool user)
    {
        var parts = new List<string>();
        long estimate = 0;
        if (windows) { parts.Add(_winTemp.Path); estimate += _winAnalysis?.EligibleBytes ?? 0; }
        if (user) { parts.Add(_userTemp.Path); estimate += _userAnalysis?.EligibleBytes ?? 0; }

        var admin = windows && !AdminHelper.IsAdministrator
            ? "\n\nBWU is not running as administrator, so files in Windows\\Temp that your account cannot delete will be skipped. (Press No and restart as administrator from Settings for a fuller cleanup.)"
            : "";
        if (!Dialogs.Confirm("Clean temporary files",
            "BWU will delete temporary files in:\n• " + string.Join("\n• ", parts) +
            "\n\nOnly files older than one hour that are not read-only, not system files and not in use are removed. " +
            "Locked, protected or inaccessible files are skipped, and personal documents, photos, videos and downloads are never touched. " +
            $"Estimated recoverable space: about {FormatHelper.Bytes(estimate)}; the real amount can be lower." + admin + "\n\nContinue?")) return;

        Begin("Cleaning temporary files…", false, true);
        Log.Append($"--- Clean temporary files — {DateTime.Now:T} ---");
        try
        {
            var steps = (windows ? 1 : 0) + (user ? 1 : 0);
            var step = 0;
            if (windows) { await RunCleanStep("Windows Temp", _winTemp.Path, _winTemp.CleanAsync, step++, steps); }
            if (user && !(_cts?.IsCancellationRequested ?? false)) { await RunCleanStep("User Temp", _userTemp.Path, _userTemp.CleanAsync, step++, steps); }
        }
        finally { End(windows && user ? "Finished" : "Idle"); }
        await AnalyzeAsync();
    }

    private async Task RunCleanStep(string name, string path, Func<IProgress<ProgressInfo>?, CancellationToken, Task<CleanupResult>> clean, int step, int steps)
    {
        var progress = new System.Progress<ProgressInfo>(p =>
        {
            Progress = (step * 100.0 + p.Percent) / steps;
            ProgressText = $"{name}: {p.Message}";
        });
        var r = await clean(progress, _cts!.Token);
        _totalFreed += r.BytesFreed;
        var ok = r.Note == null || r.Cancelled;
        var detail = r.Note != null && !r.Cancelled
            ? r.Note
            : $"Removed {r.FilesDeleted:N0} files and {r.DirectoriesRemoved:N0} empty folders, recovered {FormatHelper.Bytes(r.BytesFreed)}; skipped {r.FilesSkipped:N0} locked or protected files ({r.Elapsed.TotalSeconds:0.#} s)." +
              (r.Cancelled ? " Cancelled before finishing." : "");
        Log.Append($"{name}: {detail}");
        AddSummary($"Clean {name}", ok, detail);
    }

    // ----- DISM component store -----
    private async Task ComponentCleanupAsync()
    {
        if (!AdminHelper.EnsureAdmin("DISM component cleanup needs administrator rights.")) return;
        if (!Dialogs.Confirm("Component store cleanup",
            "DISM will remove superseded Windows components from the component store (WinSxS) using the supported StartComponentCleanup command.\n\n" +
            "Installed updates stay uninstallable (BWU does not use /ResetBase). It can take several minutes; do not shut down the PC meanwhile. The space saved varies.\n\nContinue?")) return;

        var before = _storage.GetSystemDrive()?.Free;
        Begin("Cleaning the component store… (cannot be cancelled)", false, false);
        IsIndeterminate = true;
        Log.Append($"--- DISM component cleanup — {DateTime.Now:T} ---");
        try
        {
            var r = await _components.CleanupAsync(Log.Append, p => Application.Current.Dispatcher.BeginInvoke(() => { IsIndeterminate = false; Progress = p; }));
            Log.Append(r.Message);
            var after = _storage.GetSystemDrive()?.Free;
            var delta = before.HasValue && after.HasValue ? after.Value - before.Value : (long?)null;
            AddSummary("Component store cleanup", r.Success,
                r.Message + (r.Success && delta.HasValue ? $" Measured change in free space: {(delta >= 0 ? "+" : "-")}{FormatHelper.Bytes(Math.Abs(delta.Value))}." : ""));
        }
        finally { End(); }
        await AnalyzeAsync();
    }
}
