using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Input;
using BWU.Models;
using BWU.Services;
using BWU.Utilities;

namespace BWU.ViewModels;

public sealed class SystemInfoViewModel : ObservableObject
{
    private readonly SystemInfoService _svc;
    private string _status = "Loading…";

    public ObservableCollection<InfoSection> Sections { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand CopyCommand { get; }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public SystemInfoViewModel(SystemInfoService svc)
    {
        _svc = svc;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        CopyCommand = new RelayCommand(Copy, () => Sections.Count > 0);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        Status = "Reading hardware and Windows information (this can take a few seconds)…";
        var sections = await _svc.GetSectionsAsync();
        Sections.Clear();
        foreach (var s in sections) Sections.Add(s);
        Status = "Information read from this computer.";
    }

    private void Copy()
    {
        var sb = new StringBuilder();
        foreach (var s in Sections)
        {
            sb.AppendLine("== " + s.Title + " ==");
            foreach (var i in s.Items) sb.AppendLine($"{i.Label}: {i.Value}");
            sb.AppendLine();
        }
        try { Clipboard.SetText(sb.ToString()); Status = "Report copied to the clipboard."; }
        catch (Exception ex) { Status = "Could not copy: " + ex.Message; }
    }
}
