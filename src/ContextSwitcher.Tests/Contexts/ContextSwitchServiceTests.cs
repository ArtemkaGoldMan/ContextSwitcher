using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Contexts;

public sealed class ContextSwitchServiceTests
{
    private const string SettingsPath = "settings.json";
    private const string StatePath = "state.json";

    [Fact]
    public async Task SwitchAsyncReturnsNoOpWhenTargetIsAlreadyActive()
    {
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor executor, _) = CreateService();
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
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor executor, _) = CreateService();
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
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor executor, _) = CreateService();
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
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor executor, _) = CreateService();
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

    [Fact]
    public async Task SwitchAsyncDryRunSkipsExecutionAndDoesNotPersistState()
    {
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor executor, _) = CreateService();
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
        (ContextSwitchService service, InMemoryJsonStore store, FakeAutomationStepExecutor _, _) = CreateService();
        store.Seed(SettingsPath, TwoContextConfiguration());
        store.Seed(StatePath, new CurrentContextState { CurrentContextId = "personal" });
        store.WriteFailurePaths.Add(StatePath);

        ContextSwitchResult result = await service.SwitchAsync(
            new ContextSwitchRequest("work", ContextSwitchSource.Test),
            CancellationToken.None);

        Assert.Equal(ContextSwitchStatus.Failed, result.Status);
    }

    private static (ContextSwitchService Service, InMemoryJsonStore Store, FakeAutomationStepExecutor Executor, FakeClock Clock) CreateService()
    {
        InMemoryJsonStore store = new();
        FakeAutomationStepExecutor executor = new();
        FakeClock clock = new();
        TestLogger logger = new();
        AnalyticsService analytics = new(clock);
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

        return (service, store, executor, clock);
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
