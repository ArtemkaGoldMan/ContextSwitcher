using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.Automation;
using ContextSwitcher.Infrastructure.Browser;
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
            stepTimeout, new Dictionary<string, string> { ["apps"] = "Chess,Stickies" });

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
            stepTimeout, new Dictionary<string, string> { ["apps"] = "Calculator,Stickies" });

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

    [Fact]
    public async Task ExecuteAsyncSetThemeLeavesHeadroomToReportItsOwnFailure()
    {
        FakeScriptRunner scriptRunner = new();
        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock());

        AutomationStep step = new(
            "SetTheme.work", AutomationStepType.SetTheme, "Set theme", false,
            TimeSpan.FromSeconds(5), new Dictionary<string, string> { ["mode"] = "Dark" });

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
    public async Task ExecuteAsyncSetThemeRunsDarkModeScriptForDarkMode()
    {
        FakeScriptRunner scriptRunner = new();
        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock());
        AutomationStep step = new(
            "SetTheme.work", AutomationStepType.SetTheme, "Set theme", false, TimeSpan.FromSeconds(5),
            new Dictionary<string, string> { ["mode"] = "Dark" });

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Succeeded, result.Status);
        Assert.Contains("dark mode to true", Assert.Single(scriptRunner.Scripts));
    }

    [Fact]
    public async Task ExecuteAsyncSetWallpaperWarnsWithoutCallingScriptRunnerWhenFileMissing()
    {
        FakeScriptRunner scriptRunner = new();
        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock());
        AutomationStep step = new(
            "SetWallpaper.work", AutomationStepType.SetWallpaper, "Set wallpaper", true, TimeSpan.FromSeconds(10),
            new Dictionary<string, string> { ["path"] = "/definitely/missing/wallpaper.jpg", ["allSpaces"] = "True" });

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Warning, result.Status);
        Assert.Empty(scriptRunner.Scripts);
    }

    [Fact]
    public async Task ExecuteAsyncManageBrowserContextSucceedsWhenUrlOpensCleanly()
    {
        FakeProcessRunner processRunner = new();
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());
        AutomationStep step = ManageBrowserContextStep(
            mode: "Urls", browser: "Default", urls: "https://example.com/", isCritical: false);

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
            mode: "Urls", browser: "Default", urls: "https://example.com/", isCritical: true);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Failed, result.Status);
    }

    [Fact]
    public async Task ExecuteAsyncManageBrowserContextDeserializesProfilesJsonAndLaunchesThem()
    {
        FakeProcessRunner processRunner = new();
        AutomationStepExecutor executor = CreateExecutor(processRunner, new FakeScriptRunner(), new FakeClock());

        Dictionary<string, string> arguments = new()
        {
            ["mode"] = "Profiles",
            ["browser"] = "Default",
            ["urls"] = string.Empty,
            ["tabGroups"] = string.Empty,
            ["avoidDuplicateTabs"] = "True",
            ["profilesJson"] = "[{\"browser\":\"Chrome\",\"profile_directory\":\"Profile 1\",\"urls\":[\"https://mail.example/\"]}]"
        };
        AutomationStep step = new("ManageBrowserContext.work", AutomationStepType.ManageBrowserContext, "Manage browser context", false, TimeSpan.FromSeconds(20), arguments);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Succeeded, result.Status);
        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal(["-na", "Google Chrome", "--args", "--profile-directory=Profile 1", "https://mail.example/"], call.Arguments);
    }

    private static AutomationStep ManageBrowserContextStep(string mode, string browser, string urls, bool isCritical)
    {
        Dictionary<string, string> arguments = new()
        {
            ["mode"] = mode,
            ["browser"] = browser,
            ["urls"] = urls,
            ["tabGroups"] = string.Empty,
            ["avoidDuplicateTabs"] = "False",
            ["profilesJson"] = "[]"
        };
        return new AutomationStep("ManageBrowserContext.work", AutomationStepType.ManageBrowserContext, "Manage browser context", isCritical, TimeSpan.FromSeconds(20), arguments);
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
            new Dictionary<string, string> { ["containers"] = "postgres-work,redis-work" });

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
            new Dictionary<string, string> { ["containers"] = "redis-work" });

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Failed, result.Status);
    }

    [Fact]
    public async Task ExecuteAsyncControlMediaSkipsWhenAutoPlayDisabled()
    {
        FakeScriptRunner scriptRunner = new();
        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock());
        AutomationStep step = MediaStep(player: "AppleMusic", playlist: "Deep Focus", autoPlay: false, isCritical: false);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Succeeded, result.Status);
        Assert.Empty(scriptRunner.Scripts);
    }

    [Fact]
    public async Task ExecuteAsyncControlMediaWarnsWhenPlaylistMissing()
    {
        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), new FakeScriptRunner(), new FakeClock());
        AutomationStep step = MediaStep(player: "AppleMusic", playlist: "", autoPlay: true, isCritical: false);

        AutomationResult result = await executor.ExecuteAsync(step, CancellationToken.None);

        Assert.Equal(AutomationResultStatus.Warning, result.Status);
    }

    [Fact]
    public async Task ExecuteAsyncControlMediaPlaysAppleMusicPlaylist()
    {
        FakeScriptRunner scriptRunner = new();
        AutomationStepExecutor executor = CreateExecutor(new FakeProcessRunner(), scriptRunner, new FakeClock());
        AutomationStep step = MediaStep(player: "AppleMusic", playlist: "Deep Focus", autoPlay: true, isCritical: false);

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
        AutomationStep step = MediaStep(player: "Spotify", playlist: "spotify:playlist:abc", autoPlay: true, isCritical: true);

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

    private static AutomationStep MediaStep(string player, string playlist, bool autoPlay, bool isCritical)
    {
        Dictionary<string, string> arguments = new()
        {
            ["player"] = player,
            ["playlist"] = playlist,
            ["autoPlay"] = autoPlay ? "True" : "False"
        };
        return new AutomationStep("ControlMedia.work", AutomationStepType.ControlMedia, "Control media", isCritical, TimeSpan.FromSeconds(8), arguments);
    }

    private static AutomationStep FocusStep(bool enabled, string modeName, bool isCritical)
    {
        Dictionary<string, string> arguments = new()
        {
            ["enabled"] = enabled ? "True" : "False",
            ["modeName"] = modeName
        };
        return new AutomationStep("SetFocusMode.work", AutomationStepType.SetFocusMode, "Set Focus mode", isCritical, TimeSpan.FromSeconds(10), arguments);
    }

    private static AutomationStepExecutor CreateExecutor(FakeProcessRunner processRunner, FakeScriptRunner scriptRunner, FakeClock clock)
    {
        return new AutomationStepExecutor(processRunner, scriptRunner, new BrowserLauncher(processRunner, scriptRunner), clock);
    }

    private static AutomationStep CloseApplicationsStep(IReadOnlyList<string> apps, bool isCritical)
    {
        return new AutomationStep(
            "CloseApplications.personal", AutomationStepType.CloseApplications, "Close applications", isCritical,
            TimeSpan.FromSeconds(10), new Dictionary<string, string> { ["apps"] = string.Join(',', apps) });
    }

    private static AutomationStep LaunchApplicationsStep(IReadOnlyList<string> apps, bool isCritical)
    {
        return new AutomationStep(
            "LaunchApplications.work", AutomationStepType.LaunchApplications, "Launch applications", isCritical,
            TimeSpan.FromSeconds(15), new Dictionary<string, string> { ["apps"] = string.Join(',', apps) });
    }
}
