using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Infrastructure.Windows;

/// <summary>
/// Windows has no Shortcuts app, and its Focus can't be turned on by another app without Microsoft's
/// leave, so a profile has no Focus there and no step runs a Shortcut. Should one be asked for all the
/// same, it isn't there.
/// </summary>
public sealed class NoShortcuts : IShortcutRunner, IFocusShortcutInstaller
{
    private const string NoShortcutsApp = "Windows has no Shortcuts app.";

    /// <inheritdoc />
    public Task<ShortcutRun> RunAsync(string name, TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.FromResult(new ShortcutRun(ShortcutRunOutcome.Missing, NoShortcutsApp, new ProcessResult(-1, string.Empty, NoShortcutsApp, TimedOut: false)));

    /// <inheritdoc />
    public Task<UserMessage?> OfferAsync(string modeName, bool turnOff, CancellationToken cancellationToken) =>
        Task.FromResult<UserMessage?>(FocusMessages.CouldNotOpenShortcuts.With());
}
