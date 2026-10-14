using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;

namespace ContextSwitcher.App.Services;

/// <summary>
/// Every switch started in the app - the menu bar, the popover, the Profiles page - goes through here.
/// With <see cref="AppConfiguration.AskOnSwitch"/> on, it first finds the apps and tabs open that
/// neither profile in the switch has, asks what to do with them, adds what was added to its profile,
/// and hands the switch what to close besides. With it off - or nothing to ask about - the switch
/// goes straight through. The command line switches through <see cref="ContextSwitchService"/>
/// directly: it has no window to ask in.
/// </summary>
public sealed class SwitchCoordinator : IContextSwitchService
{
    private readonly IContextSwitchService inner;
    private readonly ConfigurationStore configurationStore;
    private readonly IRunningApplications runningApps;
    private readonly ISystemCatalog catalog;
    private readonly IAskOnSwitchPresenter presenter;
    private bool asking;

    public SwitchCoordinator(
        IContextSwitchService inner,
        ConfigurationStore configurationStore,
        IRunningApplications runningApps,
        ISystemCatalog catalog,
        IAskOnSwitchPresenter presenter)
    {
        this.inner = inner;
        this.configurationStore = configurationStore;
        this.runningApps = runningApps;
        this.catalog = catalog;
        this.presenter = presenter;
    }

    /// <inheritdoc />
    public async Task<ContextSwitchResult> SwitchAsync(ContextSwitchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        AppConfiguration configuration = AppHost.Configuration;
        if (!configuration.AskOnSwitch || request.DryRun || request.Extras is not null)
        {
            return await this.inner.SwitchAsync(request, cancellationToken).ConfigureAwait(true);
        }

        // One question at a time: a second switch while it's open is turned away, as a second switch
        // while one runs is.
        if (this.asking)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return new ContextSwitchResult(request.TargetContextId, AppHost.State.CurrentContextId, ContextSwitchStatus.Cancelled, [], now, now, request.CorrelationId ?? Guid.NewGuid().ToString("N"));
        }

        string? currentId = string.IsNullOrEmpty(AppHost.State.CurrentContextId) ? null : AppHost.State.CurrentContextId;
        ContextDefinition? entering = request.IsTurnOff ? null : configuration.Contexts.FirstOrDefault(context => context.Id == request.TargetContextId);
        bool stays = !request.IsTurnOff && request.TargetContextId == currentId && !request.Force;
        ContextDefinition? leaving = stays ? null : configuration.Contexts.FirstOrDefault(context => context.Id == currentId);
        if (stays || (leaving is null && entering is null))
        {
            return await this.inner.SwitchAsync(request, cancellationToken).ConfigureAwait(true);
        }

        this.asking = true;
        AskOnSwitchResult answer;
        try
        {
            IReadOnlyList<RunningApp> apps = this.runningApps.List();
            IReadOnlyList<OpenBrowserTab> tabs = await this.catalog.GetBrowserTabsAsync(cancellationToken).ConfigureAwait(true);
            Unclaimed unclaimed = UnclaimedItems.Find(apps, tabs, leaving, entering, configuration.IgnoredApps);
            if (unclaimed.IsEmpty)
            {
                this.asking = false;
                return await this.inner.SwitchAsync(request, cancellationToken).ConfigureAwait(true);
            }

            answer = await this.presenter.AskAsync(new AskOnSwitchViewModel(unclaimed, leaving, entering)).ConfigureAwait(true);
        }
        finally
        {
            this.asking = false;
        }

        // Built on the configuration as it is now, not as it was when the window opened.
        AppConfiguration updated = AppHost.Configuration;
        if (answer.DontAskAgain)
        {
            updated = updated with { AskOnSwitch = false };
        }

        if (!answer.Skipped && answer.Answers.AddsAnything)
        {
            updated = answer.Answers.ApplyTo(updated, leaving?.Id, entering?.Id);
        }

        if (!ReferenceEquals(updated, AppHost.Configuration))
        {
            // Saved before the switch reads it, so a profile given an app closes or opens it now.
            await this.configurationStore.SaveAsync(updated, cancellationToken).ConfigureAwait(true);
        }

        return await this.inner
            .SwitchAsync(answer.Skipped ? request : request with { Extras = answer.Answers.ToExtras() }, cancellationToken)
            .ConfigureAwait(true);
    }
}
