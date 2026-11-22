using System.Text;
using System.Windows;

namespace BWU.Utilities;

/// <summary>Thread-safe text log bound to a read-only TextBox.</summary>
public sealed class LogBuffer : ObservableObject
{
    private readonly StringBuilder _sb = new();
    private string _text = "";

    public string Text { get => _text; private set => SetProperty(ref _text, value); }

    public void Append(string line) => OnUi(() =>
    {
        _sb.AppendLine(line);
        if (_sb.Length > 200_000) _sb.Remove(0, _sb.Length - 150_000);
        Text = _sb.ToString();
    });

    public void Clear() => OnUi(() => { _sb.Clear(); Text = ""; });

    private static void OnUi(Action a)
    {
        var d = Application.Current?.Dispatcher;
        if (d == null || d.CheckAccess()) a(); else d.BeginInvoke(a);
    }
}
