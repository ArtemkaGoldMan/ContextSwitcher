using ContextSwitcher.Infrastructure.Windows.Applications;
using static ContextSwitcher.Tests.Windows.Applications.StartMenuRig;

namespace ContextSwitcher.Tests.Windows.Applications;

/// <summary>Whether an app runs, whether it has a window on screen, and ending it - against a PC the test makes up.</summary>
public sealed class WindowsAppProcessesTests
{
    private const string Steam = @"C:\Program Files (x86)\Steam\steam.exe";
    private const string SteamHelper = @"C:\Program Files (x86)\Steam\bin\cef\steamwebhelper.exe";

    private static StartApp SteamEntry => new("Steam", Steam);

    private static async Task<StartMenuRig> SteamRig(params ProcessInfo[] running)
    {
        StartMenuRig rig = new(SteamEntry);
        rig.Pc.Running = [.. running];
        await rig.ListAsync().ConfigureAwait(false);
        return rig;
    }

    [Fact]
    public async Task AnAppWithAProcessIsRunning()
    {
        StartMenuRig rig = await SteamRig(Process(100, 1, Steam), Process(200, 1, @"C:\Windows\notepad.exe"));

        Assert.True(rig.Processes.IsRunning("Steam"));
    }

    [Fact]
    public async Task AnAppWithNoProcessIsNotRunning()
    {
        StartMenuRig rig = await SteamRig(Process(200, 1, @"C:\Windows\notepad.exe"));

        Assert.False(rig.Processes.IsRunning("Steam"));
    }

    [Fact]
    public async Task AnAppTheStartMenuDoesNotHaveCannotBeTold()
    {
        StartMenuRig rig = await SteamRig();

        Assert.Null(rig.Processes.IsRunning("Not An App"));
        Assert.Null(rig.Processes.HasWindowOnScreen("Not An App"));
        Assert.False(rig.Processes.ForceQuit("Not An App"));
    }

    [Fact]
    public void BeforeTheStartMenuIsListedNothingCanBeTold()
    {
        StartMenuRig rig = new(SteamEntry);
        rig.Pc.Running = [Process(100, 1, Steam)];

        Assert.Null(rig.Processes.IsRunning("Steam"));
    }

    [Fact]
    public async Task WhenWindowsWillNotListProcessesNothingCanBeTold()
    {
        StartMenuRig rig = await SteamRig();
        rig.Pc.Running = null;

        Assert.Null(rig.Processes.IsRunning("Steam"));
        Assert.Null(rig.Processes.HasWindowOnScreen("Steam"));
        Assert.False(rig.Processes.ForceQuit("Steam"));
        Assert.Empty(rig.Pc.Terminated);
    }

    [Fact]
    public async Task ThisAppsOwnProcessIsNeverTheAppsEvenWhenItsExecutableMatches()
    {
        StartMenuRig rig = await SteamRig(Process(100, 1, Steam));
        rig.Pc.CurrentProcessId = 100;

        Assert.False(rig.Processes.IsRunning("Steam"));
        Assert.False(rig.Processes.ForceQuit("Steam"));
        Assert.Empty(rig.Pc.Terminated);
    }

    [Fact]
    public async Task AShownWindowIsOnScreen()
    {
        StartMenuRig rig = await SteamRig(Process(100, 1, Steam));
        rig.Windows.Open = [Window(1, 100)];

        Assert.True(rig.Processes.HasWindowOnScreen("Steam"));
    }

    /// <summary>Steam draws every window it shows in a helper it started.</summary>
    [Fact]
    public async Task AWindowOfAProcessTheAppStartedIsTheAppsOwn()
    {
        StartMenuRig rig = await SteamRig(Process(100, 1, Steam), Process(300, 100, SteamHelper));
        rig.Windows.Open = [Window(1, 300)];

        Assert.True(rig.Processes.HasWindowOnScreen("Steam"));
    }

    [Fact]
    public async Task AWindowOfAnotherAppIsNotTheAppsOwn()
    {
        StartMenuRig rig = await SteamRig(Process(100, 1, Steam), Process(200, 1, @"C:\Windows\notepad.exe"));
        rig.Windows.Open = [Window(1, 200)];

        Assert.False(rig.Processes.HasWindowOnScreen("Steam"));
    }

    [Theory]
    [InlineData(false, false, false, false, 800, 600)]
    [InlineData(true, true, false, false, 800, 600)]
    [InlineData(true, false, false, true, 800, 600)]
    [InlineData(true, false, false, false, 99, 600)]
    [InlineData(true, false, false, false, 800, 99)]
    [InlineData(true, false, false, false, 0, 0)]
    public async Task AWindowThatIsHiddenCloakedAPaletteOrTinyIsNotOnScreen(bool visible, bool cloaked, bool minimized, bool tool, int width, int height)
    {
        StartMenuRig rig = await SteamRig(Process(100, 1, Steam));
        rig.Windows.Open = [Window(1, 100, visible, cloaked, minimized, owned: false, tool, width, height)];

        Assert.False(rig.Processes.HasWindowOnScreen("Steam"));
    }

    [Fact]
    public async Task AMinimisedWindowIsOnTheTaskbarAndSoOnScreen()
    {
        StartMenuRig rig = await SteamRig(Process(100, 1, Steam));
        rig.Windows.Open = [Window(1, 100, minimized: true, width: 160, height: 28)];

        Assert.True(rig.Processes.HasWindowOnScreen("Steam"));
    }

    [Fact]
    public async Task AnAppNotRunningHasNoWindow()
    {
        StartMenuRig rig = await SteamRig();
        rig.Windows.Open = [Window(1, 100)];

        Assert.False(rig.Processes.HasWindowOnScreen("Steam"));
    }

    [Fact]
    public async Task WhenWindowsWillNotListWindowsItCannotBeTold()
    {
        StartMenuRig rig = await SteamRig(Process(100, 1, Steam));
        rig.Windows.Open = null;

        Assert.Null(rig.Processes.HasWindowOnScreen("Steam"));
    }

    [Fact]
    public async Task ForceQuitEndsTheAppAndWhatItStarted()
    {
        StartMenuRig rig = await SteamRig(
            Process(100, 1, Steam),
            Process(300, 100, SteamHelper),
            Process(301, 300, SteamHelper),
            Process(200, 1, @"C:\Windows\notepad.exe"));

        Assert.True(rig.Processes.ForceQuit("Steam"));

        Assert.Equal([100, 300, 301], rig.Pc.Terminated.Order());
    }

    /// <summary>Windows gives an ended process's id to a later one, which the app never started.</summary>
    [Fact]
    public async Task AProcessThatStartedBeforeItsSupposedParentIsNotItsChild()
    {
        DateTime appStarted = new(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc);
        StartMenuRig rig = await SteamRig(
            Process(100, 1, Steam, started: appStarted),
            Process(400, 100, @"C:\Windows\notepad.exe", started: appStarted.AddHours(-5)),
            new ProcessInfo(401, 100, @"C:\Windows\mspaint.exe", null, null));

        rig.Processes.ForceQuit("Steam");

        Assert.Equal([100], rig.Pc.Terminated);
    }

    [Fact]
    public async Task ForceQuitNeverEndsWhatWindowsCannotRunWithout()
    {
        StartMenuRig rig = new(new StartApp("File Explorer", @"C:\Windows\explorer.exe"));
        rig.Pc.Running = [Process(100, 1, @"C:\Windows\explorer.exe"), Process(101, 100, @"C:\Windows\system32\svchost.exe")];
        await rig.ListAsync();

        Assert.True(rig.Processes.ForceQuit("File Explorer"));

        Assert.Empty(rig.Pc.Terminated);
        Assert.Contains(rig.Logger.Entries, entry => entry.EventId == "ForceQuitSkipped");
    }

    /// <summary>The app being asked to switch can be a child of an app it closes: it would end itself.</summary>
    [Fact]
    public async Task ThisAppAndWhatItStartedAreNeverEndedEvenAsAChildOfTheApp()
    {
        StartMenuRig rig = await SteamRig(
            Process(100, 1, Steam),
            Process(500, 100, @"C:\Apps\ContextSwitcher.exe"),
            Process(501, 500, @"C:\Windows\powershell.exe"));
        rig.Pc.CurrentProcessId = 500;

        rig.Processes.ForceQuit("Steam");

        Assert.Equal([100], rig.Pc.Terminated);
    }

    [Fact]
    public async Task AProcessWindowsWillNotEndIsLoggedAndTheRestAreStillEnded()
    {
        StartMenuRig rig = await SteamRig(Process(100, 1, Steam), Process(300, 100, SteamHelper));
        rig.Pc.Unendable.Add(300);

        rig.Processes.ForceQuit("Steam");

        Assert.Equal([100], rig.Pc.Terminated);
        Assert.Contains(rig.Logger.Entries, entry => entry.EventId == "ForceQuitFailed");
    }

    [Fact]
    public async Task AnAppThatIsNotRunningHasNothingToForceQuit()
    {
        StartMenuRig rig = await SteamRig(Process(200, 1, @"C:\Windows\notepad.exe"));

        Assert.False(rig.Processes.ForceQuit("Steam"));
        Assert.Empty(rig.Pc.Terminated);
    }

    [Fact]
    public async Task ProcessIdsThatPointAtEachOtherDoNotLoop()
    {
        DateTime start = new(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc);
        StartMenuRig rig = await SteamRig(
            Process(100, 300, Steam, started: start),
            Process(300, 100, SteamHelper, started: start));

        rig.Processes.ForceQuit("Steam");

        Assert.Equal([100, 300], rig.Pc.Terminated.Order());
    }
}
