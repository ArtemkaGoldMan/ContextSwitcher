using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;

namespace ContextSwitcher.App.Services;

/// <summary>
/// Every switch started in the app - the menu bar, the popover, the Profiles page, the setup wizard -
/// goes through here. With <see cref="AppConfiguration.AskOnSwitch"/> on, it first finds the apps and
/// tabs open that neither profile in the switch has, asks what to do with them, adds what was added to
/// its profile, and hands the switch what to close besides. With it off - or nothing to ask about - the
/// switch goes straight through. Either way it then reads the state the switch wrote and publishes it,
/// so whatever started the switch, every window shows the outcome at once. The command line switches
/// through <see cref="ContextSwitchService"/> directly: it has no window to ask in or to update.
/// </summary>
public sealed class SwitchCoordinator : IContextSwitchService
{
    private readonly IContextSwitchService inner;
    private readonly ConfigurationStore configurationStore;
    private readonly IRunningApplications runningApps;
    private readonly ISystemCatalog catalog;
    private readonly IAskOnSwitchPresenter presenter;
    private readonly IJsonStore jsonStore;
    private readonly string statePath;
    private bool asking;

    /// <param name="inner">Runs the switch itself.</param>
    /// <param name="configurationStore">Saves what the person added to a profile when asked.</param>
    /// <param name="runningApps">The apps open now, to ask about.</param>
    /// <param name="catalog">The browser tabs open now, to ask about.</param>
    /// <param name="presenter">Shows the question.</param>
    /// <param name="jsonStore">Reads the state the switch wrote.</param>
    /// <param name="statePath">Where the switch writes its state: <c>state.json</c>.</param>
    public SwitchCoordinator(
        IContextSwitchService inner,
        ConfigurationStore configurationStore,
        IRunningApplications runningApps,
        ISystemCatalog catalog,
        IAskOnSwitchPresenter presenter,
        IJsonStore jsonStore,
        string statePath)
    {
        this.inner = inner;
        this.configurationStore = configurationStore;
        this.runningApps = runningApps;
        this.catalog = catalog;
        this.presenter = presenter;
        this.jsonStore = jsonStore;
        this.statePath = statePath;
    }

    /// <inheritdoc />
    public async Task<ContextSwitchResult> SwitchAsync(ContextSwitchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ContextSwitchResult result = await this.AskThenSwitchAsync(request, cancellationToken).ConfigureAwait(true);

        CurrentContextState? state = await this.jsonStore.ReadAsync<CurrentContextState>(this.statePath, cancellationToken).ConfigureAwait(true);
        AppHost.UpdateState(state ?? new CurrentContextState());
        return result;
    }

    private async Task<ContextSwitchResult> AskThenSwitchAsync(ContextSwitchRequest request, CancellationToken cancellationToken)
    {
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
