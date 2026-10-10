using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Updates;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>Records what it is asked to install instead of touching any app.</summary>
public sealed class FakeAppUpdater : IAppUpdater
{
    public string? Blocker { get; set; }

    public UpdateException? StageFailure { get; set; }

    public StagedUpdate? Installed { get; private set; }

    public Task<string?> GetInstallBlockerAsync(CancellationToken cancellationToken) => Task.FromResult(this.Blocker);

    public Task<StagedUpdate> StageAsync(ReleaseInfo release, CancellationToken cancellationToken) =>
        this.StageFailure is { } failure
            ? Task.FromException<StagedUpdate>(failure)
            : Task.FromResult(new StagedUpdate(release.Version, "/tmp/staged/ContextSwitcher.app", "/tmp/staged"));

    public void InstallOnExit(StagedUpdate update) => this.Installed = update;
}
