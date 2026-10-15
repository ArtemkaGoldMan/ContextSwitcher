using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.Tests.App;

namespace ContextSwitcher.Tests.App.ViewModels;

[Collection(AppHostTestCollection.Name)]
public sealed class SwitchNoticeViewModelTests
{
    private static readonly DateTimeOffset Started = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    public SwitchNoticeViewModelTests()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts =
            [
                new ContextDefinition { Id = "work", DisplayName = "Work", AccentColor = "#2F6FED" },
                new ContextDefinition { Id = "personal", DisplayName = "Personal", AccentColor = "#20A67A" }
            ]
        };
        AppHost.UpdateConfiguration(configuration, new ConfigurationValidator().Validate(configuration));
        AppHost.UpdateState(new CurrentContextState { CurrentContextId = "work" });
    }

    [Fact]
    public void ASwitchWithWarningsShowsThemUnderTheProfilesName()
    {
        using SwitchNoticeViewModel notice = new(Started);

        AppHost.UpdateState(Switched("personal", ContextSwitchStatus.SucceededWithWarnings, Started.AddMinutes(5), "Could not close Slack."));

        Assert.True(notice.IsShown);
        Assert.False(notice.IsFailure);
        Assert.Equal("Switched to Personal, with warnings", notice.Title);
        Assert.Equal(["Could not close Slack."], notice.Messages);
    }

    [Fact]
    public void AFailedSwitchShowsAsAFailure()
    {
        using SwitchNoticeViewModel notice = new(Started);

        AppHost.UpdateState(Switched("personal", ContextSwitchStatus.Failed, Started.AddMinutes(5), "Docker container 'db' did not start."));

        Assert.True(notice.IsShown);
        Assert.True(notice.IsFailure);
        Assert.Equal("Couldn't switch to Personal", notice.Title);
    }

    [Fact]
    public void ACleanSwitchShowsNothing()
    {
        using SwitchNoticeViewModel notice = new(Started);

        AppHost.UpdateState(Switched("personal", ContextSwitchStatus.Succeeded, Started.AddMinutes(5)));

        Assert.False(notice.IsShown);
    }

    /// <summary>The next switch replaces the notice: a clean one takes it away.</summary>
    [Fact]
    public void TheNextCleanSwitchTakesTheNoticeAway()
    {
        using SwitchNoticeViewModel notice = new(Started);
        AppHost.UpdateState(Switched("personal", ContextSwitchStatus.SucceededWithWarnings, Started.AddMinutes(5), "Could not close Slack."));

        AppHost.UpdateState(Switched("work", ContextSwitchStatus.Succeeded, Started.AddMinutes(9)));

        Assert.False(notice.IsShown);
    }

    /// <summary>
    /// A switch from before the app started is not news - the notice would otherwise greet every
    /// launch with whatever went wrong yesterday.
    /// </summary>
    [Fact]
    public void ProblemsFromBeforeTheAppStartedAreNotShown()
    {
        AppHost.UpdateState(Switched("personal", ContextSwitchStatus.SucceededWithWarnings, Started.AddHours(-20), "Could not close Slack."));

        using SwitchNoticeViewModel notice = new(Started);
        AppHost.UpdateState(AppHost.State with { });

        Assert.False(notice.IsShown);
    }

    /// <summary>
    /// The state is published again every time it is re-read, which is not a new switch: a closed
    /// notice stays closed until a switch actually happens.
    /// </summary>
    [Fact]
    public void AClosedNoticeStaysClosedUntilTheNextSwitch()
    {
        using SwitchNoticeViewModel notice = new(Started);
        AppHost.UpdateState(Switched("personal", ContextSwitchStatus.SucceededWithWarnings, Started.AddMinutes(5), "Could not close Slack."));

        notice.DismissCommand.Execute(null);
        AppHost.UpdateState(AppHost.State with { });

        Assert.False(notice.IsShown);

        AppHost.UpdateState(Switched("work", ContextSwitchStatus.SucceededWithWarnings, Started.AddMinutes(9), "Could not quit Music."));

        Assert.True(notice.IsShown);
        Assert.Equal(["Could not quit Music."], notice.Messages);
    }

    /// <summary>A switch turned away before it ran wrote no state, so it is reported directly.</summary>
    [Fact]
    public void ARejectedSwitchIsShownUntilTheNextSwitch()
    {
        using SwitchNoticeViewModel notice = new(Started);

        notice.ShowRejected("Couldn't switch to Personal", "Another switch was still running.", Started.AddMinutes(1));
        AppHost.UpdateState(AppHost.State with { });

        Assert.True(notice.IsShown);
        Assert.True(notice.IsFailure);

        AppHost.UpdateState(Switched("personal", ContextSwitchStatus.Succeeded, Started.AddMinutes(2)));

        Assert.False(notice.IsShown);
    }


    /// <summary>Problems from turning a profile off are told as that, under the profile turned off.</summary>
    [Fact]
    public void ProblemsTurningAProfileOffAreToldAsThat()
    {
        using SwitchNoticeViewModel notice = new(Started);

        AppHost.UpdateState(Switched(string.Empty, ContextSwitchStatus.SucceededWithWarnings, Started.AddMinutes(5), "Could not close Slack.") with { PreviousContextId = "work" });
        Assert.Equal("Turned off Work, with warnings", notice.Title);

        AppHost.UpdateState(Switched(string.Empty, ContextSwitchStatus.Failed, Started.AddMinutes(6), "Could not close Slack.") with { PreviousContextId = "work" });
        Assert.Equal("Couldn't turn off Work", notice.Title);
    }
    /// <summary>
    /// An app the switch left closing goes, or stays on in the background, after the card is up: the
    /// card follows, offers to end the one in the background, and goes when nothing is left.
    /// </summary>
    [Fact]
    public async Task TheOpenCardFollowsItsSwitchAsAppsQuitOrStay()
    {
        List<(DateTimeOffset, string)> forceQuit = [];
        using SwitchNoticeViewModel notice = new(Started, (completedAt, app) =>
        {
            forceQuit.Add((completedAt, app));
            return Task.CompletedTask;
        });
        DateTimeOffset completedAt = Started.AddMinutes(5);

        AppHost.UpdateState(Keyed(completedAt, SwitchMessages.StillClosing.With("Autodesk Fusion"), SwitchMessages.StillClosing.With("Steam")));
        Assert.Equal(2, notice.Lines.Count);
        Assert.DoesNotContain(notice.Lines, line => line.CanForceQuit);

        AppHost.UpdateState(Keyed(completedAt, SwitchMessages.StayedInBackground.With("Steam")));
        NoticeLine steam = Assert.Single(notice.Lines);
        Assert.Equal(["Steam still hasn't quit: it is running in the background, or its window is on another desktop."], notice.Messages);
        Assert.True(steam.CanForceQuit);
        Assert.Equal("Force quit Steam", steam.ForceQuitName);
        await steam.ForceQuitCommand!.ExecuteAsync(null);
        Assert.Equal([(completedAt, "Steam")], forceQuit);

        AppHost.UpdateState(Keyed(completedAt) with { LastSwitchStatus = nameof(ContextSwitchStatus.Succeeded) });
        Assert.False(notice.IsShown);
    }

    [Fact]
    public void AClosedCardStaysClosedWhenItsSwitchChanges()
    {
        using SwitchNoticeViewModel notice = new(Started);
        DateTimeOffset completedAt = Started.AddMinutes(5);
        AppHost.UpdateState(Keyed(completedAt, SwitchMessages.StillClosing.With("Steam")));

        notice.DismissCommand.Execute(null);
        AppHost.UpdateState(Keyed(completedAt, SwitchMessages.StayedInBackground.With("Steam")));

        Assert.False(notice.IsShown);
    }

    [Fact]
    public void ACardSayingASwitchWasTurnedAwayKeepsSayingIt()
    {
        DateTimeOffset completedAt = Started.AddMinutes(5);
        AppHost.UpdateState(Keyed(completedAt, SwitchMessages.StillClosing.With("Steam")));
        using SwitchNoticeViewModel notice = new(Started);

        notice.ShowRejected("Couldn't switch to Work", "Another switch is already running.", Started.AddMinutes(6));
        AppHost.UpdateState(Keyed(completedAt, SwitchMessages.StayedInBackground.With("Steam")));

        Assert.Equal(["Another switch is already running."], notice.Messages);
    }

    private static CurrentContextState Keyed(DateTimeOffset completedAt, params UserMessage[] problems) => new()
    {
        CurrentContextId = "personal",
        LastSwitchCompletedAt = completedAt,
        LastSwitchStatus = nameof(ContextSwitchStatus.SucceededWithWarnings),
        LastErrors = problems.Select(problem => new StateError
        {
            StepId = "CloseApplications.work",
            Message = problem.English,
            MessageKey = problem.Key,
            MessageArguments = problem.Arguments,
            OccurredAt = completedAt
        }).ToList()
    };

    private static CurrentContextState Switched(string contextId, ContextSwitchStatus status, DateTimeOffset completedAt, params string[] errors) => new()
    {
        CurrentContextId = contextId,
        LastSwitchCompletedAt = completedAt,
        LastSwitchStatus = status.ToString(),
        LastErrors = errors.Select(message => new StateError { StepId = "step", Message = message, OccurredAt = completedAt }).ToList()
    };
}
