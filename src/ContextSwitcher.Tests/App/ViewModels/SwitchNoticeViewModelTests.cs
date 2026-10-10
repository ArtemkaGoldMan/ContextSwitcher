using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
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

    private static CurrentContextState Switched(string contextId, ContextSwitchStatus status, DateTimeOffset completedAt, params string[] errors) => new()
    {
        CurrentContextId = contextId,
        LastSwitchCompletedAt = completedAt,
        LastSwitchStatus = status.ToString(),
        LastErrors = errors.Select(message => new StateError { StepId = "step", Message = message, OccurredAt = completedAt }).ToList()
    };
}
