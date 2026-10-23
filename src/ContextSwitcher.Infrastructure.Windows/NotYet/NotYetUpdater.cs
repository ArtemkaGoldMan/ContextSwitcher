using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.Core.Updates;

namespace ContextSwitcher.Infrastructure.Windows.NotYet;

/// <summary>
/// Updating on Windows comes with its installer, at the end of stage 1. Until then no copy is an
/// installed one, so there is nothing for an update to replace, and Settings offers the release page.
/// </summary>
public sealed class NotYetUpdater : IAppUpdater
{
    /// <inheritdoc />
    public Task<UserMessage?> GetInstallBlockerAsync(CancellationToken cancellationToken) =>
        Task.FromResult<UserMessage?>(UpdateMessages.NotInstalledApp.With());

    /// <inheritdoc />
    public Task<StagedUpdate> StageAsync(ReleaseInfo release, CancellationToken cancellationToken) =>
        throw new UpdateException(UpdateMessages.NotInstalledApp.With());

    /// <inheritdoc />
    public void InstallOnExit(StagedUpdate update) => throw new UpdateException(UpdateMessages.NotInstalledApp.With());
}
