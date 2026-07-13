using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.Browser;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Browser;

public sealed class BrowserLauncherTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeUsesPlainOpenForDefaultBrowser()
    {
        FakeProcessRunner processRunner = new();
        BrowserLauncher launcher = new(processRunner, new FakeScriptRunner());

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Default, ["https://example.com/"], [], false, [], Timeout),
            CancellationToken.None);

        Assert.Empty(outcome.Warnings);
        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal("open", call.FileName);
        Assert.Equal(["https://example.com/"], call.Arguments);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeTargetsNamedBrowser()
    {
        FakeProcessRunner processRunner = new();
        BrowserLauncher launcher = new(processRunner, new FakeScriptRunner());

        await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://example.com/"], [], false, [], Timeout),
            CancellationToken.None);

        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal(["-a", "Google Chrome", "https://example.com/"], call.Arguments);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeSkipsOpenWhenExistingTabFocused()
    {
        FakeProcessRunner processRunner = new();
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, "true", string.Empty, false));

        BrowserLauncher launcher = new(processRunner, scriptRunner);

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Safari, ["https://example.com/"], [], true, [], Timeout),
            CancellationToken.None);

        Assert.Empty(outcome.Warnings);
        Assert.Empty(processRunner.Calls);
        Assert.Single(scriptRunner.Scripts);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeOpensWhenNoExistingTabFound()
    {
        FakeProcessRunner processRunner = new();
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, "false", string.Empty, false));

        BrowserLauncher launcher = new(processRunner, scriptRunner);

        await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Safari, ["https://example.com/"], [], true, [], Timeout),
            CancellationToken.None);

        Assert.Single(processRunner.Calls);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeReportsWarningOnNonZeroExit()
    {
        FakeProcessRunner processRunner = new();
        processRunner.Enqueue(new ProcessResult(1, string.Empty, "no such app", false));
        BrowserLauncher launcher = new(processRunner, new FakeScriptRunner());

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Default, ["https://example.com/"], [], false, [], Timeout),
            CancellationToken.None);

        Assert.Single(outcome.Warnings);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncGroupsModeSucceedsWithoutFallbackWhenGroupActivated()
    {
        FakeProcessRunner processRunner = new();
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, "true", string.Empty, false));

        BrowserLauncher launcher = new(processRunner, scriptRunner);

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Groups, BrowserKind.Safari, ["https://fallback.example/"], ["Work-Core"], false, [], Timeout),
            CancellationToken.None);

        Assert.Empty(outcome.Warnings);
        Assert.Empty(processRunner.Calls);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncGroupsModeFallsBackToUrlsWhenGroupActivationFails()
    {
        FakeProcessRunner processRunner = new();
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, "false", string.Empty, false));

        BrowserLauncher launcher = new(processRunner, scriptRunner);

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Groups, BrowserKind.Chrome, ["https://fallback.example/"], ["Work-Core"], false, [], Timeout),
            CancellationToken.None);

        Assert.Single(outcome.Warnings);
        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal(["-a", "Google Chrome", "https://fallback.example/"], call.Arguments);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncGroupsModeUsesChromiumScriptForBrave()
    {
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, "true", string.Empty, false));
        BrowserLauncher launcher = new(new FakeProcessRunner(), scriptRunner);

        await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Groups, BrowserKind.Brave, [], ["Personal"], false, [], Timeout),
            CancellationToken.None);

        Assert.Contains("Brave Browser", Assert.Single(scriptRunner.Scripts));
    }

    [Fact]
    public async Task ManageBrowserContextAsyncProfilesModeLaunchesEachProfileWithProfileDirectoryArgument()
    {
        FakeProcessRunner processRunner = new();
        BrowserLauncher launcher = new(processRunner, new FakeScriptRunner());

        BrowserProfileConfig[] profiles =
        [
            new() { Browser = BrowserKind.Chrome, ProfileDirectory = "Profile 1", Urls = ["https://mail.example/"] },
            new() { Browser = BrowserKind.Brave, ProfileDirectory = "Default", Urls = [] }
        ];

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Profiles, BrowserKind.Default, [], [], false, profiles, Timeout),
            CancellationToken.None);

        Assert.Empty(outcome.Warnings);
        Assert.Equal(2, processRunner.Calls.Count);

        ProcessStartOptions chromeCall = processRunner.Calls[0];
        Assert.Equal("open", chromeCall.FileName);
        Assert.Equal(["-na", "Google Chrome", "--args", "--profile-directory=Profile 1", "https://mail.example/"], chromeCall.Arguments);

        ProcessStartOptions braveCall = processRunner.Calls[1];
        Assert.Equal(["-na", "Brave Browser", "--args", "--profile-directory=Default"], braveCall.Arguments);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncProfilesModeReportsWarningOnFailure()
    {
        FakeProcessRunner processRunner = new();
        processRunner.Enqueue(new ProcessResult(1, string.Empty, "profile locked", false));
        BrowserLauncher launcher = new(processRunner, new FakeScriptRunner());

        BrowserProfileConfig[] profiles = [new() { Browser = BrowserKind.Chrome, ProfileDirectory = "Profile 1" }];

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Profiles, BrowserKind.Default, [], [], false, profiles, Timeout),
            CancellationToken.None);

        Assert.Single(outcome.Warnings);
    }
}
