using System.Collections.ObjectModel;
using System.Windows.Input;
using BWU.Models;
using BWU.Services;
using BWU.Utilities;

namespace BWU.ViewModels;

public sealed class DebloaterViewModel : ObservableObject
{
    private readonly DebloaterService _svc;
    private List<AppxItem> _all = new();
    private string _search = "";
    private bool _showProtected, _busy;
    private string _status = "Loading installed apps…";

    public ObservableCollection<AppxItem> Apps { get; } = new();
    public ObservableCollection<SummaryItem> Results { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand SelectOptionalCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand RemoveCommand { get; }

    public DebloaterViewModel(DebloaterService svc)
    {
        _svc = svc;
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        SelectOptionalCommand = new RelayCommand(() => { foreach (var a in _all) a.IsSelected = a.Category == AppCategory.Optional; OnPropertyChanged(nameof(SelectedCount)); });
        ClearSelectionCommand = new RelayCommand(() => { foreach (var a in _all) a.IsSelected = false; OnPropertyChanged(nameof(SelectedCount)); });
        RemoveCommand = new AsyncRelayCommand(RemoveAsync, () => !IsBusy && SelectedCount > 0);
        Results.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasResults));
        _ = LoadAsync();
    }

    public bool HasResults => Results.Count > 0;
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool IsBusy { get => _busy; private set { if (SetProperty(ref _busy, value)) CommandManager.InvalidateRequerySuggested(); } }
    public int SelectedCount => _all.Count(a => a.IsSelected);
    public string RemoveText => $"Review and remove selected ({SelectedCount})";

    public string Search { get => _search; set { if (SetProperty(ref _search, value)) ApplyFilter(); } }
    public bool ShowProtected { get => _showProtected; set { if (SetProperty(ref _showProtected, value)) ApplyFilter(); } }

    private async Task LoadAsync()
    {
        IsBusy = true;
        Status = "Reading installed apps (current user)…";
        var (items, error) = await _svc.ListAsync();
        foreach (var old in _all) old.PropertyChanged -= OnItemChanged;
        _all = items.ToList();
        foreach (var i in _all) i.PropertyChanged += OnItemChanged;
        ApplyFilter();
        Status = error ?? $"{_all.Count} apps found: {_all.Count(a => a.Category == AppCategory.Optional)} optional, " +
                          $"{_all.Count(a => a.Category == AppCategory.Other)} other, {_all.Count(a => a.Category == AppCategory.Protected)} protected.";
        IsBusy = false;
    }

    private void OnItemChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppxItem.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(RemoveText));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void ApplyFilter()
    {
        Apps.Clear();
        foreach (var a in _all.Where(a => (ShowProtected || a.Category != AppCategory.Protected) &&
                    (string.IsNullOrWhiteSpace(Search) ||
                     a.DisplayName.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                     a.Name.Contains(Search, StringComparison.OrdinalIgnoreCase))))
            Apps.Add(a);
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(RemoveText));
    }

    private async Task RemoveAsync()
    {
        var selected = _all.Where(a => a.IsSelected && a.CanSelect).ToList();
        if (selected.Count == 0) return;

        var list = string.Join("\n", selected.Select(a => $"• {a.DisplayName}  ({a.Name})"));
        if (!Dialogs.Confirm("Review changes",
            $"The following {selected.Count} app(s) will be removed for the current user:\n\n{list}\n\n" +
            "Removal affects only your account and can be undone by reinstalling from the Microsoft Store. " +
            "Windows components and dependencies are never removed by BWU.\n\nContinue?")) return;

        IsBusy = true;
        Results.Clear();
        int done = 0;
        foreach (var app in selected)
        {
            Status = $"Removing {app.DisplayName} ({++done} of {selected.Count})…";
            var r = await _svc.RemoveAsync(app);
            Results.Add(new SummaryItem(app.DisplayName, r.Success, r.Message));
        }
        var failed = Results.Count(r => !r.Success);
        await LoadAsync();
        Status = failed == 0 ? $"All {selected.Count} selected app(s) were removed." : $"{selected.Count - failed} removed, {failed} failed. See the results below.";
    }
}
