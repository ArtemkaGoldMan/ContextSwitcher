using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.Windows.Applications;
using ConfiguredApp = ContextSwitcher.Core.Configuration.AppIdentity;
using static ContextSwitcher.Tests.Windows.Applications.StartMenuRig;

namespace ContextSwitcher.Tests.Windows.Applications;

/// <summary>The <c>apps</c> map on Windows: what is recorded for an app, and how it is read when the app is found, opened or closed.</summary>
public sealed class AppsMapWindowsTests
{
    private const string Code = @"C:\Users\someone\AppData\Local\Programs\Microsoft VS Code\Code.exe";
    private const string Notepad = @"C:\Windows\notepad.exe";

    private static async Task<StartMenuRig> Rig(params StartApp[] apps)
    {
        StartMenuRig rig = new(apps);
        await rig.ListAsync().ConfigureAwait(false);
        return rig;
    }

    private static readonly StartApp VsCode = new("Visual Studio Code", "Microsoft.VisualStudioCode") { Target = Code };

    // What is recorded.

    [Fact]
    public async Task ADesktopAppIsRecordedWithItsIdAndTheFullPathOfItsExecutable()
    {
        StartMenuRig rig = await Rig(VsCode);
        WindowsAppIdentityCatalog catalog = new(rig.StartApps, rig.Identities);

        ConfiguredApp? entry = await catalog.IdentifyAsync("visual studio code", CancellationToken.None);

        Assert.Equal(new ConfiguredApp { WindowsAppId = "Microsoft.VisualStudioCode", WindowsExe = Code }, entry);
    }

    [Fact]
    public async Task AStoreAppIsRecordedByItsIdAloneForItsProcessesCarryItsPackage()
    {
        StartMenuRig rig = await Rig(new StartApp("Microsoft Teams", "MSTeams_8wekyb3d8bbwe!MSTeams"));

        ConfiguredApp? entry = await new WindowsAppIdentityCatalog(rig.StartApps, rig.Identities).IdentifyAsync("Microsoft Teams", CancellationToken.None);

        Assert.Equal(new ConfiguredApp { WindowsAppId = "MSTeams_8wekyb3d8bbwe!MSTeams" }, entry);
    }

    /// <summary>The id is kept as the Start menu has it, to be opened by; the executable is written out.</summary>
    [Fact]
    public async Task AKnownFoldersGuidInTheIdIsKeptInTheIdAndWrittenOutInTheExecutable()
    {
        StartMenuRig rig = await Rig(new StartApp("Steam", @"{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}\Steam\Steam.exe"));
        rig.Folders.Folders[Guid.Parse("7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E")] = @"C:\Program Files (x86)";

        ConfiguredApp? entry = await new WindowsAppIdentityCatalog(rig.StartApps, rig.Identities).IdentifyAsync("Steam", CancellationToken.None);

        Assert.Equal(@"{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}\Steam\Steam.exe", entry!.WindowsAppId);
        Assert.Equal(@"C:\Program Files (x86)\Steam\Steam.exe", entry.WindowsExe);
    }

    [Fact]
    public async Task AnAppThatHasNoExecutableToTellIsStillRecordedByItsId()
    {
        StartMenuRig rig = await Rig(new StartApp("Edge", "MSEdge"));

        ConfiguredApp? entry = await new WindowsAppIdentityCatalog(rig.StartApps, rig.Identities).IdentifyAsync("Edge", CancellationToken.None);

        Assert.Equal(new ConfiguredApp { WindowsAppId = "MSEdge" }, entry);
    }

    [Fact]
    public async Task AnAppNotInTheStartMenuHasNothingToRecord()
    {
        StartMenuRig rig = await Rig(VsCode);

        Assert.Null(await new WindowsAppIdentityCatalog(rig.StartApps, rig.Identities).IdentifyAsync("Nope", CancellationToken.None));
    }

    [Fact]
    public async Task RecordingListsTheStartMenuIfNothingHas()
    {
        StartMenuRig rig = new(VsCode);

        ConfiguredApp? entry = await new WindowsAppIdentityCatalog(rig.StartApps, rig.Identities).IdentifyAsync("Visual Studio Code", CancellationToken.None);

        Assert.NotNull(entry);
    }

    // How it is read, to find an app's processes.

    [Fact]
    public async Task WhatWasRecordedFindsTheAppEvenWhenTheStartMenuCallsItSomethingElse()
    {
        StartMenuRig rig = await Rig(new StartApp("Code - OSS", "Microsoft.VisualStudioCode") { Target = Code });
        rig.AppsMap.Entries["Visual Studio Code"] = new ConfiguredApp { WindowsAppId = "Microsoft.VisualStudioCode", WindowsExe = Code };

        Assert.Equal(new AppIdentity(Code, null), rig.Identities.Find("Visual Studio Code"));
    }

    [Fact]
    public async Task AnExecutableThePersonWroteWinsOverTheStartMenu()
    {
        StartMenuRig rig = await Rig(VsCode);
        rig.AppsMap.Entries["Visual Studio Code"] = new ConfiguredApp { WindowsExe = @"D:\Portable\Code.exe" };

        Assert.Equal(new AppIdentity(@"D:\Portable\Code.exe", null), rig.Identities.Find("Visual Studio Code"));
    }

    /// <summary>The settings may say "slack.exe": a program of that name, wherever it is.</summary>
    [Fact]
    public async Task AFileNameAloneIsAProgramOfThatNameAnywhere()
    {
        StartMenuRig rig = await Rig();
        rig.AppsMap.Entries["Slack"] = new ConfiguredApp { WindowsExe = "slack.exe" };
        rig.Pc.Running = [Process(100, 1, @"C:\Users\someone\AppData\Local\slack\app-4\slack.exe"), Process(200, 1, @"C:\Other\notslack.exe")];

        Assert.True(rig.Processes.IsRunning("Slack"));
        rig.Processes.ForceQuit("Slack");

        Assert.Equal([100], rig.Pc.Terminated);
    }

    [Fact]
    public async Task AStoreAppsIdNeedsNoStartMenuEntryToTellItsProcesses()
    {
        StartMenuRig rig = await Rig();
        rig.AppsMap.Entries["Teams"] = new ConfiguredApp { WindowsAppId = "MSTeams_8wekyb3d8bbwe!MSTeams" };

        Assert.Equal(new AppIdentity(null, "MSTeams_8wekyb3d8bbwe"), rig.Identities.Find("Teams"));
    }

    [Fact]
    public async Task AnIdTheStartMenuListsGivesWhatTheStartMenuSaysOfIt()
    {
        StartMenuRig rig = await Rig(VsCode);
        rig.AppsMap.Entries["My Editor"] = new ConfiguredApp { WindowsAppId = "Microsoft.VisualStudioCode" };

        Assert.Equal(new AppIdentity(Code, null), rig.Identities.Find("My Editor"));
    }

    /// <summary>An entry that says nothing of use - an id the Start menu doesn't have, a relative path - is not a reason to lose the app.</summary>
    [Theory]
    [InlineData("Gone.App", null)]
    [InlineData(null, @"bin\slack.exe")]
    [InlineData(null, "notes.txt")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public async Task AnEntryThatSaysNothingOfUseFallsBackToTheStartMenu(string? id, string? exe)
    {
        StartMenuRig rig = await Rig(VsCode);
        rig.AppsMap.Entries["Visual Studio Code"] = new ConfiguredApp { WindowsAppId = id, WindowsExe = exe };

        Assert.Equal(new AppIdentity(Code, null), rig.Identities.Find("Visual Studio Code"));
    }

    [Fact]
    public async Task AnAppWithNoEntryIsFoundByTheStartMenu()
    {
        StartMenuRig rig = await Rig(VsCode);

        Assert.Equal(new AppIdentity(Code, null), rig.Identities.Find("Visual Studio Code"));
    }

    [Fact]
    public async Task AFileNameAloneStillProtectsWhatWindowsCannotRunWithout()
    {
        StartMenuRig rig = await Rig();
        rig.AppsMap.Entries["Shell"] = new ConfiguredApp { WindowsExe = "explorer.exe" };
        rig.Pc.Running = [Process(100, 1, @"C:\Windows\explorer.exe")];

        rig.Processes.ForceQuit("Shell");

        Assert.Empty(rig.Pc.Terminated);
    }

    // How it is read, to open an app.

    private static ProcessResult Opened => new(1, string.Empty, string.Empty, TimedOut: false);

    [Fact]
    public async Task AnAppTheStartMenuHasRenamedIsOpenedByTheIdTheMapKept()
    {
        StartMenuRig rig = await Rig(new StartApp("Code - OSS", "Microsoft.VisualStudioCode") { Target = Code });
        rig.AppsMap.Entries["Visual Studio Code"] = new ConfiguredApp { WindowsAppId = "Microsoft.VisualStudioCode" };
        rig.Runner.Enqueue(Opened);

        AppLaunch launch = await rig.Launcher.LaunchAsync("Visual Studio Code", TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal(AppLaunchOutcome.Launched, launch.Outcome);
        Assert.Equal([@"shell:AppsFolder\Microsoft.VisualStudioCode"], rig.Runner.Calls[^1].Arguments);
    }

    /// <summary>Which of two apps of one name is meant is the person's to say.</summary>
    [Fact]
    public async Task TheMapChoosesBetweenAppsOfOneName()
    {
        StartMenuRig rig = await Rig(new StartApp("Outlook", "Microsoft.Office.OUTLOOK.EXE.15"));
        rig.AppsMap.Entries["Outlook"] = new ConfiguredApp { WindowsAppId = "Microsoft.Office.OUTLOOK.EXE.15" };
        rig.Runner.Enqueue(Opened);

        await rig.Launcher.LaunchAsync("Outlook", TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal([@"shell:AppsFolder\Microsoft.Office.OUTLOOK.EXE.15"], rig.Runner.Calls[^1].Arguments);
    }

    /// <summary>An uninstalled app is still said to be not installed, not opened by an id that leads nowhere.</summary>
    [Fact]
    public async Task AnIdTheStartMenuNoLongerListsIsNotOpenedAndTheNameIsTried()
    {
        StartMenuRig rig = await Rig(VsCode);
        rig.AppsMap.Entries["Visual Studio Code"] = new ConfiguredApp { WindowsAppId = "Uninstalled.App" };
        rig.Runner.Enqueue(Opened);

        await rig.Launcher.LaunchAsync("Visual Studio Code", TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal([@"shell:AppsFolder\Microsoft.VisualStudioCode"], rig.Runner.Calls[^1].Arguments);
    }

    [Fact]
    public async Task AnAppNeitherTheMapNorTheStartMenuHasIsNotInstalled()
    {
        StartMenuRig rig = await Rig(VsCode);
        rig.AppsMap.Entries["Slack"] = new ConfiguredApp { WindowsAppId = "Uninstalled.Slack" };

        AppLaunch launch = await rig.Launcher.LaunchAsync("Slack", TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal(AppLaunchOutcome.NotInstalled, launch.Outcome);
        Assert.Single(rig.Runner.Calls);
    }

    [Fact]
    public async Task TheIdIsMatchedWithoutCaseOrSpaces()
    {
        StartMenuRig rig = await Rig(VsCode);
        rig.AppsMap.Entries["Editor"] = new ConfiguredApp { WindowsAppId = "  microsoft.visualstudiocode " };
        rig.Runner.Enqueue(Opened);

        AppLaunch launch = await rig.Launcher.LaunchAsync("Editor", TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal(AppLaunchOutcome.Launched, launch.Outcome);
    }

    [Fact]
    public async Task NotepadsRecordedEntryAlsoStopsItBeingStartedTwice()
    {
        StartMenuRig rig = await Rig(new StartApp("Notepad", Notepad));
        rig.AppsMap.Entries["Notepad"] = new ConfiguredApp { WindowsAppId = Notepad, WindowsExe = Notepad };
        rig.Pc.Running = [Process(100, 1, Notepad)];
        rig.Windows.Open = [Window(1, 100)];

        AppLaunch launch = await rig.Launcher.LaunchAsync("Notepad", TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal(AppLaunchOutcome.Launched, launch.Outcome);
        Assert.DoesNotContain(rig.Runner.Calls, call => call.FileName == "explorer");
    }
}
