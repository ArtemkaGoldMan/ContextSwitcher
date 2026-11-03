using System.Security;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Infrastructure.Windows.Startup;

/// <summary>
/// Opening at sign-in the way Windows apps do it: a command in the current user's <c>Run</c> key, which
/// Startup apps in Settings and the Task Manager list, and where the person can turn it off. Turning it
/// off there is noted in <c>StartupApproved\Run</c>, which this reads: an entry that is there but turned
/// off is "needs approval", as on the Mac, and the person turning it on here is the approval.
///
/// The command is the path of the running executable, quoted. An entry that names another path - the
/// app was moved, or another copy wrote it - is shown as off, so that turning it on writes this one. A
/// build run from the code is never offered it.
/// </summary>
public sealed class WindowsLoginItem : ILoginItem
{
    private static readonly TimeSpan SettingsTimeout = TimeSpan.FromSeconds(10);

    private readonly IStartupEntries entries;
    private readonly LoginItemOptions options;
    private readonly string? executablePath;
    private readonly IProcessRunner processRunner;

    /// <summary>Opening at sign-in for the app at <paramref name="executablePath"/>, as Windows keeps it.</summary>
    /// <param name="entries">What opens at sign-in, as Windows keeps it.</param>
    /// <param name="options">Where, and which builds aren't offered it.</param>
    /// <param name="executablePath">The path of the running executable; null when it can't be told.</param>
    /// <param name="processRunner">Opens Settings at Startup apps.</param>
    public WindowsLoginItem(IStartupEntries entries, LoginItemOptions options, string? executablePath, IProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(processRunner);

        this.entries = entries;
        this.options = options;
        this.executablePath = executablePath;
        this.processRunner = processRunner;
    }

    /// <inheritdoc />
    public LoginItemStatus Status
    {
        get
        {
            if (!this.IsInstalledApp(out string command))
            {
                return LoginItemStatus.Unavailable;
            }

            try
            {
                if (!string.Equals(this.entries.Command(this.options.ValueName), command, StringComparison.OrdinalIgnoreCase))
                {
                    return LoginItemStatus.Off;
                }

                return TurnedOff(this.entries.Approval(this.options.ValueName)) ? LoginItemStatus.NeedsApproval : LoginItemStatus.On;
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
            {
                // Not logged: asked each time Settings is drawn, and an unreadable key reads as
                // not offered - the person is told nothing wrong, and nothing is written.
                return LoginItemStatus.Unavailable;
            }
        }
    }

    /// <inheritdoc />
    public string? SetEnabled(bool enabled)
    {
        if (!this.IsInstalledApp(out string command))
        {
            return "Opening at sign-in is only offered to the installed app.";
        }

        try
        {
            if (enabled)
            {
                this.entries.SetCommand(this.options.ValueName, command);
            }
            else
            {
                this.entries.RemoveCommand(this.options.ValueName);
            }

            // On: the person asked here, which is as good as turning it back on in Startup apps. Off:
            // nothing is left there to be turned off.
            this.entries.RemoveApproval(this.options.ValueName);
            return null;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return $"Windows wouldn't change what opens at sign-in: {ex.Message}";
        }
    }

    /// <inheritdoc />
    public void OpenSystemSettings() =>
        _ = this.processRunner.RunAsync(new ProcessStartOptions("explorer", ["ms-settings:startupapps"], SettingsTimeout), CancellationToken.None);

    /// <summary>The first byte of what Startup apps keeps is odd for an entry the person turned off there (3, or 7), even for one that is on (2, or 6).</summary>
    public static bool TurnedOff(byte[]? approval) => approval is { Length: > 0 } && (approval[0] & 1) == 1;

    /// <summary>Whether this is an installed app, and the command that opens it: its path, in quotes, as a path with a space needs.</summary>
    private bool IsInstalledApp(out string command)
    {
        command = string.Empty;
        string? path = this.executablePath;
        if (string.IsNullOrWhiteSpace(path)
            || !Path.IsPathRooted(path)
            || !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFileName(path), "dotnet.exe", StringComparison.OrdinalIgnoreCase)
            || this.options.BuildFolders.Any(folder => path.Contains(folder, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        command = $"\"{path}\"";
        return true;
    }
}
