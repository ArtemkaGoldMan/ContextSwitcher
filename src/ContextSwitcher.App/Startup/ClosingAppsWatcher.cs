using Avalonia.Threading;
using ContextSwitcher.App.Services;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Contexts;

namespace ContextSwitcher.App.Startup;

/// <summary>
/// An app a switch asked to quit that was still going when it finished is watched for a couple of
/// minutes, and its line in the warnings follows what it does. This hands the follow-up each switch
/// that finishes from the start on - wherever it was started, the command line included - and
/// publishes what it changes, as long as that is still the last switch: one that finished meanwhile
/// has warnings of its own.
/// </summary>
public sealed class ClosingAppsWatcher(ClosingAppsFollowUp followUp, IAppState appState, IClock clock) : IHousekeeping
{
    private DateTimeOffset? watched;

    /// <inheritdoc />
    public void Start()
    {
        DateTimeOffset since = clock.UtcNow;
        appState.StateChanged += (_, _) =>
        {
            if (appState.State.LastSwitchCompletedAt is { } completedAt && completedAt >= since && completedAt != this.watched)
            {
                this.watched = completedAt;
                followUp.Watch(appState.State);
            }
        };

        followUp.StateAmended += (_, amended) => Dispatcher.UIThread.Post(() =>
        {
            if (appState.State.LastSwitchCompletedAt == amended.LastSwitchCompletedAt)
            {
                appState.UpdateState(amended);
            }
        });
    }
}
