using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>The Mac's <c>open</c>: links in their app, files in Finder, and System Settings' panes.</summary>
public sealed class MacShell : IShell
{
    private const string AutomationSettingsUrl = "x-apple.systempreferences:com.apple.preference.security?Privacy_Automation";
    private const string FocusSettingsUrl = "x-apple.systempreferences:com.apple.Focus-Settings.extension";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>A mail link waits on the mail app opening, which can take longer than a page.</summary>
    private static readonly TimeSpan LinkTimeout = TimeSpan.FromSeconds(10);

    private readonly IProcessRunner processRunner;

    public MacShell(IProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(processRunner);

        this.processRunner = processRunner;
    }

    /// <inheritdoc />
    public async Task<bool> OpenLinkAsync(string url, CancellationToken cancellationToken)
    {
        ProcessResult opened = await this.processRunner.RunAsync(new ProcessStartOptions("open", [url], LinkTimeout), cancellationToken)
            .ConfigureAwait(false);
        return opened.ExitCode == 0;
    }

    /// <inheritdoc />
    public void ShowFile(string path)
    {
        IReadOnlyList<string> arguments = File.Exists(path) ? ["-R", path] : [Path.GetDirectoryName(path) ?? path];
        this.Open(arguments);
    }

    /// <inheritdoc />
    public void OpenSystemPage(SystemPage page) => this.Open(page switch
    {
        SystemPage.AutomationPermission => [AutomationSettingsUrl],
        SystemPage.FocusSettings => [FocusSettingsUrl],
        SystemPage.Shortcuts => ["-a", "Shortcuts"],
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, null)
    });

    private void Open(IReadOnlyList<string> arguments) =>
        _ = this.processRunner.RunAsync(new ProcessStartOptions("open", arguments, Timeout), CancellationToken.None);
}
