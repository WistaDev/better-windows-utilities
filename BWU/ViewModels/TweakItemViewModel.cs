using System.Windows.Input;
using BWU.Services;
using BWU.Utilities;

namespace BWU.ViewModels;

/// <summary>One registry setting shown as a card: explanation, status and Apply / Revert.</summary>
public sealed class TweakItemViewModel : ObservableObject
{
    private readonly TweakService _svc;
    private bool _isApplied, _isSupported = true, _hasBackup;
    private string _statusText = "", _resultText = "";

    public RegistryTweak Tweak { get; }
    public ICommand ApplyCommand { get; }
    public ICommand RevertCommand { get; }

    public TweakItemViewModel(RegistryTweak tweak, TweakService svc)
    {
        Tweak = tweak;
        _svc = svc;
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => IsSupported && !IsApplied);
        RevertCommand = new AsyncRelayCommand(RevertAsync, () => IsSupported && (IsApplied || HasBackup));
        Refresh();
    }

    public string Title => Tweak.Title;
    public string Description => Tweak.Description;
    public string Consequence => Tweak.Consequence;
    public bool RequiresAdmin => Tweak.RequiresAdmin;

    public bool IsApplied { get => _isApplied; private set => SetProperty(ref _isApplied, value); }
    public bool IsSupported { get => _isSupported; private set => SetProperty(ref _isSupported, value); }
    public bool HasBackup { get => _hasBackup; private set => SetProperty(ref _hasBackup, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string ResultText { get => _resultText; private set => SetProperty(ref _resultText, value); }

    public void Refresh()
    {
        IsSupported = _svc.IsSupported(Tweak);
        IsApplied = IsSupported && _svc.IsApplied(Tweak);
        HasBackup = _svc.HasBackup(Tweak);
        StatusText = !IsSupported ? "Not supported on this version of Windows" : IsApplied ? "Applied" : "Not applied";
        CommandManager.InvalidateRequerySuggested();
    }

    private async Task ApplyAsync()
    {
        var msg = $"{Tweak.Description}\n\nEffect: {Tweak.Consequence}\n\n" +
                  "The current value is backed up first, so you can revert this change." +
                  (Tweak.RequiresAdmin ? "\nAdministrator rights are required." : "") + "\n\nApply now?";
        if (!Dialogs.Confirm("Apply: " + Title, msg)) return;
        if (Tweak.RequiresAdmin && !AdminHelper.EnsureAdmin("This setting is machine-wide and needs administrator rights.")) return;

        var r = await Task.Run(() => _svc.Apply(Tweak));
        ResultText = (r.Success ? "✔ " : "✖ ") + r.Message;
        Refresh();
    }

    private async Task RevertAsync()
    {
        if (!Dialogs.Confirm("Revert: " + Title, "BWU will restore the original value saved before this setting was changed. Continue?")) return;
        if (Tweak.RequiresAdmin && !AdminHelper.EnsureAdmin("Reverting this machine-wide setting needs administrator rights.")) return;

        var r = await Task.Run(() => _svc.Revert(Tweak));
        ResultText = (r.Success ? "✔ " : "✖ ") + r.Message;
        Refresh();
    }
}
