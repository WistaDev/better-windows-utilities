using System.Windows;

namespace BWU.Utilities;

public static class Dialogs
{
    private static Window? Owner => Application.Current?.MainWindow;

    private static MessageBoxResult Show(string title, string message, MessageBoxButton b, MessageBoxImage i)
        => Owner != null && Owner.IsVisible
            ? MessageBox.Show(Owner, message, title, b, i)
            : MessageBox.Show(message, title, b, i);

    public static bool Confirm(string title, string message)
        => Show(title, message, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public static void Info(string title, string message) => Show(title, message, MessageBoxButton.OK, MessageBoxImage.Information);
    public static void Warn(string title, string message) => Show(title, message, MessageBoxButton.OK, MessageBoxImage.Warning);
    public static void Error(string title, string message) => Show(title, message, MessageBoxButton.OK, MessageBoxImage.Error);

    public static string? PickImage()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select a logo image",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
