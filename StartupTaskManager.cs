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
                Arguments = string.Join(" ", arguments.Select(QuoteArgument)),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

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

    private static string QuoteArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        if (!argument.Any(char.IsWhiteSpace) && !argument.Contains('"'))
        {
            return argument;
        }

        var builder = new StringBuilder(argument.Length + 2);
        var backslashes = 0;
        builder.Append('"');

        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', backslashes * 2 + 1);
                builder.Append('"');
                backslashes = 0;
                continue;
            }

            builder.Append('\\', backslashes);
            builder.Append(character);
            backslashes = 0;
        }

        builder.Append('\\', backslashes * 2);
        builder.Append('"');
        return builder.ToString();
    }

    private readonly record struct SchtasksResult(int ExitCode, string ErrorText);
}
