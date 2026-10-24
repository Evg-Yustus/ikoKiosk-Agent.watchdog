using System.Diagnostics;
using System.Text;

internal static class StartupTaskManager
{
    public static bool IsEnabled()
    {
        return RunSchtasks("/Query", "/TN", WatchdogSettings.ScheduledTaskName).ExitCode == 0;
    }

    public static void Enable(string executablePath)
    {
        var action = $"\"{executablePath}\" --autostart";
        var result = RunSchtasks(
            "/Create",
            "/TN",
            WatchdogSettings.ScheduledTaskName,
            "/TR",
            action,
            "/SC",
            "ONLOGON",
            "/RL",
            "HIGHEST",
            "/F");

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Не удалось создать задачу планировщика: {result.ErrorText}");
        }
    }

    public static void Disable()
    {
        if (!IsEnabled())
        {
            return;
        }

        var result = RunSchtasks(
            "/Delete",
            "/TN",
            WatchdogSettings.ScheduledTaskName,
            "/F");

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Не удалось удалить задачу планировщика: {result.ErrorText}");
        }
    }

    private static SchtasksResult RunSchtasks(params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(outputTask, errorTask);

        var errorText = string.IsNullOrWhiteSpace(errorTask.Result)
            ? outputTask.Result.Trim()
            : errorTask.Result.Trim();

        return new(process.ExitCode, errorText);
    }

    private readonly record struct SchtasksResult(int ExitCode, string ErrorText);
}
