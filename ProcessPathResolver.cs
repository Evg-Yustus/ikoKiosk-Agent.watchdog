internal static class ProcessPathResolver
{
    public static string? Find()
    {
        foreach (var path in DirectCandidates())
        {
            if (File.Exists(path))
            {
                return path;
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

}
