using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.Automation;
using ContextSwitcher.Infrastructure.Browser;
using ContextSwitcher.Infrastructure.MacOS;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Automation;

public sealed class AutomationStepExecutorTests
{
    [Fact]
    public async Task ExecuteAsyncCloseApplicationsSucceedsWhenAppReportsNotRunning()
    {
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, string.Empty, string.Empty, false)); // quit
        scriptRunner.Enqueue(new ProcessResult(0, "false", string.Empty, false)); // is running?

        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock());
        AutomationStep step = CloseApplicationsStep(["Slack"], isCritical: false);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task ExecuteAsyncCloseApplicationsWarnsWhenNonCriticalAndAppStillRunning()
    {
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, string.Empty, string.Empty, false)); // quit
        scriptRunner.Enqueue(new ProcessResult(0, "true", string.Empty, false)); // still running

        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock());
        AutomationStep step = CloseApplicationsStep(["Slack"], isCritical: false);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Warning, result.Status);
    }

    [Fact]
    public async Task ExecuteAsyncCloseApplicationsFailsWhenCriticalAndAppStillRunning()
    {
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, string.Empty, string.Empty, false)); // quit
        scriptRunner.Enqueue(new ProcessResult(0, "true", string.Empty, false)); // still running

        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock());
        AutomationStep step = CloseApplicationsStep(["Slack"], isCritical: true);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Failed, result.Status);
    }

    /// <summary>
    /// VS Code, Cursor and other Electron apps answer "quit" at once and are gone a second or two
    /// later. Checking only once, straight after the quit, reported them as "Could not close" on
    /// switch after switch while they were closing fine; the check now waits for them.
    /// </summary>
    [Fact]
    public async Task AnAppThatTakesAMomentToQuitIsNotReportedAsStillRunning()
    {
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, string.Empty, string.Empty, false)); // quit
        scriptRunner.Enqueue(new ProcessResult(0, "true", string.Empty, false));     // still closing
        scriptRunner.Enqueue(new ProcessResult(0, "true", string.Empty, false));     // still closing
        scriptRunner.Enqueue(new ProcessResult(0, "false", string.Empty, false));    // gone

        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock());

        AutomationResult result = await executor.ExecuteAsync(CloseApplicationsStep(["Visual Studio Code"], isCritical: false), CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Succeeded, result.Status);
        Assert.Equal(4, scriptRunner.Scripts.Count);
    }

    /// <summary>
    /// An app that really does not quit - a "save changes?" sheet waiting on the user - is still
    /// reported, after a bounded number of checks that all fit inside the app's share of the step.
    /// A check that fails says nothing either way, so it does not count as "gone".
    /// </summary>
    [Fact]
    public async Task AnAppThatNeverQuitsIsStillReportedWithinItsBudget()
    {
        FakeScriptRunner scriptRunner = new() { DefaultResult = new ProcessResult(0, "true", string.Empty, false) };
        scriptRunner.Enqueue(new ProcessResult(0, string.Empty, string.Empty, false)); // quit
        scriptRunner.Enqueue(new ProcessResult(1, string.Empty, "AppleEvent timed out", false)); // unknown

        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock());
        AutomationStep step = CloseApplicationsStep(["TextEdit"], isCritical: false);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Warning, result.Status);
        Assert.Equal("Didn't quit: TextEdit. It may be asking whether to save changes.", result.Message);
        Assert.InRange(scriptRunner.Scripts.Count, 3, 16);
        Assert.All(scriptRunner.Timeouts, timeout => Assert.True(timeout <= step.Timeout * 0.5));
    }

    /// <summary>
    /// A graceful quit can block indefinitely on a modal "save this document?" sheet. Every script
    /// used to get the whole step budget, so the first such app consumed all of it: the step-level
    /// timeout fired mid-quit, the "Could not close" warning never ran, and later apps in the list
    /// were never asked to quit at all.
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncCloseApplicationsSplitsTheBudgetAcrossAppsAndTheirChecks()
    {
        FakeScriptRunner scriptRunner = new();
        scriptRunner.DefaultResult = new ProcessResult(0, "false", string.Empty, false);

        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock());

        // Mirrors the plan builder, which sizes the step at ten seconds per app.
        TimeSpan stepTimeout = TimeSpan.FromSeconds(20);
        AutomationStep step = new(
            "CloseApplications.personal", AutomationStepType.CloseApplications, "Close applications", false,
            stepTimeout, new AppsArguments(["Chess", "Stickies"]).ToDictionary());

        await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(4, scriptRunner.Timeouts.Count);
        Assert.All(scriptRunner.Timeouts, timeout => Assert.True(timeout < stepTimeout));

        // Everything the step can spend must still fit inside the step's own budget, or the
        // step-level timeout wins the race and the per-app reporting is lost again.
        TimeSpan worstCase = scriptRunner.Timeouts.Aggregate(TimeSpan.Zero, (total, next) => total + next);
        Assert.True(worstCase <= stepTimeout, $"worst case {worstCase} exceeds the step's {stepTimeout}");
    }

    [Fact]
    public async Task ExecuteAsyncLaunchApplicationsSplitsTheBudgetAcrossApps()
    {
        FakeProcessRunner processRunner = new();
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());

        TimeSpan stepTimeout = TimeSpan.FromSeconds(30);
        AutomationStep step = new(
            "LaunchApplications.work", AutomationStepType.LaunchApplications, "Launch applications", false,
            stepTimeout, new AppsArguments(["Calculator", "Stickies"]).ToDictionary());

        await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(2, processRunner.Calls.Count);
        TimeSpan worstCase = processRunner.Calls.Aggregate(TimeSpan.Zero, (total, call) => total + call.Timeout);
        Assert.True(worstCase < stepTimeout, $"worst case {worstCase} exceeds the step's {stepTimeout}");
    }

    /// <summary>
    /// Single-call steps have the same failure shape: handing the command the whole step budget
    /// meant a hung Shortcuts invocation surfaced as a bare "step timed out" instead of the
    /// actionable "create this Shortcut" warning below it.
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncSetFocusModeLeavesHeadroomToReportItsOwnFailure()
    {
        FakeProcessRunner processRunner = new();
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());

        AutomationStep step = FocusStep(enabled: true, modeName: "Work", isCritical: false);

        await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.True(Assert.Single(processRunner.Calls).Timeout < step.Timeout);
    }

    /// <summary>
    /// The same headroom on the script-runner path. This was covered by the theme step until theme
    /// support was removed; media playback is the remaining single-call step that goes through
    /// AppleScript rather than a plain process.
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncControlMediaLeavesHeadroomToReportItsOwnFailure()
    {
        FakeScriptRunner scriptRunner = new();
        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock());

        AutomationStep step = MediaStep(player: MediaPlayerKind.AppleMusic, playlist: "Deep Focus", autoPlay: true, isCritical: false);

        await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.True(Assert.Single(scriptRunner.Timeouts) < step.Timeout);
    }

    [Fact]
    public async Task ExecuteAsyncLaunchApplicationsCallsOpenWithAppNameArgument()
    {
        FakeProcessRunner processRunner = new();
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());
        AutomationStep step = LaunchApplicationsStep(["Visual Studio Code"], isCritical: false);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Succeeded, result.Status);
        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal("open", call.FileName);
        Assert.Equal(["-a", "Visual Studio Code"], call.Arguments);
    }

    /// <summary>"Could not launch" for an app that was never installed sent people looking for what went wrong with it.</summary>
    [Fact]
    public async Task AnAppThatIsNotInstalledIsSaidApartFromOneThatFailedToOpen()
    {
        FakeProcessRunner processRunner = new();
        processRunner.Enqueue(new ProcessResult(1, string.Empty, "Unable to find application named 'Nonexistent App'", false));
        processRunner.Enqueue(new ProcessResult(1, string.Empty, "The application cannot be opened for an unexpected reason", false));

        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());
        AutomationStep step = LaunchApplicationsStep(["Nonexistent App", "Broken App"], isCritical: false);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Warning, result.Status);
        Assert.Equal(
            ["Not installed, so not opened: Nonexistent App.", "Could not launch: Broken App."],
            result.Problems.Select(problem => problem.English));
    }

    [Fact]
    public async Task ExecuteAsyncLaunchApplicationsWarnsOnNonZeroExitWhenNonCritical()
    {
        FakeProcessRunner processRunner = new();
        processRunner.Enqueue(new ProcessResult(1, string.Empty, "app not found", false));

        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());
        AutomationStep step = LaunchApplicationsStep(["Nonexistent App"], isCritical: false);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Warning, result.Status);
    }

    [Fact]
    public async Task ExecuteAsyncManageBrowserContextSucceedsWhenUrlOpensCleanly()
    {
        FakeProcessRunner processRunner = new();
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());
        AutomationStep step = ManageBrowserContextStep(
            mode: BrowserManagementMode.Urls, browser: BrowserKind.Default, url: "https://example.com/", isCritical: false);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Succeeded, result.Status);
        Assert.Single(processRunner.Calls);
    }

    [Fact]
    public async Task ExecuteAsyncManageBrowserContextFailsWhenCriticalAndUrlOpenFails()
    {
        FakeProcessRunner processRunner = new();
        processRunner.Enqueue(new ProcessResult(1, string.Empty, "no browser", false));
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());
        AutomationStep step = ManageBrowserContextStep(
            mode: BrowserManagementMode.Urls, browser: BrowserKind.Default, url: "https://example.com/", isCritical: true);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Failed, result.Status);
    }

    [Fact]
    public async Task ExecuteAsyncManageBrowserContextDeserializesProfilesJsonAndLaunchesThem()
    {
        FakeProcessRunner processRunner = new();
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());

        BrowserContextArguments arguments = new(
            "work", BrowserManagementMode.Profiles, BrowserKind.Default, [], [], AvoidDuplicateTabs: true,
            [new BrowserProfileConfig { Browser = BrowserKind.Chrome, ProfileDirectory = "Profile 1", Urls = ["https://mail.example/"] }]);
        AutomationStep step = new("ManageBrowserContext.work", AutomationStepType.ManageBrowserContext, "Manage browser context", false, TimeSpan.FromSeconds(20), arguments.ToDictionary());

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Succeeded, result.Status);
        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal(["-na", "Google Chrome", "--args", "--profile-directory=Profile 1", "https://mail.example/"], call.Arguments);
    }


    [Fact]
    public async Task ExecuteAsyncCloseBrowserTabsReportsWhatItClosed()
    {
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, "4\u001fhttps://linear.app/a\n5\u001fhttps://linear.app/b\n", string.Empty, false));
        scriptRunner.Enqueue(new ProcessResult(0, "2", string.Empty, false));
        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock());
        CloseTabsArguments arguments = new(BrowserKind.Chrome, ["https://linear.app/"], [], "work");
        AutomationStep step = new("CloseBrowserTabs.work", AutomationStepType.CloseBrowserTabs, "Close browser tabs", false, TimeSpan.FromSeconds(10), arguments.ToDictionary());

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Succeeded, result.Status);
        Assert.Equal("Closed 2 tab(s).", result.Message);
        Assert.Contains("set doomed to {\"4\", \"5\"}", scriptRunner.Scripts[1], StringComparison.Ordinal);
    }
    /// <summary>
    /// A comma belongs to the address: a map's coordinates used to split it into pieces that
    /// opened nothing.
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncManageBrowserContextOpensAnAddressWithCommasWhole()
    {
        FakeProcessRunner processRunner = new();
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());
        AutomationStep step = ManageBrowserContextStep(
            mode: BrowserManagementMode.Urls, browser: BrowserKind.Default, url: "https://www.google.com/maps/@50.45,30.52,12z", isCritical: false);

        await executor.ExecuteAsync(step, CancellationToken.None);

        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal(["https://www.google.com/maps/@50.45,30.52,12z"], call.Arguments);
    }

    private static AutomationStep ManageBrowserContextStep(BrowserManagementMode mode, BrowserKind browser, string url, bool isCritical)
    {
        BrowserContextArguments arguments = new(null, mode, browser, [url], [], AvoidDuplicateTabs: false, []);
        return new AutomationStep("ManageBrowserContext.work", AutomationStepType.ManageBrowserContext, "Manage browser context", isCritical, TimeSpan.FromSeconds(20), arguments.ToDictionary());
    }

    [Fact]
    public async Task ExecuteAsyncReturnsSkippedForNotYetImplementedStepTypes()
    {
        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), new FakeScriptRunner(), new FakeClock());
        AutomationStep step = new(
            "OpenUrls.work", AutomationStepType.OpenUrls, "Open urls", false,
            TimeSpan.FromSeconds(10), new Dictionary<string, string>());

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Skipped, result.Status);
    }

    [Theory]
    [InlineData(AutomationStepType.StartDockerResources, "start")]
    [InlineData(AutomationStepType.StopDockerResources, "stop")]
    public async Task ExecuteAsyncDockerCommandsCallDockerWithCommandAndContainers(AutomationStepType type, string expectedCommand)
    {
        FakeProcessRunner processRunner = new();
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());
        AutomationStep step = new(
            $"{type}.work", type, "Docker", false, TimeSpan.FromSeconds(45),
            new ContainersArguments(["postgres-work", "redis-work"]).ToDictionary());

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Succeeded, result.Status);
        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal("docker", call.FileName);
        Assert.Equal([expectedCommand, "postgres-work", "redis-work"], call.Arguments);
    }

    [Fact]
    public async Task ExecuteAsyncDockerCommandFailsWhenCriticalAndDockerUnavailable()
    {
        FakeProcessRunner processRunner = new();
        processRunner.Enqueue(new ProcessResult(1, string.Empty, "docker: command not found", false));
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());
        AutomationStep step = new(
            "StopDockerResources.work", AutomationStepType.StopDockerResources, "Docker", true, TimeSpan.FromSeconds(45),
            new ContainersArguments(["redis-work"]).ToDictionary());

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Failed, result.Status);
    }

    [Fact]
    public async Task ExecuteAsyncControlMediaSkipsWhenAutoPlayDisabled()
    {
        FakeScriptRunner scriptRunner = new();
        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock());
        AutomationStep step = MediaStep(player: MediaPlayerKind.AppleMusic, playlist: "Deep Focus", autoPlay: false, isCritical: false);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Succeeded, result.Status);
        Assert.Empty(scriptRunner.Scripts);
    }

    [Fact]
    public async Task ExecuteAsyncControlMediaWarnsWhenPlaylistMissing()
    {
        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), new FakeScriptRunner(), new FakeClock());
        AutomationStep step = MediaStep(player: MediaPlayerKind.AppleMusic, playlist: "", autoPlay: true, isCritical: false);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Warning, result.Status);
    }

    [Fact]
    public async Task ExecuteAsyncControlMediaPlaysAppleMusicPlaylist()
    {
        FakeScriptRunner scriptRunner = new();
        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock());
        AutomationStep step = MediaStep(player: MediaPlayerKind.AppleMusic, playlist: "Deep Focus", autoPlay: true, isCritical: false);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Succeeded, result.Status);
        Assert.Contains("Music", Assert.Single(scriptRunner.Scripts));
        Assert.Contains("Deep Focus", Assert.Single(scriptRunner.Scripts));
    }

    [Fact]
    public async Task ExecuteAsyncControlMediaSpotifyFailureIsAlwaysWarningEvenWhenCritical()
    {
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(1, string.Empty, "spotify error", false));
        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock());
        AutomationStep step = MediaStep(player: MediaPlayerKind.Spotify, playlist: "spotify:playlist:abc", autoPlay: true, isCritical: true);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Warning, result.Status);
    }

    [Fact]
    public async Task ExecuteAsyncSetFocusModeRunsFocusShortcutWhenEnabled()
    {
        FakeProcessRunner processRunner = new();
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());
        AutomationStep step = FocusStep(enabled: true, modeName: "Work", isCritical: false);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Succeeded, result.Status);
        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal("shortcuts", call.FileName);
        Assert.Equal(["run", "ContextSwitcher - Focus Work"], call.Arguments);
    }

    [Fact]
    public async Task ExecuteAsyncSetFocusModeRunsFocusOffShortcutWhenDisabled()
    {
        FakeProcessRunner processRunner = new();
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());
        AutomationStep step = FocusStep(enabled: false, modeName: string.Empty, isCritical: false);

        await executor.ExecuteAsync(step, CancellationToken.None);

        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal(["run", "ContextSwitcher - Focus Off"], call.Arguments);
    }

    /// <summary>Leaving a profile's Focus runs that mode's own Off Shortcut.</summary>
    [Fact]
    public async Task TurningFocusOffRunsTheLeftModesOwnOffShortcut()
    {
        FakeProcessRunner processRunner = new();
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());

        AutomationResult result = await executor.ExecuteAsync(FocusStep(enabled: false, modeName: string.Empty, isCritical: false, previousModeName: "Do Not Disturb"), CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Succeeded, result.Status);
        Assert.Equal(["run", "ContextSwitcher - Focus Off - Do Not Disturb"], Assert.Single(processRunner.Calls).Arguments);
    }

    /// <summary>Without it, the original single "Focus Off" someone built by hand still works.</summary>
    [Fact]
    public async Task TurningFocusOffFallsBackToTheOriginalOffShortcut()
    {
        FakeProcessRunner processRunner = new();
        processRunner.Enqueue(new ProcessResult(1, string.Empty, "Error: The operation couldn’t be completed. Couldn’t find shortcut", false));
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());

        AutomationResult result = await executor.ExecuteAsync(FocusStep(enabled: false, modeName: string.Empty, isCritical: false, previousModeName: "Work"), CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Succeeded, result.Status);
        Assert.Equal(
            [["run", "ContextSwitcher - Focus Off - Work"], ["run", "ContextSwitcher - Focus Off"]],
            processRunner.Calls.Select(call => call.Arguments.ToArray()));
    }

    /// <summary>
    /// A Shortcut that exists but fails says why - not "create it", which sent the user looking for
    /// a Shortcut that was right there in their list.
    /// </summary>
    [Fact]
    public async Task AShortcutThatRunsButFailsReportsMacOSsReason()
    {
        FakeProcessRunner processRunner = new();
        processRunner.Enqueue(new ProcessResult(1, string.Empty, "Error: The action could not run because a Focus named “Reading” does not exist on this device.", false));
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());

        AutomationResult result = await executor.ExecuteAsync(FocusStep(enabled: false, modeName: string.Empty, isCritical: false, previousModeName: "Reading"), CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Warning, result.Status);
        Assert.Single(processRunner.Calls);
        Assert.Equal(
            "Shortcut 'ContextSwitcher - Focus Off - Reading' ran but failed: The action could not run because a Focus named “Reading” does not exist on this device.",
            result.Message);
    }

    [Fact]
    public async Task AMissingShortcutSaysHowToCreateIt()
    {
        FakeProcessRunner processRunner = new() { DefaultResult = new ProcessResult(1, string.Empty, "Error: Couldn’t find shortcut", false) };
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());

        AutomationResult result = await executor.ExecuteAsync(FocusStep(enabled: true, modeName: "Work", isCritical: false), CancellationToken.None);

        Assert.StartsWith("Shortcut 'ContextSwitcher - Focus Work' doesn't exist yet.", result.Message, StringComparison.Ordinal);
        Assert.Contains("Create it", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsyncSetFocusModeFailsWhenCriticalAndShortcutMissing()
    {
        FakeProcessRunner processRunner = new();
        processRunner.Enqueue(new ProcessResult(1, string.Empty, "No shortcut named...", false));
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());
        AutomationStep step = FocusStep(enabled: true, modeName: "Work", isCritical: true);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Failed, result.Status);
    }

    private static AutomationStep MediaStep(MediaPlayerKind player, string playlist, bool autoPlay, bool isCritical)
    {
        MediaArguments arguments = new(player, playlist, autoPlay);
        return new AutomationStep("ControlMedia.work", AutomationStepType.ControlMedia, "Control media", isCritical, TimeSpan.FromSeconds(8), arguments.ToDictionary());
    }

    private static AutomationStep FocusStep(bool enabled, string modeName, bool isCritical, string previousModeName = "")
    {
        FocusArguments arguments = new(enabled, modeName, previousModeName);
        return new AutomationStep("SetFocusMode.work", AutomationStepType.SetFocusMode, "Set Focus mode", isCritical, TimeSpan.FromSeconds(10), arguments.ToDictionary());
    }

    /// <summary>
    /// Every handler, with the Mac's ways of asking the system over fake runners, so what each step
    /// would run on a Mac is what is checked.
    /// </summary>
    private static AutomationStepExecutor CreateExecutor(FakeProcessRunner processRunner, FakeScriptRunner scriptRunner, FakeClock clock, FakeApplicationHider? hider = null, FakeAppProcesses? processes = null)
    {
        // No wait between "is it still running?" checks, so a test of an app that never quits does
        // not sit through the real interval. Never the real hider: it would hide this Mac's apps -
        // nor the real processes, whose windows are this Mac's.
        BrowserLauncher browser = new(new MacBrowserControl(processRunner, scriptRunner), new FakeOpenedTabsStore(), new FakeBrowserRuns(), new TestLogger(), TabTimings.Immediate);
        return new AutomationStepExecutor(
            [
                new CloseApplicationsHandler(new MacAppQuitter(scriptRunner), processes ?? new FakeAppProcesses(), clock, QuitTimings.Immediate),
                new LaunchApplicationsHandler(new MacAppLauncher(processRunner), clock),
                new HideApplicationsHandler(hider ?? new FakeApplicationHider(), clock),
                new ManageBrowserContextHandler(browser, clock),
                new CloseBrowserTabsHandler(browser, clock),
                new DockerContainersHandler(AutomationStepType.StartDockerResources, processRunner, clock),
                new DockerContainersHandler(AutomationStepType.StopDockerResources, processRunner, clock),
                new ControlMediaHandler(new MacMediaPlayer(scriptRunner), clock),
                new SetFocusModeHandler(new MacShortcutRunner(processRunner), clock),
            ],
            clock);
    }

    /// <summary>A step goes to the handler for its type, and to no other.</summary>
    [Fact]
    public async Task AStepGoesToTheHandlerForItsType()
    {
        FakeStepHandler launching = new(AutomationStepType.LaunchApplications);
        FakeStepHandler closing = new(AutomationStepType.CloseApplications);
        AutomationStepExecutor executor = new([launching, closing], new FakeClock());

        await executor.ExecuteAsync(LaunchApplicationsStep(["Notes"], isCritical: false), CancellationToken.None);

        Assert.Equal(["LaunchApplications.work"], launching.Ran);
        Assert.Empty(closing.Ran);
    }

    /// <summary>Two handlers for one type is a mistake in the registrations, said when the executor is made.</summary>
    [Fact]
    public void TwoHandlersForOneTypeAreRefused()
    {
        Assert.Throws<ArgumentException>(() => new AutomationStepExecutor(
            [new FakeStepHandler(AutomationStepType.SetFocusMode), new FakeStepHandler(AutomationStepType.SetFocusMode)], new FakeClock()));
    }

    private sealed class FakeStepHandler(AutomationStepType type) : IStepHandler
    {
        public List<string> Ran { get; } = [];

        public AutomationStepType Type => type;

        public Task<AutomationResult> ExecuteAsync(AutomationStep step, CancellationToken cancellationToken)
        {
            this.Ran.Add(step.Id);
            return Task.FromResult(new AutomationResult(step.Id, step.Type, AutomationResultStatus.Succeeded, "Ran.", 0, null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
        }
    }

    /// <summary>
    /// Autodesk Fusion answers being asked to quit with "user canceled" (-128), closes its windows and
    /// is gone half a minute later; it was told as "it may be asking to save", which it was not. With
    /// no window on screen it is still closing, which the follow-up watches.
    /// </summary>
    [Fact]
    public async Task AnAppWithNoWindowOnScreenIsStillClosingNotAskingToSave()
    {
        FakeScriptRunner scriptRunner = new() { DefaultResult = new ProcessResult(0, "true", string.Empty, false) };
        scriptRunner.Enqueue(new ProcessResult(1, string.Empty, "execution error: Autodesk Fusion got an error: User canceled. (-128)", false));
        FakeAppProcesses processes = new FakeAppProcesses().Running("Autodesk Fusion", hasWindow: false);

        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock(), processes: processes);
        AutomationResult result = await executor.ExecuteAsync(CloseApplicationsStep(["Autodesk Fusion"], isCritical: false), CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Warning, result.Status);
        UserMessage problem = Assert.Single(result.Problems);
        Assert.Equal(SwitchMessages.StillClosing.Key, problem.Key);
        Assert.Equal("Still closing: Autodesk Fusion. This line goes once it has quit.", problem.English);
    }

    /// <summary>Each app still running has a line of its own, so each can change on its own afterwards.</summary>
    [Fact]
    public async Task EachAppStillRunningHasALineOfItsOwn()
    {
        FakeScriptRunner scriptRunner = new() { DefaultResult = new ProcessResult(0, "true", string.Empty, false) };
        FakeAppProcesses processes = new FakeAppProcesses().Running("Autodesk Fusion", hasWindow: false).Running("TextEdit", hasWindow: true);

        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock(), processes: processes);
        AutomationResult result = await executor.ExecuteAsync(CloseApplicationsStep(["Autodesk Fusion", "TextEdit", "Notes"], isCritical: false), CancellationToken.None);

        Assert.Equal(
            [(SwitchMessages.StillClosing.Key, "Autodesk Fusion"), (SwitchMessages.CouldNotClose.Key, "TextEdit"), (SwitchMessages.CouldNotClose.Key, "Notes")],
            result.Problems.Select(problem => (problem.Key, problem.Arguments[0])));
    }

    [Fact]
    public async Task ExecuteAsyncHideApplicationsAsksEachToHide()
    {
        FakeApplicationHider hider = new();
        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), new FakeScriptRunner(), new FakeClock(), hider);
        AutomationStep step = new(
            "HideApplications.work", AutomationStepType.HideApplications, "Hide applications", false, TimeSpan.FromSeconds(5),
            new AppsArguments(["Slack", "Notion"]).ToDictionary());

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(["Slack", "Notion"], hider.Hidden);
        Assert.Equal(AutomationResultStatus.Succeeded, result.Status);
    }

    private static AutomationStep CloseApplicationsStep(IReadOnlyList<string> apps, bool isCritical)
    {
        return new AutomationStep(
            "CloseApplications.personal", AutomationStepType.CloseApplications, "Close applications", isCritical,
            TimeSpan.FromSeconds(10), new AppsArguments(apps).ToDictionary());
    }

    private static AutomationStep LaunchApplicationsStep(IReadOnlyList<string> apps, bool isCritical)
    {
        return new AutomationStep(
            "LaunchApplications.work", AutomationStepType.LaunchApplications, "Launch applications", isCritical,
            TimeSpan.FromSeconds(15), new AppsArguments(apps).ToDictionary());
    }
}
