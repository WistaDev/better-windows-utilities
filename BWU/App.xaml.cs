using System.Text;
using System.Windows;
using System.Windows.Threading;
using BWU.Services;
using BWU.Utilities;

namespace BWU;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);   // OEM code pages for console tools

        DispatcherUnhandledException += OnUnhandled;
        AppDomain.CurrentDomain.UnhandledException += (_, a) =>
            Dialogs.Error("BWU", "A fatal error occurred: " + (a.ExceptionObject as Exception)?.Message);

        if (!WindowsService.IsSupportedOs)
        {
            MessageBox.Show("BWU requires Windows 10 or Windows 11.", "BWU", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var settings = SettingsService.Instance;
        settings.Load();
        ThemeService.Apply(settings.Settings.Theme);
        Loc.SetLanguage(settings.Settings.Language);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Dialogs.Error("Unexpected error", e.Exception.Message);
        e.Handled = true;
    }
}
