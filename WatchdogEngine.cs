using System.Diagnostics;

internal sealed record WatchdogCheckResult(string Message, bool IsHealthy);

internal sealed class WatchdogEngine
{
    private static readonly TimeSpan[] RestartBackoff =
    {
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(4),
        TimeSpan.FromMinutes(8),
        TimeSpan.FromMinutes(16),
        TimeSpan.FromMinutes(30)
    };

    private readonly WindowsServiceManager _serviceManager = new();
    private int _serviceFailureCount;
    private int _processFailureCount;
    private DateTime _nextServiceAttemptUtc = DateTime.MinValue;
    private DateTime _nextProcessAttemptUtc = DateTime.MinValue;

    public WatchdogCheckResult CheckOnce()
    {
        var now = DateTime.UtcNow;
        var serviceStatus = _serviceManager.Query(WatchdogSettings.ServiceName);

        if (!serviceStatus.IsRunning)
        {
            if (serviceStatus.State == WindowsServiceState.Missing)
            {
                return new(
                    $"Служба «{WatchdogSettings.ServiceDisplayName}» не найдена.",
                    false);
            }

            if (now < _nextServiceAttemptUtc)
            {
                return new(
                    $"Служба «{WatchdogSettings.ServiceDisplayName}» не запущена. Следующая попытка позже.",
                    false);
            }

            serviceStatus = _serviceManager.EnsureRunning(WatchdogSettings.ServiceName);
            if (!serviceStatus.IsRunning)
            {
                RegisterServiceFailure(now);
                return new(
                    $"Не удалось запустить службу «{WatchdogSettings.ServiceDisplayName}» (код {serviceStatus.ErrorCode}).",
                    false);
            }

            ResetServiceFailures();
        }
        else
        {
            ResetServiceFailures();
        }

        if (IsProcessRunning())
        {
            ResetProcessFailures();
            return new("Служба и приложение агента запущены.", true);
        }

        if (now < _nextProcessAttemptUtc)
        {
            return new("Служба запущена, ожидается следующая попытка запуска приложения.", false);
        }

        var processPath = ProcessPathResolver.Find();
        if (processPath is null)
        {
            return new(
                $"Служба запущена, но {WatchdogSettings.ProcessFileName} не найден.",
                false);
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = processPath,
                WorkingDirectory = Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            RegisterProcessFailure(now);
            return new($"Не удалось запустить приложение агента: {exception.Message}", false);
        }

        if (WaitForProcess())
        {
            ResetProcessFailures();
            return new("Служба запущена, приложение агента запущено.", true);
        }

        RegisterProcessFailure(now);
        return new("Приложение агента было запущено, но процесс не появился.", false);
    }

    private static bool IsProcessRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName(WatchdogSettings.ProcessName);
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool WaitForProcess()
    {
        var deadline = DateTime.UtcNow + WatchdogSettings.ProcessStartWaitTimeout;
        do
        {
            if (IsProcessRunning())
            {
                return true;
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(500));
        }
        while (DateTime.UtcNow < deadline);

        return IsProcessRunning();
    }

    private void RegisterServiceFailure(DateTime now)
    {
        _serviceFailureCount = Math.Min(_serviceFailureCount + 1, RestartBackoff.Length);
        _nextServiceAttemptUtc = now + RestartBackoff[_serviceFailureCount - 1];
    }

    private void RegisterProcessFailure(DateTime now)
    {
        _processFailureCount = Math.Min(_processFailureCount + 1, RestartBackoff.Length);
        _nextProcessAttemptUtc = now + RestartBackoff[_processFailureCount - 1];
    }

    private void ResetServiceFailures()
    {
        _serviceFailureCount = 0;
        _nextServiceAttemptUtc = DateTime.MinValue;
    }

    private void ResetProcessFailures()
    {
        _processFailureCount = 0;
        _nextProcessAttemptUtc = DateTime.MinValue;
    }
}
