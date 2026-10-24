using System.ComponentModel;
using System.Runtime.InteropServices;

internal enum WindowsServiceState
{
    Missing,
    Stopped,
    StartPending,
    Running,
    ContinuePending,
    PausePending,
    Paused,
    StopPending,
    Unknown
}

internal readonly record struct WindowsServiceStatus(WindowsServiceState State, int ErrorCode)
{
    public bool IsRunning => State == WindowsServiceState.Running;
}

internal sealed class WindowsServiceManager
{
    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceQueryStatus = 0x0004;
    private const uint ServiceStart = 0x0010;
    private const int ScStatusProcessInfo = 0;
    private const uint ServiceStopped = 0x00000001;
    private const uint ServiceStartPending = 0x00000002;
    private const uint ServiceRunning = 0x00000004;
    private const uint ServiceContinuePending = 0x00000005;
    private const uint ServicePausePending = 0x00000006;
    private const uint ServicePaused = 0x00000007;
    private const uint ServiceStopPending = 0x00000003;
    private const int ErrorServiceDoesNotExist = 1060;
    private const int ErrorServiceAlreadyRunning = 1056;

    public WindowsServiceStatus Query(string serviceName)
    {
        var serviceManager = OpenSCManager(null, null, ScManagerConnect);
        if (serviceManager == IntPtr.Zero)
        {
            return new(WindowsServiceState.Unknown, Marshal.GetLastWin32Error());
        }

        try
        {
            var service = OpenService(serviceManager, serviceName, ServiceQueryStatus);
            if (service == IntPtr.Zero)
            {
                var errorCode = Marshal.GetLastWin32Error();
                return errorCode == ErrorServiceDoesNotExist
                    ? new(WindowsServiceState.Missing, errorCode)
                    : new(WindowsServiceState.Unknown, errorCode);
            }

            try
            {
                return QueryStatus(service);
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(serviceManager);
        }
    }

    public WindowsServiceStatus EnsureRunning(string serviceName)
    {
        var currentStatus = Query(serviceName);
        if (currentStatus.IsRunning)
        {
            return currentStatus;
        }

        if (currentStatus.State == WindowsServiceState.StartPending)
        {
            return WaitForRunning(serviceName);
        }

        if (currentStatus.State != WindowsServiceState.Stopped)
        {
            return currentStatus;
        }

        var serviceManager = OpenSCManager(null, null, ScManagerConnect);
        if (serviceManager == IntPtr.Zero)
        {
            return new(WindowsServiceState.Unknown, Marshal.GetLastWin32Error());
        }

        try
        {
            var service = OpenService(
                serviceManager,
                serviceName,
                ServiceQueryStatus | ServiceStart);

            if (service == IntPtr.Zero)
            {
                var errorCode = Marshal.GetLastWin32Error();
                return errorCode == ErrorServiceDoesNotExist
                    ? new(WindowsServiceState.Missing, errorCode)
                    : new(WindowsServiceState.Unknown, errorCode);
            }

            try
            {
                if (!StartService(service, 0, IntPtr.Zero))
                {
                    var errorCode = Marshal.GetLastWin32Error();
                    if (errorCode != ErrorServiceAlreadyRunning)
                    {
                        return new(WindowsServiceState.Unknown, errorCode);
                    }
                }
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(serviceManager);
        }

        return WaitForRunning(serviceName);
    }

    private static WindowsServiceStatus WaitForRunning(string serviceName)
    {
        var deadline = DateTime.UtcNow + WatchdogSettings.ServiceWaitTimeout;
        WindowsServiceStatus status;

        do
        {
            status = new WindowsServiceManager().Query(serviceName);
            if (status.IsRunning
                || status.State is WindowsServiceState.Missing or WindowsServiceState.Unknown)
            {
                return status;
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(500));
        }
        while (DateTime.UtcNow < deadline);

        return status;
    }

    private static WindowsServiceStatus QueryStatus(IntPtr service)
    {
        if (!QueryServiceStatusEx(
                service,
                ScStatusProcessInfo,
                out var status,
                (uint)Marshal.SizeOf<SERVICE_STATUS_PROCESS>(),
                out _))
        {
            return new(WindowsServiceState.Unknown, Marshal.GetLastWin32Error());
        }

        return new(MapState(status.CurrentState), 0);
    }

    private static WindowsServiceState MapState(uint state)
    {
        return state switch
        {
            ServiceStopped => WindowsServiceState.Stopped,
            ServiceStartPending => WindowsServiceState.StartPending,
            ServiceRunning => WindowsServiceState.Running,
            ServiceContinuePending => WindowsServiceState.ContinuePending,
            ServicePausePending => WindowsServiceState.PausePending,
            ServicePaused => WindowsServiceState.Paused,
            ServiceStopPending => WindowsServiceState.StopPending,
            _ => WindowsServiceState.Unknown
        };
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManager(
        string? machineName,
        string? databaseName,
        uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenService(
        IntPtr serviceManager,
        string serviceName,
        uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceStatusEx(
        IntPtr service,
        int infoLevel,
        out SERVICE_STATUS_PROCESS status,
        uint bufferSize,
        out uint bytesNeeded);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool StartService(
        IntPtr service,
        uint numberOfArguments,
        IntPtr argumentVectors);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS_PROCESS
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
        public uint ProcessId;
        public uint ServiceFlags;
    }
}
