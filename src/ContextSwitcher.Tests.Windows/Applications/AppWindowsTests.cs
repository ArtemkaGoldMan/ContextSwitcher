using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Infrastructure.Windows.Applications;
using static ContextSwitcher.Tests.Windows.Applications.StartMenuRig;

namespace ContextSwitcher.Tests.Windows.Applications;

/// <summary>Hiding an app by minimising its windows, bringing them back, and the apps open now.</summary>
public sealed class AppWindowsTests
{
    private const string Code = @"C:\Users\someone\AppData\Local\Programs\Microsoft VS Code\Code.exe";
    private const string Notepad = @"C:\Windows\notepad.exe";

    private static async Task<StartMenuRig> Rig(params ProcessInfo[] running)
    {
        StartMenuRig rig = new(
            new StartApp("Visual Studio Code", "Microsoft.VisualStudioCode") { Target = Code },
            new StartApp("Notepad", Notepad),
            new StartApp("Microsoft Teams", "MSTeams_8wekyb3d8bbwe!MSTeams"));
        rig.Pc.Running = [.. running];
        await rig.ListAsync().ConfigureAwait(false);
        return rig;
    }

    [Fact]
    public async Task HidingMinimisesTheMainWindowsOnScreen()
    {
        StartMenuRig rig = await Rig(Process(100, 1, Code), Process(101, 100, Code));
        rig.Windows.Open = [Window(1, 100), Window(2, 101)];

        int hidden = rig.Hider.Hide(["Visual Studio Code"]);

        Assert.Equal(1, hidden);
        Assert.Equal<nint>([1, 2], rig.Windows.Minimized.Order());
    }

    /// <summary>A dialog stays with its app; a palette, a hidden window, a minimised one and one on another desktop aren't minimised.</summary>
    [Fact]
    public async Task OnlyMainWindowsStillOnScreenAreMinimised()
    {
        StartMenuRig rig = await Rig(Process(100, 1, Code));
        rig.Windows.Open =
        [
            Window(1, 100),
            Window(2, 100, owned: true),
            Window(3, 100, tool: true),
            Window(4, 100, visible: false),
            Window(5, 100, minimized: true),
            Window(6, 100, cloaked: true)
        ];

        rig.Hider.Hide(["Visual Studio Code"]);

        Assert.Equal<nint>([1], rig.Windows.Minimized);
    }

    [Fact]
    public async Task HidingLeavesOtherAppsAlone()
    {
        StartMenuRig rig = await Rig(Process(100, 1, Code), Process(200, 1, Notepad));
        rig.Windows.Open = [Window(1, 100), Window(2, 200)];

        rig.Hider.Hide(["Visual Studio Code"]);

        Assert.Equal<nint>([1], rig.Windows.Minimized);
    }

    [Fact]
    public async Task OnlyAppsThatAreRunningAreCounted()
    {
        StartMenuRig rig = await Rig(Process(100, 1, Code));
        rig.Windows.Open = [Window(1, 100)];

        int hidden = rig.Hider.Hide(["Visual Studio Code", "Notepad", "Not An App", " "]);

        Assert.Equal(1, hidden);
    }

    [Fact]
    public async Task AnAppRunningOnlyInTheBackgroundIsNotHidden()
    {
        StartMenuRig rig = await Rig(Process(100, 1, Code));
        rig.Windows.Open = [Window(1, 100, visible: false)];

        Assert.Equal(0, rig.Hider.Hide(["Visual Studio Code"]));
        Assert.Empty(rig.Windows.Minimized);
    }

    [Fact]
    public async Task HidingNothingHidesNothing()
    {
        StartMenuRig rig = await Rig(Process(100, 1, Code));
        rig.Windows.Open = [Window(1, 100)];

        Assert.Equal(0, rig.Hider.Hide([]));
        Assert.Empty(rig.Windows.Minimized);
    }

    [Fact]
    public async Task RestoringBringsBackOnlyTheMinimisedMainWindows()
    {
        StartMenuRig rig = await Rig(Process(100, 1, Code));
        rig.Windows.Open = [Window(1, 100, minimized: true), Window(2, 100), Window(3, 100, minimized: true, owned: true)];

        int asked = rig.AppWindows.Restore("Visual Studio Code");

        Assert.Equal(1, asked);
        Assert.Equal<nint>([1], rig.Windows.Restored);
    }

    [Fact]
    public async Task RestoringAnAppThatIsNotRunningDoesNothing()
    {
        StartMenuRig rig = await Rig(Process(200, 1, Notepad));
        rig.Windows.Open = [Window(2, 200, minimized: true)];

        Assert.Equal(0, rig.AppWindows.Restore("Visual Studio Code"));
        Assert.Equal(0, rig.AppWindows.Restore("Not An App"));
        Assert.Empty(rig.Windows.Restored);
    }

    [Fact]
    public async Task RestoringCountsOnlyWhatWindowsTookUp()
    {
        StartMenuRig rig = await Rig(Process(100, 1, Code));
        rig.Windows.Open = [Window(1, 100, minimized: true), Window(2, 100, minimized: true)];
        rig.Windows.Unreachable.Add(2);

        Assert.Equal(1, rig.AppWindows.Restore("Visual Studio Code"));
    }

    /// <summary>Opening a profile again brings back what leaving it minimised - and no second window of the app.</summary>
    [Fact]
    public async Task OpeningAnAppAlreadyOpenRestoresItsMinimisedWindowsAndStartsNothing()
    {
        StartMenuRig rig = await Rig(Process(100, 1, Code));
        rig.Windows.Open = [Window(1, 100, minimized: true)];

        AppLaunch launch = await rig.Launcher.LaunchAsync("Visual Studio Code", TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal(AppLaunchOutcome.Launched, launch.Outcome);
        Assert.Equal<nint>([1], rig.Windows.Restored);
        Assert.DoesNotContain(rig.Runner.Calls, call => call.FileName == "explorer");
    }

    [Fact]
    public async Task OpeningAnAppWithAWindowOnScreenStartsNothingEither()
    {
        StartMenuRig rig = await Rig(Process(100, 1, Code));
        rig.Windows.Open = [Window(1, 100)];

        AppLaunch launch = await rig.Launcher.LaunchAsync("Visual Studio Code", TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal(AppLaunchOutcome.Launched, launch.Outcome);
        Assert.Empty(rig.Windows.Restored);
        Assert.DoesNotContain(rig.Runner.Calls, call => call.FileName == "explorer");
    }

    /// <summary>An app only in the tray, or one whose window Windows cloaks, has nothing to show: it is opened.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task OpeningAnAppWithNothingToShowStartsIt(bool hasWindow, bool cloaked)
    {
        StartMenuRig rig = await Rig(Process(100, 1, Code));
        rig.Windows.Open = hasWindow ? [Window(1, 100, cloaked: cloaked)] : [Window(1, 100, visible: false)];
        rig.Runner.Enqueue(new ContextSwitcher.Core.ProcessExecution.ProcessResult(1, string.Empty, string.Empty, TimedOut: false));

        await rig.Launcher.LaunchAsync("Visual Studio Code", TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal("explorer", rig.Runner.Calls[^1].FileName);
    }

    [Fact]
    public async Task OpeningAnAppThatIsNotRunningStartsIt()
    {
        StartMenuRig rig = await Rig();
        rig.Runner.Enqueue(new ContextSwitcher.Core.ProcessExecution.ProcessResult(1, string.Empty, string.Empty, TimedOut: false));

        await rig.Launcher.LaunchAsync("Visual Studio Code", TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Empty(rig.Windows.Restored);
        Assert.Equal("explorer", rig.Runner.Calls[^1].FileName);
    }

    [Fact]
    public async Task TheAppsOpenNowAreThoseRunningWithAMainWindow()
    {
        StartMenuRig rig = await Rig(Process(100, 1, Code), Process(200, 1, Notepad), Process(300, 1, @"C:\Other\other.exe"));
        rig.Windows.Open = [Window(1, 100), Window(2, 200), Window(3, 300)];

        IReadOnlyList<ContextSwitcher.Core.Applications.RunningApp> running = rig.Running.List();

        Assert.Equal(["Notepad", "Visual Studio Code"], running.Select(app => app.Name).Order());
    }

    [Fact]
    public async Task AnAppWithNoWindowOnTheTaskbarIsNotOpen()
    {
        StartMenuRig rig = await Rig(Process(100, 1, Code), Process(200, 1, Notepad), Process(400, 1, @"C:\x\ms-teams.exe", "MSTeams_8wekyb3d8bbwe"));
        rig.Windows.Open =
        [
            Window(1, 100, visible: false),
            Window(2, 200, owned: true),
            Window(4, 400, cloaked: true)
        ];

        Assert.Empty(rig.Running.List());
    }

    [Fact]
    public async Task AStoreAppAndAnAppWhoseWindowIsInAHelperAreOpenToo()
    {
        StartMenuRig rig = await Rig(
            Process(400, 1, @"C:\x\ms-teams.exe", "MSTeams_8wekyb3d8bbwe"),
            Process(100, 1, Code),
            Process(101, 100, Code));
        rig.Windows.Open = [Window(4, 400), Window(2, 101)];

        Assert.Equal(["Microsoft Teams", "Visual Studio Code"], rig.Running.List().Select(app => app.Name).Order());
    }

    /// <summary>This app is the one asking: never in its own list, nor an app it was started by that is only its parent.</summary>
    [Fact]
    public async Task ThisAppIsNotInTheList()
    {
        StartMenuRig rig = await Rig(Process(100, 1, Notepad));
        rig.Pc.CurrentProcessId = 100;
        rig.Windows.Open = [Window(1, 100)];

        Assert.Empty(rig.Running.List());
    }

    [Fact]
    public void BeforeTheStartMenuIsListedNothingIsKnownToBeOpen()
    {
        StartMenuRig rig = new(new StartApp("Notepad", Notepad));
        rig.Pc.Running = [Process(100, 1, Notepad)];
        rig.Windows.Open = [Window(1, 100)];

        Assert.Empty(rig.Running.List());
    }

    [Fact]
    public async Task WhenWindowsWillNotListNothingIsKnownToBeOpen()
    {
        StartMenuRig rig = await Rig(Process(100, 1, Notepad));
        rig.Windows.Open = null;

        Assert.Empty(rig.Running.List());

        rig.Windows.Open = [Window(1, 100)];
        rig.Pc.Running = null;

        Assert.Empty(rig.Running.List());
    }

    [Fact]
    public async Task TheListNamesAppsAsAProfileDoes()
    {
        StartMenuRig rig = await Rig(Process(100, 1, Notepad));
        rig.Windows.Open = [Window(1, 100)];

        Assert.True(rig.Running.List().Single().IsCalled("notepad"));
    }
}
