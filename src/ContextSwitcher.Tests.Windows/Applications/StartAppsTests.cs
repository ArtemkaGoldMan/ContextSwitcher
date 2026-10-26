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
    private static readonly StartAppsOptions Options = new(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));

    private readonly FakeProcessRunner runner = new();
    private readonly FakeClock clock = new();
    private readonly TestLogger logger = new();

    [Fact]
    public void ANameAndAnIdAreReadAsBase64OfUtf8()
    {
        IReadOnlyList<StartApp> apps = StartAppsParser.Parse(Listing(("Telegram Десктоп", "TelegramMessengerLLP.TelegramDesktop")));

        Assert.Equal(new StartApp("Telegram Десктоп", "TelegramMessengerLLP.TelegramDesktop"), Assert.Single(apps));
    }

    /// <summary>PowerShell prints one result as an object, not an array of one.</summary>
    [Fact]
    public void OneAppComesAsAnObject()
    {
        string entry = $$"""{"n":"{{Encode("Slack")}}","i":"{{Encode("com.squirrel.slack.slack")}}"}""";

        Assert.Equal("Slack", Assert.Single(StartAppsParser.Parse(entry)).Name);
    }

    [Fact]
    public void AppsAreSortedByNameAndEachNameIsOnce()
    {
        IReadOnlyList<StartApp> apps = StartAppsParser.Parse(Listing(("Slack", "a"), ("Code", "b"), ("slack", "c")));

        Assert.Equal(["Code", "Slack"], apps.Select(app => app.Name));
        Assert.Equal("a", apps[1].AppId);
    }

    [Fact]
    public void AnEntryThatDoesNotReadIsSkipped()
    {
        string json = $$"""[{"n":"not base64!","i":"{{Encode("x")}}"},{"n":"{{Encode("Code")}}"},{"n":"{{Encode("Slack")}}","i":"{{Encode("s")}}"}]""";

        Assert.Equal(["Slack"], StartAppsParser.Parse(json).Select(app => app.Name));
    }

    [Fact]
    public void NothingPrintedIsNoApps()
    {
        Assert.Empty(StartAppsParser.Parse("  \r\n"));
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
    public async Task ThePickerListsNamesWithoutIcons()
    {
        this.runner.Enqueue(Success(Listing(("Slack", "a"), ("Code", "b"))));

        IReadOnlyList<InstalledApp> apps = await new WindowsInstalledApps(this.NewStartApps()).GetInstalledAppsAsync(CancellationToken.None);

        Assert.Equal(["Code", "Slack"], apps.Select(app => app.Name));
        Assert.All(apps, app => Assert.Null(app.IconPath));
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

    private static ProcessResult Success(string output) => new(0, output, string.Empty, TimedOut: false);

    private static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    private static string Listing(params (string Name, string Id)[] apps) =>
        "[" + string.Join(",", apps.Select(app => $$"""{"n":"{{Encode(app.Name)}}","i":"{{Encode(app.Id)}}"}""")) + "]";

    private StartApps NewStartApps() => new(this.runner, this.clock, this.logger, Options);

    private WindowsAppLauncher Launcher() => new(this.runner, this.NewStartApps());
}
