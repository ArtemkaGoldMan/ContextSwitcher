using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Infrastructure.Windows;

/// <summary>
/// Windows' File Explorer: a link opens in its app - a page in the default browser, a mail link in the
/// mail app - and a file is shown selected in its folder. The system pages the Mac opens - the
/// Automation permission, Focus, Shortcuts - have no Windows counterpart, and the app offers none.
/// </summary>
public sealed class WindowsShell : IShell
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>A mail link waits on the mail app opening, which can take longer than a page.</summary>
    private static readonly TimeSpan LinkTimeout = TimeSpan.FromSeconds(10);

    private readonly IProcessRunner processRunner;

    public WindowsShell(IProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(processRunner);

        this.processRunner = processRunner;
    }

    /// <inheritdoc />
    public async Task<bool> OpenLinkAsync(string url, CancellationToken cancellationToken)
    {
        // File Explorer hands the link to its app and ends with 1 whether that worked or not, so its
        // exit code says nothing; only it not ending does.
        ProcessResult opened = await this.processRunner.RunAsync(new ProcessStartOptions("explorer", [url], LinkTimeout), cancellationToken)
            .ConfigureAwait(false);
        return !opened.TimedOut;
    }

    /// <inheritdoc />
    public void ShowFile(string path)
    {
        IReadOnlyList<string> arguments = File.Exists(path) ? [$"/select,{path}"] : [Path.GetDirectoryName(path) ?? path];
        _ = this.processRunner.RunAsync(new ProcessStartOptions("explorer", arguments, Timeout), CancellationToken.None);
    }

    /// <inheritdoc />
    public void OpenSystemPage(SystemPage page)
    {
        // Not logged: no Windows page answers to these, and the app shows nothing that opens one.
    }
}
