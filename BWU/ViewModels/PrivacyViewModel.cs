using System.Collections.ObjectModel;
using System.Windows.Input;
using BWU.Services;
using BWU.Utilities;

namespace BWU.ViewModels;

public sealed class PrivacyViewModel : ObservableObject
{
    public ObservableCollection<TweakItemViewModel> Tweaks { get; } = new();
    public IReadOnlyList<(string Title, string Uri)> Shortcuts => TweakCatalog.PrivacyShortcuts;
    public ICommand OpenUriCommand { get; }
    public ICommand RefreshCommand { get; }

    public PrivacyViewModel(TweakService tweaks)
    {
        foreach (var t in TweakCatalog.Privacy) Tweaks.Add(new TweakItemViewModel(t, tweaks));
        OpenUriCommand = new RelayCommand(p => { if (p is string u) CustomizationService.OpenUri(u); });
        RefreshCommand = new RelayCommand(() => { foreach (var t in Tweaks) t.Refresh(); OnPropertyChanged(nameof(Overview)); });
        foreach (var t in Tweaks) t.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TweakItemViewModel.IsApplied)) OnPropertyChanged(nameof(Overview)); };
    }

    public string Overview
    {
        get
        {
            var supported = Tweaks.Count(t => t.IsSupported);
            var applied = Tweaks.Count(t => t.IsApplied);
            return $"{applied} of {supported} privacy settings managed by BWU are currently applied.";
        }
    }
}
