using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;
using System.ComponentModel;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("ru-RU");

        if (!Elevation.TryEnsureAdministrator(args))
        {
            return 0;
        }

        using var instanceMutex = new Mutex(true, WatchdogSettings.MutexName, out var createdNew);
        if (!createdNew)
        {
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new WatchdogForm());
        return 0;
    }
}

internal static class Elevation
{
    public static bool TryEnsureAdministrator(IReadOnlyCollection<string> args)
    {
        if (IsAdministrator())
        {
            return true;
        }

        if (args.Any(argument => string.Equals(argument, "--elevated", StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(
                "iikoKiosk Agent watchdog не получил права администратора и будет закрыт.",
                "iikoKiosk Agent watchdog",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }

        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new InvalidOperationException("Не удалось определить путь к EXE.");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "--elevated",
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                UseShellExecute = true,
                Verb = "runas"
            });

            return false;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            return false;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Не удалось запустить watchdog с правами администратора:\n{exception.Message}",
                "iikoKiosk Agent watchdog",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
