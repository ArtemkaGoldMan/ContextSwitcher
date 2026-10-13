using System.Diagnostics;
using System.Globalization;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Infrastructure.Browser;

/// <summary>
/// Tells one run of Chrome or Brave from the next by its main process: the process id together with
/// when it started, since an id alone can come round again. The browser's helper processes have
/// longer names ("Google Chrome Helper"), so the exact name is the main one.
/// </summary>
public sealed class ProcessBrowserRuns : IBrowserRuns
{
    /// <inheritdoc />
    public string? Current(BrowserKind browser)
    {
        string? name = browser switch
        {
            BrowserKind.Chrome => "Google Chrome",
            BrowserKind.Brave => "Brave Browser",
            _ => null
        };
        if (name is null)
        {
            return null;
        }

        Process[] processes = Process.GetProcessesByName(name);
        try
        {
            // One main process per browser; were there ever two, neither could be told to be the one scripted.
            if (processes.Length != 1)
            {
                return null;
            }

            Process main = processes[0];
            return string.Create(CultureInfo.InvariantCulture, $"{main.Id}@{main.StartTime.ToUniversalTime():O}");
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // It quit between being found and being asked, or its start time can't be read.
            return null;
        }
        finally
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }
        }
    }
}
