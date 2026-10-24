using System.Text.RegularExpressions;

internal static class ProcessPathResolver
{
    private static readonly Regex ProcessPathPattern = new(
        "\"(?<path>[^\"]*Resto\\.CashServer\\.ServiceControl\\.exe)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string? Find()
    {
        foreach (var path in DirectCandidates())
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        foreach (var scriptPath in ScriptCandidates())
        {
            if (!File.Exists(scriptPath))
            {
                continue;
            }

            try
            {
                var script = File.ReadAllText(scriptPath);
                foreach (Match match in ProcessPathPattern.Matches(script))
                {
                    var path = match.Groups["path"].Value.Trim();
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }
            catch (IOException)
            {
                // The installation can be changed while the watchdog is running.
            }
            catch (UnauthorizedAccessException)
            {
                // A missing readable script is handled on the next check.
            }
        }

        return null;
    }

    private static IEnumerable<string> DirectCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, WatchdogSettings.ProcessFileName);
        yield return @"C:\RMS\iikoRMS\Front.Net\Resto.CashServer.ServiceControl.exe";
        yield return @"C:\Program Files\iiko\iikoRMS\Front.Net\Resto.CashServer.ServiceControl.exe";
        yield return @"C:\Program Files (x86)\iiko\iikoRMS\Front.Net\Resto.CashServer.ServiceControl.exe";
    }

    private static IEnumerable<string> ScriptCandidates()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!string.IsNullOrWhiteSpace(desktop))
        {
            yield return Path.Combine(desktop, "RestoCashServerStart.cmd");
        }

        yield return @"C:\ProgramData\RestoCashServerStart.cmd";
    }
}
