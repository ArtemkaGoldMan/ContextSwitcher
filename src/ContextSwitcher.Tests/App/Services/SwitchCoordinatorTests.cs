using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.Services;

/// <summary>
/// Every switch started in the app goes through the coordinator. With asking on, it asks about the
/// apps and tabs neither profile has, saves what was added to a profile before the switch reads it,
/// and hands the switch what to close besides; with asking off, or nothing to ask about, it doesn't ask.
/// </summary>
[Collection(AppHostTestCollection.Name)]
public sealed class SwitchCoordinatorTests
{
    private readonly StubContextSwitchService inner = new();
    private readonly FakeRunningApplications running = new();
    private readonly FakeSystemCatalog catalog = new();
    private readonly FakeAskOnSwitchPresenter presenter = new();
    private readonly InMemoryJsonStore jsonStore = new();
    private readonly ConfigPaths paths = new("/tmp/context-switcher-coordinator-tests");

    /// <summary>
    /// Whatever started the switch - the menu bar, the popover, the Profiles page, the setup wizard - the
    /// state it wrote is published at once, rather than each of them reading it back for itself.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EverySwitchPublishesTheStateItWrote(bool askOnSwitch)
    {
        SwitchCoordinator coordinator = this.Create(askOnSwitch);
        await this.jsonStore.WriteAsync(this.paths.StatePath, new CurrentContextState { CurrentContextId = "personal" });

        await coordinator.SwitchAsync(new ContextSwitchRequest("personal", ContextSwitchSource.MenuBar), CancellationToken.None);

        Assert.Equal("personal", AppHost.State.CurrentContextId);
    }

    [Fact]
    public async Task WithAskingOffItNeverAsks()
    {
        SwitchCoordinator coordinator = this.Create(askOnSwitch: false);
        this.running.Apps.Add(new RunningApp("Figma"));

        await coordinator.SwitchAsync(new ContextSwitchRequest("personal", ContextSwitchSource.MenuBar), CancellationToken.None);

        Assert.Empty(this.presenter.Asked);
        Assert.Null(this.inner.LastRequest!.Extras);
    }

    [Fact]
    public async Task WithNothingElseOpenItDoesntAsk()
    {
        SwitchCoordinator coordinator = this.Create(askOnSwitch: true);
        this.running.Apps.Add(new RunningApp("Slack"));

        await coordinator.SwitchAsync(new ContextSwitchRequest("personal", ContextSwitchSource.MenuBar), CancellationToken.None);

        Assert.Empty(this.presenter.Asked);
        Assert.Equal("personal", this.inner.LastRequest!.TargetContextId);
    }

    [Fact]
    public async Task WhatWasChosenToCloseGoesToTheSwitch()
    {
        SwitchCoordinator coordinator = this.Create(askOnSwitch: true);
        this.running.Apps.AddRange([new RunningApp("Figma"), new RunningApp("Notes")]);
        this.catalog.BrowserTabs.Add(new OpenBrowserTab(BrowserKind.Chrome, "Lo-fi", "https://www.youtube.com/watch?v=1"));
        this.presenter.Answer = viewModel =>
        {
            viewModel.Apps.Single(row => row.Title == "Figma").Choice = UnclaimedChoice.Close;
            viewModel.Tabs[0].Choice = UnclaimedChoice.Close;
            viewModel.ContinueCommand.Execute(null);
        };

        await coordinator.SwitchAsync(new ContextSwitchRequest("personal", ContextSwitchSource.Dashboard), CancellationToken.None);

        AskOnSwitchViewModel asked = Assert.Single(this.presenter.Asked);
        Assert.Equal("Switching from Work to Personal", asked.Title);
        SwitchExtras extras = this.inner.LastRequest!.Extras!;
        Assert.Equal(["Figma"], extras.CloseApps);
        Assert.Equal("https://www.youtube.com/watch?v=1", Assert.Single(extras.CloseTabs).Url);
    }

    /// <summary>Added to the profile being left, an app is saved there before the switch reads it - so it closes now, and opens next time.</summary>
    [Fact]
    public async Task WhatWasAddedIsSavedBeforeTheSwitch()
    {
        SwitchCoordinator coordinator = this.Create(askOnSwitch: true);
        this.running.Apps.Add(new RunningApp("Figma"));
        this.presenter.Answer = viewModel =>
        {
            viewModel.Apps[0].Choice = UnclaimedChoice.AddToLeaving;
            viewModel.DontAskAgain = true;
            viewModel.ContinueCommand.Execute(null);
        };

        await coordinator.SwitchAsync(new ContextSwitchRequest("personal", ContextSwitchSource.Dashboard), CancellationToken.None);

        AppConfiguration saved = this.jsonStore.Get<AppConfiguration>(this.paths.SettingsPath)!;
        ContextDefinition work = saved.Contexts.Single(context => context.Id == "work");
        Assert.Contains("Figma", work.LaunchApps);
        Assert.Contains("Figma", work.CloseApps);
        Assert.False(saved.AskOnSwitch);
        Assert.Empty(this.inner.LastRequest!.Extras!.CloseApps);
    }

    [Fact]
    public async Task SkippingSwitchesAsIfItHadntAsked()
    {
        SwitchCoordinator coordinator = this.Create(askOnSwitch: true);
        this.running.Apps.Add(new RunningApp("Figma"));

        await coordinator.SwitchAsync(ContextSwitchRequest.TurnOff(ContextSwitchSource.MenuBar), CancellationToken.None);

        Assert.Equal("Turning Work off", Assert.Single(this.presenter.Asked).Title);
        Assert.True(this.inner.LastRequest!.IsTurnOff);
        Assert.Null(this.inner.LastRequest.Extras);
        Assert.Null(this.jsonStore.Get<AppConfiguration>(this.paths.SettingsPath));
    }

    /// <summary>Clicking the profile that's already on, without forcing, changes nothing - so there's nothing to ask.</summary>
    [Fact]
    public async Task StayingInTheSameProfileDoesntAsk()
    {
        SwitchCoordinator coordinator = this.Create(askOnSwitch: true);
        this.running.Apps.Add(new RunningApp("Figma"));

        await coordinator.SwitchAsync(new ContextSwitchRequest("work", ContextSwitchSource.MenuBar), CancellationToken.None);

        Assert.Empty(this.presenter.Asked);
    }

    /// <summary>One question at a time: a second switch while the window is open is turned away.</summary>
    [Fact]
    public async Task ASecondSwitchWhileAskingIsTurnedAway()
    {
        SwitchCoordinator coordinator = this.Create(askOnSwitch: true);
        this.running.Apps.Add(new RunningApp("Figma"));
        AskOnSwitchViewModel? open = null;
        this.presenter.Answer = viewModel => open = viewModel;

        Task<ContextSwitchResult> first = coordinator.SwitchAsync(new ContextSwitchRequest("personal", ContextSwitchSource.MenuBar), CancellationToken.None);
        ContextSwitchResult second = await coordinator.SwitchAsync(new ContextSwitchRequest("personal", ContextSwitchSource.Dashboard), CancellationToken.None);

        Assert.Equal(ContextSwitchStatus.Cancelled, second.Status);
        open!.SkipCommand.Execute(null);
        await first;
        Assert.Equal("personal", this.inner.LastRequest!.TargetContextId);
    }

    private SwitchCoordinator Create(bool askOnSwitch)
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            AskOnSwitch = askOnSwitch,
            Contexts =
            [
                new ContextDefinition { Id = "work", DisplayName = "Work", AccentColor = "#2F6FED", CloseApps = ["Slack"] },
                new ContextDefinition { Id = "personal", DisplayName = "Personal", AccentColor = "#20A67A" }
            ]
        };
        ConfigurationValidator validator = new();
        AppHost.UpdateConfiguration(configuration, validator.Validate(configuration));
        AppHost.UpdateState(new CurrentContextState { CurrentContextId = "work" });
        ConfigurationStore store = new(this.jsonStore, this.paths, validator, new TestLogger());
        return new SwitchCoordinator(this.inner, store, this.running, this.catalog, this.presenter, this.jsonStore, this.paths.StatePath);
    }
}
