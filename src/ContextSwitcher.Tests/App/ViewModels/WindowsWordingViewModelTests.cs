using System.Globalization;
using ContextSwitcher.App.Resources;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Platform;
using ContextSwitcher.Infrastructure.MacOS;
using ContextSwitcher.Tests.TestDoubles;
using ContextSwitcher.Tests.Ui;

namespace ContextSwitcher.Tests.App.ViewModels;

/// <summary>The pages show the words of the system they run on: Settings and the welcome, on Windows and on the Mac.</summary>
public sealed class WindowsWordingViewModelTests
{
    private static readonly PlatformCapabilities Mac = MacPlatform.Capabilities;
    private static readonly PlatformCapabilities Windows = MacPlatform.Capabilities with { Wording = SystemWording.Windows };

    private static SettingsViewModel Settings(PlatformCapabilities capabilities, FakeLoginItem? loginItem = null)
    {
        UiScenario scenario = UiScenario.WithTwoProfiles();
        return new SettingsViewModel(
            scenario.AppState, scenario.ConfigurationStore, scenario.Permissions, scenario.Shell, scenario.Updates, scenario.Diagnostics,
            new FakePreferredLanguages(), capabilities, loginItem ?? FakeLoginItem.Unavailable(), scenario.InstalledApps);
    }

    private static T InEnglish<T>(Func<T> read)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        try
        {
            return read();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void OnWindowsTheChoiceThatFollowsTheSystemIsMatchWindows()
    {
        (string theme, string language) = InEnglish(() =>
        {
            SettingsViewModel settings = Settings(Windows);
            return (settings.AppearanceChoices[0].Label, settings.LanguageChoices[0].Label);
        });

        Assert.Equal("Match Windows", theme);
        Assert.Equal("Match Windows", language);
    }

    [Fact]
    public void OnTheMacTheChoiceThatFollowsTheSystemIsStillMatchMacOS()
    {
        (string theme, string language) = InEnglish(() =>
        {
            SettingsViewModel settings = Settings(Mac);
            return (settings.AppearanceChoices[0].Label, settings.LanguageChoices[0].Label);
        });

        Assert.Equal("Match macOS", theme);
        Assert.Equal("Match macOS", language);
    }

    [Fact]
    public void OnWindowsTurnedOffInStartupAppsIsSaidInWindowsWordsAndSettingsIsTheLink()
    {
        string? hint = InEnglish(() => Settings(Windows, new FakeLoginItem { Status = LoginItemStatus.NeedsApproval }).OpenAtLoginHint);

        Assert.NotNull(hint);
        Assert.Contains("Startup", hint);
        Assert.DoesNotContain("macOS", hint);
    }

    [Fact]
    public void OnTheMacWaitingForApprovalIsStillSaidInLoginItems()
    {
        string? hint = InEnglish(() => Settings(Mac, new FakeLoginItem { Status = LoginItemStatus.NeedsApproval }).OpenAtLoginHint);

        Assert.Contains("Login Items", hint);
    }

    [Fact]
    public void ARefusalIsAttributedToTheSystemThatMadeIt()
    {
        (string? onWindows, string? onTheMac) = InEnglish(() =>
        {
            FakeLoginItem refusing() => new() { Status = LoginItemStatus.Off, Refusal = "no" };
            SettingsViewModel windows = Settings(Windows, refusing());
            windows.OpenAtLogin = true;
            SettingsViewModel mac = Settings(Mac, refusing());
            mac.OpenAtLogin = true;
            return (windows.OpenAtLoginHint, mac.OpenAtLoginHint);
        });

        Assert.Equal("Windows didn't allow it: no", onWindows);
        Assert.Equal("macOS didn't allow it: no", onTheMac);
    }

    [Fact]
    public void SettingsGivesThePageTheWordsOfItsSystem()
    {
        Assert.Equal(SystemWording.Windows, WordsOf(Settings(Windows).Words));
        Assert.Equal(SystemWording.Mac, WordsOf(Settings(Mac).Words));
    }

    [Fact]
    public void TheWelcomeOnWindowsSaysHowToKeepTheIconInSightAndTheMacDoesNot()
    {
        UiScenario scenario = UiScenario.WithTwoProfiles();
        OnboardingViewModel onWindows = new(scenario.AppState, scenario.ConfigurationStore, scenario.InstalledApps, scenario.SwitchService, FakeLoginItem.Unavailable(), Windows);
        OnboardingViewModel onTheMac = scenario.Onboarding();

        Assert.True(onWindows.Words.HasPinTheIcon);
        Assert.False(onTheMac.Words.HasPinTheIcon);
        Assert.Contains("PC", InEnglish(() => onWindows.Words.OneClickFlips));
        Assert.Contains("Mac", InEnglish(() => onTheMac.Words.OneClickFlips));
    }

    [Fact]
    public void TheDiagnosticsOnWindowsSayCtrlVNotCommandV()
    {
        string said = InEnglish(() => new SystemText(Windows).EmailOpened);

        Assert.Contains("Ctrl+V", said);
        Assert.DoesNotContain("⌘", said);
    }

    private static SystemWording WordsOf(SystemText text) =>
        InEnglish(() => text.ShowLog.Contains("Explorer", StringComparison.Ordinal)) ? SystemWording.Windows : SystemWording.Mac;
}
