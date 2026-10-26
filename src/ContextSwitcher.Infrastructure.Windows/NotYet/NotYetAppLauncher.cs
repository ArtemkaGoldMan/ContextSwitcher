using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Windows.NotYet;

/// <summary>Opening apps on Windows comes with the apps part of stage 1; until then it fails, saying why.</summary>
public sealed class NotYetAppLauncher : IAppLauncher
{
    /// <inheritdoc />
    public Task<AppLaunch> LaunchAsync(string app, TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.FromResult(new AppLaunch(AppLaunchOutcome.Failed, NotYetReason.Apps));
}
