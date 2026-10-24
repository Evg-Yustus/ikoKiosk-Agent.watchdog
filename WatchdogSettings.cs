using System;

internal static class WatchdogSettings
{
    public const string ServiceName = "Resto.CashServer.Svc";
    public const string ServiceDisplayName = "iiko Agent";
    public const string ProcessName = "Resto.CashServer.ServiceControl";
    public const string ProcessFileName = "Resto.CashServer.ServiceControl.exe";
    public const string MutexName = @"Global\iikoKiosk-Agent.watchdog";
    public const string ScheduledTaskName = "iikoKiosk-Agent.watchdog";

    public static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan ServiceWaitTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan ProcessStartWaitTimeout = TimeSpan.FromSeconds(5);
}
