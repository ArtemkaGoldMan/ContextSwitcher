using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Contexts;

public sealed class ContextSwitchServiceTests
{
    private const string SettingsPath = "settings.json";
    private const string StatePath = "state.json";

    [Fact]
    public async Task SwitchAsyncReturnsNoOpWhenTargetIsAlreadyActive()
    {
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor executor, _, _) = CreateService();
        store.Seed(SettingsPath, TwoContextConfiguration());
        store.Seed(StatePath, new CurrentContextState { CurrentContextId = "work" });

        ContextSwitchResult result = await service.SwitchAsync(
            new ContextSwitchRequest("work", ContextSwitchSource.Test),
            CancellationToken.None);

        Assert.Equal(ContextSwitchStatus.NoOp, result.Status);
        Assert.Empty(result.StepResults);
        Assert.Empty(executor.ExecutedSteps);
    }

    [Fact]
    public async Task SwitchAsyncRejectsConcurrentSwitch()
    {
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor executor, _, _) = CreateService();
        store.Seed(SettingsPath, TwoContextConfiguration());
        store.Seed(StatePath, new CurrentContextState { CurrentContextId = "personal" });

        executor.Entered = new TaskCompletionSource();
        executor.Gate = new TaskCompletionSource();

        Task<ContextSwitchResult> firstSwitch = service.SwitchAsync(
            new ContextSwitchRequest("work", ContextSwitchSource.Test),
            CancellationToken.None);

        await executor.Entered.Task;

        ContextSwitchResult second = await service.SwitchAsync(
            new ContextSwitchRequest("work", ContextSwitchSource.Test),
            CancellationToken.None);

        Assert.Equal(ContextSwitchStatus.Cancelled, second.Status);

        executor.Gate.SetResult();
        ContextSwitchResult first = await firstSwitch;
        Assert.Equal(ContextSwitchStatus.Succeeded, first.Status);
    }

    [Fact]
    public async Task SwitchAsyncFailsWhenCriticalStepFails()
    {
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor executor, _, _) = CreateService();
        AppConfiguration configuration = TwoContextConfiguration(criticalSteps: ["LaunchApplications"]);
        store.Seed(SettingsPath, configuration);
        store.Seed(StatePath, new CurrentContextState { CurrentContextId = "personal" });

        DateTimeOffset now = DateTimeOffset.UtcNow;
        executor.SetResult(
            "LaunchApplications.work",
            new AutomationResult("LaunchApplications.work", AutomationStepType.LaunchApplications, AutomationResultStatus.Failed, "boom", 1, null, null, now, now));

        ContextSwitchResult result = await service.SwitchAsync(
            new ContextSwitchRequest("work", ContextSwitchSource.Test),
            CancellationToken.None);

        Assert.Equal(ContextSwitchStatus.Failed, result.Status);

        // The manage-browser-context step (which comes after LaunchApplications in the plan)
        // must not have run once the critical failure short-circuited the pipeline.
        Assert.DoesNotContain(executor.ExecutedSteps, step => step.Type == AutomationStepType.ManageBrowserContext);
    }

    [Fact]
    public async Task SwitchAsyncSucceedsWithWarningsWhenNonCriticalStepFails()
    {
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor executor, _, _) = CreateService();
        store.Seed(SettingsPath, TwoContextConfiguration());
        store.Seed(StatePath, new CurrentContextState { CurrentContextId = "personal" });

        DateTimeOffset now = DateTimeOffset.UtcNow;
        executor.SetResult(
            "LaunchApplications.work",
            new AutomationResult("LaunchApplications.work", AutomationStepType.LaunchApplications, AutomationResultStatus.Failed, "boom", 1, null, null, now, now));

        ContextSwitchResult result = await service.SwitchAsync(
            new ContextSwitchRequest("work", ContextSwitchSource.Test),
            CancellationToken.None);

        Assert.Equal(ContextSwitchStatus.SucceededWithWarnings, result.Status);

        // Non-critical failures continue the pipeline by default.
        Assert.Contains(executor.ExecutedSteps, step => step.Type == AutomationStepType.ManageBrowserContext);
        Assert.Single(result.StepResults, step => step.Status == AutomationResultStatus.Failed);
    }

    /// <summary>
    /// Each problem a step reports is its own entry in state.json, with its key and values, so the app
    /// can show it in the user's language; the English stays beside it for the log and older builds.
    /// </summary>
    [Fact]
    public async Task EachProblemIsRecordedWithItsKeyAndValues()
    {
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor executor, _, _) = CreateService();
        store.Seed(SettingsPath, TwoContextConfiguration());
        store.Seed(StatePath, new CurrentContextState { CurrentContextId = "personal" });

        DateTimeOffset now = DateTimeOffset.UtcNow;
        UserMessage tabs = SwitchMessages.CouldNotCheckTabs.With("Google Chrome");
        UserMessage page = SwitchMessages.CouldNotOpenUrl.With("https://example.com/", "denied");
        executor.SetResult(
            "ManageBrowserContext.work",
            new AutomationResult("ManageBrowserContext.work", AutomationStepType.ManageBrowserContext, AutomationResultStatus.Warning, $"{tabs.English} {page.English}", null, null, null, now, now)
            {
                Problems = [tabs, page]
            });

        await service.SwitchAsync(new ContextSwitchRequest("work", ContextSwitchSource.Test), CancellationToken.None);

        CurrentContextState state = store.Get<CurrentContextState>(StatePath)!;
        Assert.Collection(
            state.LastErrors,
            error =>
            {
                Assert.Equal("Switch_CouldNotCheckTabs", error.MessageKey);
                Assert.Equal(["Google Chrome"], error.MessageArguments);
                Assert.Equal(tabs.English, error.Message);
            },
            error =>
            {
                Assert.Equal("Switch_CouldNotOpenUrl", error.MessageKey);
                Assert.Equal(["https://example.com/", "denied"], error.MessageArguments);
            });
    }

    [Fact]
    public async Task SwitchAsyncDryRunSkipsExecutionAndDoesNotPersistState()
    {
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor executor, _, _) = CreateService();
        store.Seed(SettingsPath, TwoContextConfiguration());
        store.Seed(StatePath, new CurrentContextState { CurrentContextId = "personal" });

        ContextSwitchResult result = await service.SwitchAsync(
            new ContextSwitchRequest("work", ContextSwitchSource.Test, DryRun: true),
            CancellationToken.None);

        Assert.Equal(ContextSwitchStatus.Succeeded, result.Status);
        Assert.Empty(executor.ExecutedSteps);
        Assert.All(result.StepResults, step => Assert.Equal(AutomationResultStatus.Skipped, step.Status));

        CurrentContextState? state = store.Get<CurrentContextState>(StatePath);
        Assert.Equal("personal", state?.CurrentContextId);
    }

    [Fact]
    public async Task SwitchAsyncFailsWhenStateWriteFails()
    {
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor _, _, _) = CreateService();
        store.Seed(SettingsPath, TwoContextConfiguration());
        store.Seed(StatePath, new CurrentContextState { CurrentContextId = "personal" });
        store.WriteFailurePaths.Add(StatePath);

        ContextSwitchResult result = await service.SwitchAsync(
            new ContextSwitchRequest("work", ContextSwitchSource.Test),
            CancellationToken.None);

        Assert.Equal(ContextSwitchStatus.Failed, result.Status);
    }

    /// <summary>
    /// Section 12 asks the developer log to carry the step id, exit code and trimmed output for
    /// every step. Previously a switch logged only SwitchStarted/SwitchCompleted, so there was no
    /// way to tell afterwards which steps had run or how they ended.
    /// </summary>
    [Fact]
    public async Task SwitchAsyncLogsEveryStepWithItsIdExitCodeAndOutput()
    {
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor executor, _, TestLogger logger) = CreateService();
        store.Seed(SettingsPath, TwoContextConfiguration());
        store.Seed(StatePath, new CurrentContextState { CurrentContextId = "personal" });

        DateTimeOffset now = DateTimeOffset.UtcNow;
        executor.SetResult(
            "LaunchApplications.work",
            new AutomationResult(
                "LaunchApplications.work",
                AutomationStepType.LaunchApplications,
                AutomationResultStatus.Warning,
                "Could not launch: Slack.",
                ExitCode: 1,
                StandardOutput: null,
                StandardError: "Slack: not found",
                now,
                now));

        ContextSwitchResult result = await service.SwitchAsync(
            new ContextSwitchRequest("work", ContextSwitchSource.Test),
            CancellationToken.None);

        List<LogEntry> stepEntries = logger.Entries.Where(entry => entry.EventId == "StepCompleted").ToList();
        Assert.Equal(result.StepResults.Count, stepEntries.Count);

        LogEntry launch = Assert.Single(stepEntries, entry => entry.Data?["stepId"] == "LaunchApplications.work");
        Assert.Equal(LogLevel.Warning, launch.Level);
        Assert.Equal("Warning", launch.Data!["status"]);
        Assert.Equal("1", launch.Data["exitCode"]);
        Assert.Equal("Slack: not found", launch.Data["stderr"]);

        // Every step entry shares the switch's correlation id, so one switch can be reassembled.
        Assert.All(stepEntries, entry => Assert.Equal(result.CorrelationId, entry.CorrelationId));
    }

    [Fact]
    public async Task SwitchAsyncTrimsLongProcessOutputBeforeLogging()
    {
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor executor, _, TestLogger logger) = CreateService();
        store.Seed(SettingsPath, TwoContextConfiguration());
        store.Seed(StatePath, new CurrentContextState { CurrentContextId = "personal" });

        DateTimeOffset now = DateTimeOffset.UtcNow;
        executor.SetResult(
            "LaunchApplications.work",
            new AutomationResult(
                "LaunchApplications.work",
                AutomationStepType.LaunchApplications,
                AutomationResultStatus.Warning,
                "noisy",
                ExitCode: 1,
                StandardOutput: null,
                StandardError: new string('x', 5000),
                now,
                now));

        await service.SwitchAsync(new ContextSwitchRequest("work", ContextSwitchSource.Test), CancellationToken.None);

        LogEntry launch = Assert.Single(
            logger.Entries,
            entry => entry.Data?.GetValueOrDefault("stepId") == "LaunchApplications.work");

        Assert.True(launch.Data!["stderr"].Length < 1000);
    }


    /// <summary>
    /// Clicking the active profile turns it off: the half of a switch that leaves it runs, and then
    /// no profile is current and no time is counted.
    /// </summary>
    [Fact]
    public async Task TurningOffLeavesTheActiveProfileAndEntersNone()
    {
        InMemoryAnalyticsSessionStore sessions = new();
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor executor) = CreateService(sessions);
        store.Seed(SettingsPath, TwoContextConfiguration());
        store.Seed(StatePath, new CurrentContextState { CurrentContextId = "personal" });

        ContextSwitchResult result = await service.SwitchAsync(ContextSwitchRequest.TurnOff(ContextSwitchSource.Test), CancellationToken.None);

        Assert.Equal(ContextSwitchStatus.Succeeded, result.Status);
        Assert.Equal((string.Empty, "personal"), (result.TargetContextId, result.PreviousContextId));
        Assert.Equal([AutomationStepType.CloseApplications], executor.ExecutedSteps.Select(step => step.Type));
        CurrentContextState? state = await store.ReadAsync<CurrentContextState>(StatePath);
        Assert.Equal((string.Empty, "personal"), (state?.CurrentContextId, state?.PreviousContextId));
        Assert.NotNull(state?.LastSwitchCompletedAt);
        Assert.Null(await sessions.ReadActiveMarkerAsync(CancellationToken.None));
    }

    /// <summary>From nothing, a switch only enters: there is no profile to leave.</summary>
    [Fact]
    public async Task SwitchingOnAfterTurningOffOnlyEnters()
    {
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor executor) = CreateService(new InMemoryAnalyticsSessionStore());
        store.Seed(SettingsPath, TwoContextConfiguration());
        store.Seed(StatePath, new CurrentContextState { CurrentContextId = "personal" });
        await service.SwitchAsync(ContextSwitchRequest.TurnOff(ContextSwitchSource.Test), CancellationToken.None);
        executor.ExecutedSteps.Clear();

        ContextSwitchResult result = await service.SwitchAsync(new ContextSwitchRequest("work", ContextSwitchSource.Test), CancellationToken.None);

        Assert.Null(result.PreviousContextId);
        Assert.Equal([AutomationStepType.LaunchApplications, AutomationStepType.ManageBrowserContext], executor.ExecutedSteps.Select(step => step.Type));
    }

    [Fact]
    public async Task TurningOffWithNothingActiveChangesNothing()
    {
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor executor) = CreateService(new InMemoryAnalyticsSessionStore());
        store.Seed(SettingsPath, TwoContextConfiguration());

        ContextSwitchResult result = await service.SwitchAsync(ContextSwitchRequest.TurnOff(ContextSwitchSource.Test), CancellationToken.None);

        Assert.Equal(ContextSwitchStatus.NoOp, result.Status);
        Assert.Empty(executor.ExecutedSteps);
        Assert.Null(await store.ReadAsync<CurrentContextState>(StatePath));
    }

    /// <summary>The profile that is on is the switch that turns it off; any other is switched to.</summary>
    [Fact]
    public void ClickingTheActiveProfileTurnsItOff()
    {
        Assert.True(ContextSwitchRequest.ForClickOn("work", "work", ContextSwitchSource.MenuBar).IsTurnOff);
        Assert.Equal("personal", ContextSwitchRequest.ForClickOn("personal", "work", ContextSwitchSource.MenuBar).TargetContextId);
        Assert.Equal("work", ContextSwitchRequest.ForClickOn("work", string.Empty, ContextSwitchSource.Dashboard).TargetContextId);
    }

    private static (ContextSwitchService Service, InMemoryJsonStore Store, FakeAutomationStepExecutor Executor) CreateService(InMemoryAnalyticsSessionStore sessions)
    {
        InMemoryJsonStore store = new();
        FakeAutomationStepExecutor executor = new();
        FakeClock clock = new();
        ContextSwitchService service = new(
            store, executor, new AutomationPlanBuilder(), new AnalyticsService(clock, sessions), clock, new TestLogger(), SettingsPath, StatePath);
        return (service, store, executor);
    }
    private static (ContextSwitchService Service, InMemoryJsonStore Store, FakeAutomationStepExecutor Executor, FakeClock Clock, TestLogger Logger) CreateService()
    {
        InMemoryJsonStore store = new();
        FakeAutomationStepExecutor executor = new();
        FakeClock clock = new();
        TestLogger logger = new();
        AnalyticsService analytics = new(clock, new InMemoryAnalyticsSessionStore());
        AutomationPlanBuilder planBuilder = new();

        ContextSwitchService service = new(
            store,
            executor,
            planBuilder,
            analytics,
            clock,
            logger,
            SettingsPath,
            StatePath);

        return (service, store, executor, clock, logger);
    }

    private static AppConfiguration TwoContextConfiguration(IReadOnlyList<string>? criticalSteps = null)
    {
        return new AppConfiguration
        {
            ActiveContextId = "personal",
            Contexts =
            [
                new ContextDefinition
                {
                    Id = "personal",
                    DisplayName = "Personal",
                    CloseApps = ["Spotify"]
                },
                new ContextDefinition
                {
                    Id = "work",
                    DisplayName = "Work",
                    LaunchApps = ["Slack"],
                    BrowserManagement = new BrowserManagementConfig
                    {
                        Mode = BrowserManagementMode.Urls,
                        Urls = ["https://example.com/"]
                    },
                    SwitchPolicy = new SwitchPolicyConfig
                    {
                        ContinueOnNonCriticalFailure = true,
                        CriticalSteps = criticalSteps ?? []
                    }
                }
            ]
        };
    }
}
