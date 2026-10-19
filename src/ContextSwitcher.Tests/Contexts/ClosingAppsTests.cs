using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.Core.Platform;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Contexts;

/// <summary>
/// An app a switch asked to quit that was still running when it finished: its line, and what comes of
/// it as the app goes, stays on, or is ended by force. A friend's Autodesk Fusion took half a minute
/// to quit, and his Steam closed its windows and stayed in the background - both were told as "it may
/// be asking to save", which neither was.
/// </summary>
public sealed class ClosingAppsTests : IDisposable
{
    private static readonly DateTimeOffset CompletedAt = new(2026, 10, 15, 12, 9, 5, TimeSpan.Zero);

    // A folder of its own: the switch lock is a real file beside the state, and the other tests'
    // switches must not hold it.
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"cs-closing-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(this.folder))
        {
            Directory.Delete(this.folder, recursive: true);
        }
    }

    [Fact]
    public void TheAppsStillRunningAreTheOnesTheSwitchSaidSo()
    {
        IReadOnlyList<StateError> errors =
        [
            Line(SwitchMessages.StillClosing, "Autodesk Fusion"),
            Line(SwitchMessages.CouldNotClose, "TextEdit"),
            Line(SwitchMessages.CouldNotLaunch, "Slack"),
            new StateError { StepId = "old", Message = "Didn't quit: Notes, Mail." }
        ];

        Assert.Equal(["Autodesk Fusion", "TextEdit"], ClosingApps.StillRunning(errors));
        Assert.True(ClosingApps.WasClosing(errors, "autodesk fusion"));
        Assert.False(ClosingApps.WasClosing(errors, "TextEdit"));
    }

    [Fact]
    public void AnAppThatQuitTakesOnlyItsOwnLineAway()
    {
        IReadOnlyList<StateError> errors = [Line(SwitchMessages.StillClosing, "Autodesk Fusion"), Line(SwitchMessages.CouldNotClose, "TextEdit"), Line(SwitchMessages.CouldNotLaunch, "Autodesk Fusion")];

        IReadOnlyList<StateError> after = ClosingApps.Quit(errors, "Autodesk Fusion");

        Assert.Equal([SwitchMessages.CouldNotClose.Key, SwitchMessages.CouldNotLaunch.Key], after.Select(error => error.MessageKey));
    }

    [Fact]
    public void AnAppStillThereSaysWhetherItHasAWindow()
    {
        IReadOnlyList<StateError> errors = [Line(SwitchMessages.StillClosing, "Steam"), Line(SwitchMessages.StillClosing, "Autodesk Fusion")];

        IReadOnlyList<StateError> after = ClosingApps.Stayed(ClosingApps.Stayed(errors, "Steam", hasWindow: false), "Autodesk Fusion", hasWindow: true);

        Assert.Equal("Steam still hasn't quit: it is running in the background, or its window is on another desktop.", after[0].Message);
        Assert.Equal(["Steam"], after[0].MessageArguments);
        Assert.Equal("Steam", ClosingApps.ForceQuittable(after[0]));
        Assert.Equal(SwitchMessages.CouldNotClose.Key, after[1].MessageKey);
        Assert.Null(ClosingApps.ForceQuittable(after[1]));
    }

    [Fact]
    public void ALineThatAlreadySaysSoIsLeftAsItIs()
    {
        StateError line = Line(SwitchMessages.CouldNotClose, "TextEdit");

        Assert.Same(line, ClosingApps.Stayed([line], "TextEdit", hasWindow: true)[0]);
    }

    [Fact]
    public async Task TheLastSwitchsWarningsAreAmendedAndWithNoneLeftItHasNone()
    {
        (ContextSwitchService service, InMemoryJsonStore store, string statePath) = this.Service();
        store.Seed(statePath, Switch(Line(SwitchMessages.StillClosing, "Autodesk Fusion")));

        CurrentContextState? amended = await service.AmendLastSwitchAsync(CompletedAt, errors => ClosingApps.Quit(errors, "Autodesk Fusion"), CancellationToken.None);

        Assert.NotNull(amended);
        CurrentContextState written = store.Get<CurrentContextState>(statePath)!;
        Assert.Empty(written.LastErrors);
        Assert.Equal(nameof(ContextSwitchStatus.Succeeded), written.LastSwitchStatus);
        Assert.Equal(CompletedAt, written.LastSwitchCompletedAt);
    }

    [Fact]
    public async Task ANewerSwitchsStateIsNeverAmended()
    {
        (ContextSwitchService service, InMemoryJsonStore store, string statePath) = this.Service();
        CurrentContextState newer = Switch(Line(SwitchMessages.StillClosing, "Autodesk Fusion")) with { LastSwitchCompletedAt = CompletedAt.AddMinutes(1) };
        store.Seed(statePath, newer);

        CurrentContextState? amended = await service.AmendLastSwitchAsync(CompletedAt, errors => [], CancellationToken.None);

        Assert.Null(amended);
        Assert.Single(store.Get<CurrentContextState>(statePath)!.LastErrors);
    }

    [Fact]
    public async Task AnAppThatQuitsLaterHasItsLineTakenAway()
    {
        (ContextSwitchService service, InMemoryJsonStore store, string statePath) = this.Service();
        CurrentContextState state = Switch(Line(SwitchMessages.StillClosing, "Autodesk Fusion"), Line(SwitchMessages.CouldNotLaunch, "Slack"));
        store.Seed(statePath, state);
        FakeAppProcesses processes = new FakeAppProcesses().Running("Autodesk Fusion", quitsAfterLooks: 3);
        ClosingAppsFollowUp followUp = FollowUp(service, processes);
        List<CurrentContextState> published = [];
        followUp.StateAmended += (_, amended) => published.Add(amended);

        followUp.Watch(state);
        await followUp.Watching;

        CurrentContextState written = store.Get<CurrentContextState>(statePath)!;
        Assert.Equal([SwitchMessages.CouldNotLaunch.Key], written.LastErrors.Select(error => error.MessageKey));
        Assert.Equal(nameof(ContextSwitchStatus.SucceededWithWarnings), written.LastSwitchStatus);
        Assert.Single(published);
    }

    [Fact]
    public async Task AnAppStillRunningWithNoWindowStayedInTheBackground()
    {
        (ContextSwitchService service, InMemoryJsonStore store, string statePath) = this.Service();
        CurrentContextState state = Switch(Line(SwitchMessages.StillClosing, "Steam"), Line(SwitchMessages.StillClosing, "Autodesk Fusion"));
        store.Seed(statePath, state);
        FakeAppProcesses processes = new FakeAppProcesses().Running("Steam").Running("Autodesk Fusion", hasWindow: true);
        ClosingAppsFollowUp followUp = FollowUp(service, processes);

        followUp.Watch(state);
        await followUp.Watching;

        IReadOnlyList<StateError> lines = store.Get<CurrentContextState>(statePath)!.LastErrors;
        Assert.Equal(SwitchMessages.StayedInBackground.Key, lines[0].MessageKey);
        Assert.Equal(SwitchMessages.CouldNotClose.Key, lines[1].MessageKey);
        Assert.Empty(processes.ForceQuitAsked);
    }

    /// <summary>
    /// An app the switch said didn't quit, which the running apps don't know by that name, may just be
    /// called something else there: it is not taken for gone. One it said was still closing was found
    /// then, so not finding it now is gone.
    /// </summary>
    [Fact]
    public async Task OnlyAnAppSeenGoingIsTakenForGone()
    {
        (ContextSwitchService service, InMemoryJsonStore store, string statePath) = this.Service();
        CurrentContextState state = Switch(Line(SwitchMessages.CouldNotClose, "Code"), Line(SwitchMessages.StillClosing, "Autodesk Fusion"));
        store.Seed(statePath, state);
        FakeAppProcesses processes = new FakeAppProcesses().Gone("Autodesk Fusion").Gone("Code");
        ClosingAppsFollowUp followUp = FollowUp(service, processes);

        followUp.Watch(state);
        await followUp.Watching;

        Assert.Equal([SwitchMessages.CouldNotClose.Key], store.Get<CurrentContextState>(statePath)!.LastErrors.Select(error => error.MessageKey));
    }

    [Fact]
    public async Task ANewerSwitchStopsTheWatchOnTheOlderOne()
    {
        (ContextSwitchService service, InMemoryJsonStore store, string statePath) = this.Service();
        CurrentContextState older = Switch(Line(SwitchMessages.StillClosing, "Steam"));
        CurrentContextState newer = older with { LastSwitchCompletedAt = CompletedAt.AddSeconds(30), LastErrors = [Line(SwitchMessages.CouldNotLaunch, "Slack")] };
        store.Seed(statePath, newer);
        ClosingAppsFollowUp followUp = FollowUp(service, new FakeAppProcesses().Running("Steam"), watchFor: TimeSpan.FromSeconds(30));

        followUp.Watch(older);
        Task olderWatch = followUp.Watching;
        followUp.Watch(newer);
        await olderWatch.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(newer.LastErrors, store.Get<CurrentContextState>(statePath)!.LastErrors);
    }

    [Fact]
    public async Task AForceQuitThatWorkedTakesTheLineAway()
    {
        (ContextSwitchService service, InMemoryJsonStore store, string statePath) = this.Service();
        store.Seed(statePath, Switch(Line(SwitchMessages.StayedInBackground, "Steam")));
        FakeAppProcesses processes = new FakeAppProcesses().Running("Steam");

        bool gone = await FollowUp(service, processes).ForceQuitAsync(CompletedAt, "Steam", CancellationToken.None);

        Assert.True(gone);
        Assert.Equal(["Steam"], processes.ForceQuitAsked);
        Assert.Empty(store.Get<CurrentContextState>(statePath)!.LastErrors);
    }

    [Fact]
    public async Task AForceQuitThatDidNotWorkSaysSo()
    {
        (ContextSwitchService service, InMemoryJsonStore store, string statePath) = this.Service();
        store.Seed(statePath, Switch(Line(SwitchMessages.StayedInBackground, "Steam")));
        FakeAppProcesses processes = new FakeAppProcesses().Running("Steam", ignoresForceQuit: true);

        bool gone = await FollowUp(service, processes).ForceQuitAsync(CompletedAt, "Steam", CancellationToken.None);

        Assert.False(gone);
        StateError line = Assert.Single(store.Get<CurrentContextState>(statePath)!.LastErrors);
        Assert.Equal("Could not force Steam to quit.", line.Message);
    }

    private static ClosingAppsFollowUp FollowUp(ContextSwitchService service, FakeAppProcesses processes, TimeSpan? watchFor = null) =>
        new(service, processes, new FakeClock(), new TestLogger(), watchFor ?? TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(200));

    private (ContextSwitchService Service, InMemoryJsonStore Store, string StatePath) Service()
    {
        InMemoryJsonStore store = new();
        FakeClock clock = new();
        string statePath = Path.Combine(this.folder, "state.json");
        ContextSwitchService service = new(
            store,
            new FakeAutomationStepExecutor(),
            new AutomationPlanBuilder(PlatformCapabilities.MacOS),
            new AnalyticsService(clock, new InMemoryAnalyticsSessionStore()),
            clock,
            new TestLogger(),
            new FakeSwitchLock(),
            Path.Combine(this.folder, "settings.json"),
            statePath);
        return (service, store, statePath);
    }

    private static CurrentContextState Switch(params StateError[] errors) => new()
    {
        CurrentContextId = "work",
        LastSwitchCompletedAt = CompletedAt,
        LastSwitchStatus = nameof(ContextSwitchStatus.SucceededWithWarnings),
        LastErrors = errors
    };

    private static StateError Line(MessageTemplate template, string app)
    {
        UserMessage message = template.With(app);
        return new StateError { StepId = "CloseApplications.after-work", Message = message.English, MessageKey = message.Key, MessageArguments = message.Arguments, OccurredAt = CompletedAt };
    }
}
