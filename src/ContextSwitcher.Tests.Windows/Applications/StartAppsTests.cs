using System.Text;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Infrastructure.Windows.Applications;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Windows.Applications;

/// <summary>The Start menu's apps, listed once through PowerShell, and opened by their name.</summary>
public sealed class StartAppsTests
{
    private static readonly StartAppsOptions Options = new(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5), StartAppsFilter.Default);

    private readonly FakeProcessRunner runner = new();
    private readonly FakeClock clock = new();
    private readonly TestLogger logger = new();

    [Fact]
    public void ANameAndAnIdAreReadAsBase64OfUtf8()
    {
        IReadOnlyList<StartApp> apps = Parse(Listing(("Telegram Десктоп", "TelegramMessengerLLP.TelegramDesktop")));

        Assert.Equal(new StartApp("Telegram Десктоп", "TelegramMessengerLLP.TelegramDesktop"), Assert.Single(apps));
    }

    /// <summary>PowerShell prints one result as an object, not an array of one.</summary>
    [Fact]
    public void OneAppComesAsAnObject()
    {
        string entry = $$"""{"n":"{{Encode("Slack")}}","i":"{{Encode("com.squirrel.slack.slack")}}"}""";

        Assert.Equal("Slack", Assert.Single(Parse(entry)).Name);
    }

    [Fact]
    public void AppsAreSortedByNameAndEachNameIsOnce()
    {
        IReadOnlyList<StartApp> apps = Parse(Listing(("Slack", "a"), ("Code", "b"), ("slack", "c")));

        Assert.Equal(["Code", "Slack"], apps.Select(app => app.Name));
        Assert.Equal("a", apps[1].AppId);
    }

    /// <summary>What Windows lists next to the apps: the Start menu of a real PC.</summary>
    [Fact]
    public void UninstallersHelpFilesConsolesAndWebLinksAreLeftOut()
    {
        IReadOnlyList<StartApp> apps = Parse(Listing(
            ("Uninstall Telegram", @"C:\Users\someone\AppData\Roaming\Telegram Desktop\unins000.exe"),
            ("Uninstall", @"{7C5A40EF}\Ubisoft\Ubisoft Game Launcher\Uninstall.exe"),
            ("VLC Documentation", @"{6D809377}\VideoLAN\VLC\Documentation.url"),
            ("HWiNFO Manual", @"{6D809377}\HWiNFO64\HWiNFO Manual.pdf"),
            ("Services", @"{1AC14E77}\services.msc"),
            ("VideoLAN website", "https://www.videolan.org/"),
            ("Git FAQs", "HTTP://gitforwindows.org/faq"),
            ("Telegram", "Telegram.TelegramDesktop")));

        Assert.Equal(["Telegram"], apps.Select(app => app.Name));
    }

    [Fact]
    public void AppsThatOpenInAnyOtherWayAreKept()
    {
        IReadOnlyList<StartApp> apps = Parse(Listing(
            ("Visual Studio Code", "Microsoft.VisualStudioCode"),
            ("Microsoft Teams", "MSTeams_8wekyb3d8bbwe!MSTeams"),
            ("Steam", @"{7C5A40EF}\Steam\Steam.EXE"),
            ("Apex Legends", "steam://rungameid/1172470"),
            ("Skype для бізнесу", "Microsoft.Office.lync.exe.15")));

        Assert.Equal(["Apex Legends", "Microsoft Teams", "Skype для бізнесу", "Steam", "Visual Studio Code"], apps.Select(app => app.Name));
    }

    /// <summary>A name twice is one app; the one an AppUserModelID opens wins, whichever comes first.</summary>
    [Fact]
    public void OfTwoAppsWithOneNameTheOneWithAnAppUserModelIdWins()
    {
        IReadOnlyList<StartApp> apps = Parse(Listing(
            ("Outlook", @"{6D809377}\Microsoft Office\Outlook.exe"),
            ("Outlook", "Microsoft.OutlookForWindows_8wekyb3d8bbwe!App")));

        Assert.Equal("Microsoft.OutlookForWindows_8wekyb3d8bbwe!App", Assert.Single(apps).AppId);
    }

    /// <summary>A command line takes a double quote apart, and the script goes on one.</summary>
    [Fact]
    public void TheScriptHoldsNoDoubleQuote()
    {
        Assert.DoesNotContain('"', StartApps.Script);
    }

    [Fact]
    public void AShortcutsTargetIsReadWithTheApp()
    {
        string json = $$"""[{"n":"{{Encode("Code")}}","i":"{{Encode("Microsoft.VisualStudioCode")}}","t":"{{Encode(@"C:\Code\Code.exe")}}"},{"n":"{{Encode("Edge")}}","i":"{{Encode("MSEdge")}}","t":"{{Encode(string.Empty)}}"},{"n":"{{Encode("Slack")}}","i":"{{Encode("a!b")}}"}]""";

        IReadOnlyList<StartApp> apps = Parse(json);

        Assert.Equal(@"C:\Code\Code.exe", apps.Single(app => app.Name == "Code").Target);
        Assert.Null(apps.Single(app => app.Name == "Edge").Target);
        Assert.Null(apps.Single(app => app.Name == "Slack").Target);
    }

    [Fact]
    public async Task WhatWasListedIsKnownAfterItsKeepingHasRunOut()
    {
        this.runner.Enqueue(Success(Listing(("Slack", "a"))));
        StartApps startApps = this.NewStartApps();
        Assert.Empty(startApps.Known);

        await startApps.GetAsync(CancellationToken.None);
        this.clock.UtcNow += TimeSpan.FromHours(3);

        Assert.Equal("Slack", Assert.Single(startApps.Known).Name);
    }

    [Fact]
    public void AnEntryThatDoesNotReadIsSkipped()
    {
        string json = $$"""[{"n":"not base64!","i":"{{Encode("x")}}"},{"n":"{{Encode("Code")}}"},{"n":"{{Encode("Slack")}}","i":"{{Encode("s")}}"}]""";

        Assert.Equal(["Slack"], Parse(json).Select(app => app.Name));
    }

    [Fact]
    public void NothingPrintedIsNoApps()
    {
        Assert.Empty(Parse("  \r\n"));
    }

    [Fact]
    public async Task TheListIsAskedForOnceWhileItIsKept()
    {
        this.runner.Enqueue(Success(Listing(("Slack", "a"))));
        StartApps startApps = this.NewStartApps();

        await startApps.GetAsync(CancellationToken.None);
        this.clock.UtcNow += TimeSpan.FromMinutes(4);
        IReadOnlyList<StartApp> again = await startApps.GetAsync(CancellationToken.None);

        Assert.Single(again);
        ProcessStartOptions call = Assert.Single(this.runner.Calls);
        Assert.Equal("powershell", call.FileName);
        Assert.Equal(["-NoProfile", "-NonInteractive", "-Command", StartApps.Script], call.Arguments);
    }

    [Fact]
    public async Task TheListIsAskedForAgainWhenItHasGoneStale()
    {
        this.runner.Enqueue(Success(Listing(("Slack", "a"))));
        this.runner.Enqueue(Success(Listing(("Slack", "a"), ("Code", "b"))));
        StartApps startApps = this.NewStartApps();

        await startApps.GetAsync(CancellationToken.None);
        this.clock.UtcNow += TimeSpan.FromMinutes(6);

        Assert.Equal(2, (await startApps.GetAsync(CancellationToken.None)).Count);
    }

    /// <summary>A failed listing is logged, is no apps, and is tried again at the next ask.</summary>
    [Fact]
    public async Task AListingThatFailedIsTriedAgain()
    {
        this.runner.Enqueue(new ProcessResult(1, string.Empty, "denied", TimedOut: false));
        this.runner.Enqueue(Success(Listing(("Slack", "a"))));
        StartApps startApps = this.NewStartApps();

        Assert.Empty(await startApps.GetAsync(CancellationToken.None));
        Assert.Single(await startApps.GetAsync(CancellationToken.None));
        Assert.Contains(this.logger.Entries, entry => entry.EventId == "StartAppsNotListed");
    }

    [Fact]
    public async Task PowerShellSayingGibberishIsLoggedAndIsNoApps()
    {
        this.runner.Enqueue(Success("not json"));

        Assert.Empty(await this.NewStartApps().GetAsync(CancellationToken.None));
        Assert.Contains(this.logger.Entries, entry => entry.EventId == "StartAppsNotRead");
    }

    [Fact]
    public async Task AnAppOpensThroughItsAppsFolderEntry()
    {
        this.runner.Enqueue(Success(Listing(("Visual Studio Code", @"C:\Program Files\Microsoft VS Code\Code.exe"))));
        this.runner.Enqueue(new ProcessResult(1, string.Empty, string.Empty, TimedOut: false));

        AppLaunch launch = await this.Launcher().LaunchAsync(" visual studio code ", TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.Equal(AppLaunchOutcome.Launched, launch.Outcome);
        ProcessStartOptions open = this.runner.Calls[1];
        Assert.Equal("explorer", open.FileName);
        Assert.Equal([@"shell:AppsFolder\C:\Program Files\Microsoft VS Code\Code.exe"], open.Arguments);
    }

    [Fact]
    public async Task AnAppNotInTheStartMenuIsNotInstalled()
    {
        this.runner.Enqueue(Success(Listing(("Slack", "a"))));

        AppLaunch launch = await this.Launcher().LaunchAsync("Slak", TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.Equal(AppLaunchOutcome.NotInstalled, launch.Outcome);
        Assert.Single(this.runner.Calls);
    }

    [Fact]
    public async Task AnAppThatDoesNotOpenInTimeFailed()
    {
        this.runner.Enqueue(Success(Listing(("Slack", "a"))));
        this.runner.Enqueue(new ProcessResult(-1, string.Empty, string.Empty, TimedOut: true));

        AppLaunch launch = await this.Launcher().LaunchAsync("Slack", TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.Equal(AppLaunchOutcome.Failed, launch.Outcome);
    }

    private static IReadOnlyList<StartApp> Parse(string output) => StartAppsParser.Parse(output, StartAppsFilter.Default);

    private static ProcessResult Success(string output) => new(0, output, string.Empty, TimedOut: false);

    private static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    private static string Listing(params (string Name, string Id)[] apps) =>
        "[" + string.Join(",", apps.Select(app => $$"""{"n":"{{Encode(app.Name)}}","i":"{{Encode(app.Id)}}"}""")) + "]";

    private StartApps NewStartApps() => new(this.runner, this.clock, this.logger, Options);

    private WindowsAppLauncher Launcher()
    {
        StartApps startApps = this.NewStartApps();
        FakeSystemProcesses pc = new();
        FakeSystemWindows windows = new();
        WindowsAppProcesses processes = new(new AppIdentities(startApps, new FakeKnownFolders()), pc, windows, AppProcessesOptions.Default, this.logger);
        return new(this.runner, startApps, new AppWindows(processes, windows, pc));
    }
}
