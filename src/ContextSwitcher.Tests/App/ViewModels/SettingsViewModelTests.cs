using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.App;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Logging;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.ViewModels;

public sealed class SettingsViewModelTests
{
    private readonly AppState appState = new();

    /// <summary>
    /// Each change is written as it is made - no Save - and the switch timeout, which the page no
    /// longer offers because nothing reads it, is carried over untouched rather than reset.
    /// </summary>
    [Fact]
    public async Task ChangesAreSavedAsTheyAreMadeAndTheTimeoutIsKept()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }],
            DefaultSwitchTimeoutSeconds = 37
        };
        this.appState.UpdateConfiguration(configuration, new ConfigurationValidationResult([]));

        InMemoryJsonStore jsonStore = new();
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");
        ConfigurationStore configurationStore = new(jsonStore, configPaths, new ConfigurationValidator(), this.appState, new TestLogger());
        SettingsViewModel viewModel = new(this.appState, configurationStore, new FakePermissionsChecker(), new FakeShell(), Updates(this.appState), Diagnostics());

        // On by default now; turning it off is the change.
        viewModel.ShowDockIcon = false;
        await viewModel.Saving;
        Assert.False(jsonStore.Get<AppConfiguration>(configPaths.SettingsPath)!.ShowDockIcon);

        viewModel.AnalyticsEnabled = false;
        viewModel.CheckForUpdates = false;
        viewModel.SelectedAppearance = viewModel.AppearanceChoices.Single(choice => choice.Value == AppearanceMode.Dark);
        viewModel.SelectedRetention = viewModel.RetentionChoices.Single(choice => choice.Value == 30);
        await viewModel.Saving;

        AppConfiguration persisted = jsonStore.Get<AppConfiguration>(configPaths.SettingsPath)!;
        Assert.False(persisted.ShowDockIcon);
        Assert.False(persisted.Analytics.Enabled);
        Assert.False(persisted.CheckForUpdates);
        Assert.Equal(AppearanceMode.Dark, persisted.Appearance);
        Assert.Equal(30, persisted.Analytics.RetentionDays);
        Assert.Equal(37, persisted.DefaultSwitchTimeoutSeconds);
        Assert.False(viewModel.HasErrorMessage);
    }

    /// <summary>
    /// A history length written by hand or by an older build shows as chosen, in order among the
    /// presets, rather than being swapped for the nearest one.
    /// </summary>
    [Fact]
    public void AHistoryLengthThatIsNoPresetIsOfferedAsChosen()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }],
            Analytics = new AnalyticsConfiguration { Enabled = true, RetentionDays = 1095 }
        };
        this.appState.UpdateConfiguration(configuration, new ConfigurationValidationResult([]));

        ConfigurationStore configurationStore = new(new InMemoryJsonStore(), new ConfigPaths("/tmp/context-switcher-tests"), new ConfigurationValidator(), this.appState, new TestLogger());
        SettingsViewModel viewModel = new(this.appState, configurationStore, new FakePermissionsChecker(), new FakeShell(), Updates(this.appState), Diagnostics());

        Assert.Equal(
            ["1 week", "30 days", "90 days", "6 months", "1 year", "2 years", "3 years"],
            viewModel.RetentionChoices.Select(choice => choice.Label));
        Assert.Equal(1095, viewModel.SelectedRetention.Value);
    }

    /// <summary>
    /// Both "Support the developer" buttons were wired to an empty lambda - visible, clickable and
    /// doing nothing.
    /// </summary>
    [Fact]
    public void SupportDeveloperCommandOpensTheSupportLink()
    {
        this.appState.UpdateConfiguration(
            new AppConfiguration
            {
                ActiveContextId = "work",
                Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }]
            },
            new ConfigurationValidationResult([]));

        FakeShell shell = new();
        ConfigurationStore store = new(new InMemoryJsonStore(), new ConfigPaths("/tmp/context-switcher-tests"), new ConfigurationValidator(), this.appState, new TestLogger());
        SettingsViewModel viewModel = new(this.appState, store, new FakePermissionsChecker(), shell, Updates(this.appState), Diagnostics());

        viewModel.SupportDeveloperCommand.Execute(null);

        Assert.Equal([AppLinks.Support], shell.Links);
    }

    /// <summary>The switch is macOS's login item: flipping it changes it there at once, and reads it back.</summary>
    [Fact]
    public void OpenAtLoginIsTurnedOnAndOffInMacOS()
    {
        FakeLoginItem loginItem = new();
        SettingsViewModel viewModel = WithLoginItem(loginItem);
        Assert.True(viewModel.CanOpenAtLogin);
        Assert.False(viewModel.OpenAtLogin);

        viewModel.OpenAtLogin = true;
        Assert.True(viewModel.OpenAtLogin);
        Assert.False(viewModel.HasOpenAtLoginHint);

        viewModel.OpenAtLogin = false;
        Assert.Equal([true, false], loginItem.Changes);
        Assert.Equal(LoginItemStatus.Off, loginItem.Status);
    }

    /// <summary>When macOS waits for approval, the page says so and offers the place to give it.</summary>
    [Fact]
    public void WaitingForApprovalIsExplainedWithTheWayThere()
    {
        FakeLoginItem loginItem = new() { NeedsApproval = true };
        SettingsViewModel viewModel = WithLoginItem(loginItem);

        viewModel.OpenAtLogin = true;

        Assert.True(viewModel.OpenAtLogin);
        Assert.True(viewModel.NeedsLoginItemApproval);
        Assert.Equal("macOS is waiting for you to allow it in System Settings → General → Login Items.", viewModel.OpenAtLoginHint);
        viewModel.OpenLoginItemsCommand.Execute(null);
        Assert.Equal(1, loginItem.SettingsOpened);
    }

    [Fact]
    public void ARefusalLeavesTheSwitchOffWithMacOSsReason()
    {
        SettingsViewModel viewModel = WithLoginItem(new FakeLoginItem { Refusal = "Operation not permitted" });

        viewModel.OpenAtLogin = true;

        Assert.False(viewModel.OpenAtLogin);
        Assert.Equal("macOS didn't allow it: Operation not permitted", viewModel.OpenAtLoginHint);
    }

    /// <summary>Run from source there is no login item to add; the switch is off and says why.</summary>
    [Fact]
    public void FromSourceOpeningAtLoginIsUnavailable()
    {
        SettingsViewModel viewModel = WithLoginItem(new FakeLoginItem { Status = LoginItemStatus.Unavailable });

        Assert.False(viewModel.CanOpenAtLogin);
        Assert.Equal("Only the installed app can open at login.", viewModel.OpenAtLoginHint);
    }

    private SettingsViewModel WithLoginItem(FakeLoginItem loginItem)
    {
        AppConfiguration configuration = new() { ActiveContextId = "work", Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }] };
        this.appState.UpdateConfiguration(configuration, new ConfigurationValidationResult([]));
        ConfigurationStore store = new(new InMemoryJsonStore(), new ConfigPaths("/tmp/context-switcher-tests"), new ConfigurationValidator(), this.appState, new TestLogger());
        return new SettingsViewModel(this.appState, store, new FakePermissionsChecker(), new FakeShell(), Updates(this.appState), Diagnostics(), loginItem: loginItem);
    }

    /// <summary>After an import the page shows the imported settings, without saving them over again.</summary>
    [Fact]
    public async Task AnImportedFilesSettingsShowOnThePage()
    {
        AppConfiguration configuration = new() { ActiveContextId = "work", Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }] };
        this.appState.UpdateConfiguration(configuration, new ConfigurationValidationResult([]));
        ConfigurationStore store = new(new InMemoryJsonStore(), new ConfigPaths("/tmp/context-switcher-tests"), new ConfigurationValidator(), this.appState, new TestLogger());
        SettingsViewModel viewModel = new(this.appState, store, new FakePermissionsChecker(), new FakeShell(), Updates(this.appState), Diagnostics());
        FakeSettingsFilePicker picker = new()
        {
            FileToOpen = """{ "activeContextId": "work", "showDockIcon": false, "checkForUpdates": false, "contexts": [ { "id": "work", "displayName": "Work" } ], "analytics": { "enabled": true, "retentionDays": 45 } }"""
        };
        viewModel.Transfer.FilePicker = picker;

        await viewModel.Transfer.ImportCommand.ExecuteAsync(null);
        await viewModel.Transfer.ConfirmImportCommand.ExecuteAsync(null);

        Assert.False(viewModel.ShowDockIcon);
        Assert.False(viewModel.CheckForUpdates);
        Assert.Equal(45, viewModel.SelectedRetention.Value);
        Assert.Contains(viewModel.RetentionChoices, choice => choice.Value == 45);
    }

    /// <summary>
    /// Closing the main window disposes its Settings page, which then lets go of the app's events - it
    /// used to stay alive, with its ignored-apps list, answering every save for as long as the app ran.
    /// </summary>
    [Fact]
    public void AClosedSettingsPageNoLongerAnswersSavedChanges()
    {
        AppConfiguration configuration = new() { ActiveContextId = "work", Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }], AskOnSwitch = false };
        this.appState.UpdateConfiguration(configuration, new ConfigurationValidationResult([]));
        ConfigurationStore store = new(new InMemoryJsonStore(), new ConfigPaths("/tmp/context-switcher-tests"), new ConfigurationValidator(), this.appState, new TestLogger());
        SettingsViewModel open = new(this.appState, store, new FakePermissionsChecker(), new FakeShell(), Updates(this.appState), Diagnostics());
        SettingsViewModel closed = new(this.appState, store, new FakePermissionsChecker(), new FakeShell(), Updates(this.appState), Diagnostics());
        closed.Dispose();

        this.appState.UpdateConfiguration(configuration with { AskOnSwitch = true, IgnoredApps = ["Telegram"] }, new ConfigurationValidationResult([]));

        Assert.True(open.AskOnSwitch);
        Assert.Equal(["Telegram"], open.IgnoredApps.Apps.Select(app => app.Name));
        Assert.False(closed.AskOnSwitch);
        Assert.Empty(closed.IgnoredApps.Apps);
        open.Dispose();
    }

    internal static UpdatesViewModel Updates(IAppState appState) => new(
        new UpdateChecker(new FakeReleaseSource(), new FakeAppUpdater(), new FakeSystemNotifications(), appState, new FakeAppInfo(new Version(0, 1, 0)), new FakeClock(), new TestLogger()),
        new FakeShell());

    internal static DiagnosticsSources Diagnostics() => new(
        new ConfigPaths("/tmp/context-switcher-tests"), () => new RunInfo("ContextSwitcher", "0.1.0", "26.6", "arm64", "release", 1), () => [], () => [], new FakeClock());
}
