using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Windows;

namespace BWU.Utilities;

public static class AdminHelper
{
    public static bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <summary>
    /// Returns true when the process is already elevated. Otherwise explains why elevation is needed and
    /// offers to restart BWU as administrator (returns false in that case, the current instance closes).
    /// </summary>
    public static bool EnsureAdmin(string reason)
    {
        if (IsAdministrator) return true;
        if (Dialogs.Confirm("Administrator rights required",
                reason + "\n\nRestart BWU as administrator now?"))
            RestartAsAdmin();
        return false;
    }

    public static bool RestartAsAdmin()
    {
        try
        {
            var exe = Environment.ProcessPath!;
            var psi = new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" };
            // Development case: running through "dotnet BWU.dll".
            if (string.Equals(Path.GetFileNameWithoutExtension(exe), "dotnet", StringComparison.OrdinalIgnoreCase))
                psi.Arguments = "\"" + Assembly.GetEntryAssembly()!.Location + "\"";
            Process.Start(psi);
            Application.Current.Shutdown();
            return true;
        }
        catch (Win32Exception)
        {
            Dialogs.Info("Elevation cancelled", "BWU was not restarted because the administrator prompt was cancelled.");
            return false;
        }
    }
}
