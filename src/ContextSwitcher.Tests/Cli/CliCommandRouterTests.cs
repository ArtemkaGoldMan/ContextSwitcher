using System.Text.Json;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Infrastructure.Cli;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Cli;

public sealed class CliCommandRouterTests
{
    [Fact]
    public async Task RunAsyncWithNoArgsPrintsUsageAndReturnsInvalidArguments()
    {
        (CliCommandRouter router, StubContextSwitchService _, StringWriter output) = Create();

        int exitCode = await router.RunAsync([], TwoContextConfiguration(), Valid, new CurrentContextState(), output, CancellationToken.None);

        Assert.Equal((int)CliExitCode.InvalidArguments, exitCode);
        Assert.Contains("Usage:", output.ToString());
    }

    [Fact]
    public async Task RunAsyncSwitchWithoutContextReturnsInvalidArguments()
    {
        (CliCommandRouter router, _, StringWriter output) = Create();

        int exitCode = await router.RunAsync(["switch"], TwoContextConfiguration(), Valid, new CurrentContextState(), output, CancellationToken.None);

        Assert.Equal((int)CliExitCode.InvalidArguments, exitCode);
    }

    [Fact]
    public async Task RunAsyncSwitchWithInvalidConfigurationReturnsConfigurationInvalid()
    {
        (CliCommandRouter router, _, StringWriter output) = Create();
        ConfigurationValidationResult invalid = new([new ConfigurationValidationError("contexts", "broken")]);

        int exitCode = await router.RunAsync(
            ["switch", "--context", "work"], TwoContextConfiguration(), invalid, new CurrentContextState(), output, CancellationToken.None);

        Assert.Equal((int)CliExitCode.ConfigurationInvalid, exitCode);
    }

    [Fact]
    public async Task RunAsyncSwitchWithUnknownContextReturnsUnknownContext()
    {
        (CliCommandRouter router, _, StringWriter output) = Create();

        int exitCode = await router.RunAsync(
            ["switch", "--context", "bogus"], TwoContextConfiguration(), Valid, new CurrentContextState(), output, CancellationToken.None);

        Assert.Equal((int)CliExitCode.UnknownContext, exitCode);
    }

    [Theory]
    [InlineData(ContextSwitchStatus.Succeeded, CliExitCode.Success)]
    [InlineData(ContextSwitchStatus.NoOp, CliExitCode.Success)]
    [InlineData(ContextSwitchStatus.SucceededWithWarnings, CliExitCode.SucceededWithWarnings)]
    [InlineData(ContextSwitchStatus.Cancelled, CliExitCode.AnotherSwitchRunning)]
    [InlineData(ContextSwitchStatus.Failed, CliExitCode.GeneralFailure)]
    public async Task RunAsyncSwitchMapsResultStatusToExitCode(ContextSwitchStatus status, CliExitCode expected)
    {
        (CliCommandRouter router, StubContextSwitchService switchService, StringWriter output) = Create();
        switchService.Result = switchService.Result with { Status = status };

        int exitCode = await router.RunAsync(
            ["switch", "--context", "work"], TwoContextConfiguration(), Valid, new CurrentContextState(), output, CancellationToken.None);

        Assert.Equal((int)expected, exitCode);
        Assert.Equal("work", switchService.LastRequest?.TargetContextId);
        Assert.Equal(ContextSwitchSource.Cli, switchService.LastRequest?.Source);
    }

    [Fact]
    public async Task RunAsyncSwitchJsonOutputIsValidJsonWithExpectedShape()
    {
        (CliCommandRouter router, StubContextSwitchService switchService, StringWriter output) = Create();
        switchService.Result = switchService.Result with
        {
            Status = ContextSwitchStatus.SucceededWithWarnings,
            StepResults =
            [
                new ContextSwitcher.Core.Automation.AutomationResult(
                    "SetTheme.work", ContextSwitcher.Core.Automation.AutomationStepType.SetTheme,
                    ContextSwitcher.Core.Automation.AutomationResultStatus.Warning, "Could not change theme.",
                    null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
            ]
        };

        await router.RunAsync(
            ["switch", "--context", "work", "--json"], TwoContextConfiguration(), Valid, new CurrentContextState(), output, CancellationToken.None);

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        Assert.Equal("SucceededWithWarnings", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("work", document.RootElement.GetProperty("contextId").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("warnings").GetArrayLength());
    }

    [Fact]
    public async Task RunAsyncListContextsListsAllConfiguredContexts()
    {
        (CliCommandRouter router, _, StringWriter output) = Create();

        int exitCode = await router.RunAsync(["list-contexts"], TwoContextConfiguration(), Valid, new CurrentContextState(), output, CancellationToken.None);

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Contains("work", output.ToString());
        Assert.Contains("personal", output.ToString());
    }

    [Fact]
    public async Task RunAsyncValidateConfigReturnsSuccessWhenValid()
    {
        (CliCommandRouter router, _, StringWriter output) = Create();

        int exitCode = await router.RunAsync(["validate-config"], TwoContextConfiguration(), Valid, new CurrentContextState(), output, CancellationToken.None);

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Contains("valid", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsyncValidateConfigReturnsConfigurationInvalidWithErrorsListed()
    {
        (CliCommandRouter router, _, StringWriter output) = Create();
        ConfigurationValidationResult invalid = new([new ConfigurationValidationError("contexts[0].id", "must be unique")]);

        int exitCode = await router.RunAsync(["validate-config"], TwoContextConfiguration(), invalid, new CurrentContextState(), output, CancellationToken.None);

        Assert.Equal((int)CliExitCode.ConfigurationInvalid, exitCode);
        Assert.Contains("must be unique", output.ToString());
    }

    [Fact]
    public async Task RunAsyncUnknownCommandReturnsInvalidArguments()
    {
        (CliCommandRouter router, _, StringWriter output) = Create();

        int exitCode = await router.RunAsync(["frobnicate"], TwoContextConfiguration(), Valid, new CurrentContextState(), output, CancellationToken.None);

        Assert.Equal((int)CliExitCode.InvalidArguments, exitCode);
        Assert.Contains("Unknown command", output.ToString());
    }

    [Fact]
    public void IsHeadlessCommandIsFalseOnlyForOpenDashboard()
    {
        Assert.False(CliCommandRouter.IsHeadlessCommand("open-dashboard"));
        Assert.True(CliCommandRouter.IsHeadlessCommand("switch"));
        Assert.True(CliCommandRouter.IsHeadlessCommand("anything-else"));
    }

    private static ConfigurationValidationResult Valid { get; } = new([]);

    private static (CliCommandRouter Router, StubContextSwitchService SwitchService, StringWriter Output) Create()
    {
        StubContextSwitchService switchService = new();
        return (new CliCommandRouter(switchService), switchService, new StringWriter());
    }

    private static AppConfiguration TwoContextConfiguration()
    {
        return new AppConfiguration
        {
            Contexts =
            [
                new ContextDefinition { Id = "work", DisplayName = "Work" },
                new ContextDefinition { Id = "personal", DisplayName = "Personal" }
            ]
        };
    }
}
