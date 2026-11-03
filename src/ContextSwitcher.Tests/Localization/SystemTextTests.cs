using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using ContextSwitcher.App.Resources;
using ContextSwitcher.Core.Platform;
using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.Localization;

/// <summary>The words for the computer: the Mac's as they always were, Windows' with none of the Mac's in them, in both languages.</summary>
public sealed class SystemTextTests
{
    private static readonly PlatformCapabilities Mac = MacPlatform.Capabilities;
    private static readonly PlatformCapabilities Windows = MacPlatform.Capabilities with { Wording = SystemWording.Windows };

    /// <summary>What names a Mac thing: the Mac itself, its menu bar and Finder, ⌘, System Settings and its panes, and the Ukrainian for the same.</summary>
    private static readonly Regex MacWords = new(
        @"\bMacs?\b|macOS|⌘|Finder|menu bar|Dock\b|System Settings|Login Items|смуз\w+ меню|Системн\w+ параметр|Об’єкт\w+ входу|на Mac\b|вашу macOS",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Every wording, as (name, what it says) for one <see cref="SystemText"/>.</summary>
    private static IEnumerable<(string Name, string Text)> Wordings(SystemText text) =>
    [
        (nameof(text.OneClickFlips), text.OneClickFlips),
        (nameof(text.SwitchAnytime), text.SwitchAnytime),
        (nameof(text.AddInProfileSetup), text.AddInProfileSetup),
        (nameof(text.MatchSystem), text.MatchSystem),
        (nameof(text.MatchSystemTheme), text.MatchSystemTheme),
        (nameof(text.OpenAtLoginDescription), text.OpenAtLoginDescription),
        (nameof(text.OpenAtLoginNeedsApproval), text.OpenAtLoginNeedsApproval),
        (nameof(text.OpenAtLoginFailed), text.OpenAtLoginFailed),
        (nameof(text.OpenLoginItems), text.OpenLoginItems),
        (nameof(text.TransferDescription), text.TransferDescription),
        (nameof(text.LanguageRestartHint), text.LanguageRestartHint),
        (nameof(text.DiagnosticsDescription), text.DiagnosticsDescription),
        (nameof(text.ShowLog), text.ShowLog),
        (nameof(text.EmailOpened), text.EmailOpened),
        (nameof(text.RecordsHowLongYouSpend), text.RecordsHowLongYouSpend),
        (nameof(text.QuickLinksShown), text.QuickLinksShown),
        (nameof(text.RemindersShown), text.RemindersShown)
    ];

    [Fact]
    public void TheMacAsksForTheMacsWordsAsTheyAlwaysWere()
    {
        SystemText text = new(Mac);

        Assert.Equal(Strings.Onboarding_OneClickFlipsYourMac, text.OneClickFlips);
        Assert.Equal(Strings.Onboarding_SwitchAnytimeFromTheMenu, text.SwitchAnytime);
        Assert.Equal(Strings.Onboarding_AddBrowserTabsFocusAnd, text.AddInProfileSetup);
        Assert.Equal(Strings.Settings_Theme_MatchMacOS, text.MatchSystem);
        Assert.Equal(Strings.Settings_MatchMacOSFollowsTheSystems, text.MatchSystemTheme);
        Assert.Equal(Strings.Settings_OpenAtLogin_Description, text.OpenAtLoginDescription);
        Assert.Equal(Strings.Settings_OpenAtLogin_NeedsApproval, text.OpenAtLoginNeedsApproval);
        Assert.Equal(Strings.Settings_OpenAtLogin_Failed, text.OpenAtLoginFailed);
        Assert.Equal(Strings.Settings_OpenAtLogin_OpenLoginItems, text.OpenLoginItems);
        Assert.Equal(Strings.Settings_Transfer_Description, text.TransferDescription);
        Assert.Equal(Strings.Settings_Language_RestartHint, text.LanguageRestartHint);
        Assert.Equal(Strings.Settings_Diagnostics_Description, text.DiagnosticsDescription);
        Assert.Equal(Strings.Settings_ShowLog, text.ShowLog);
        Assert.Equal(Strings.Contact_EmailOpened, text.EmailOpened);
        Assert.Equal(Strings.Settings_RecordsHowLongYouSpend, text.RecordsHowLongYouSpend);
        Assert.Equal(Strings.ProfileSetup_ShownInTheMenuBar, text.QuickLinksShown);
        Assert.Equal(Strings.ProfileSetup_RemindersShownInTheMenu, text.RemindersShown);
    }

    [Fact]
    public void WindowsAsksForItsOwnWords()
    {
        SystemText text = new(Windows);

        Assert.Equal(Strings.Onboarding_OneClickFlipsYourMac_Windows, text.OneClickFlips);
        Assert.Equal(Strings.Onboarding_SwitchAnytimeFromTheMenu_Windows, text.SwitchAnytime);
        Assert.Equal(Strings.Onboarding_AddBrowserTabsFocusAnd_Windows, text.AddInProfileSetup);
        Assert.Equal(Strings.Settings_Theme_MatchMacOS_Windows, text.MatchSystem);
        Assert.Equal(Strings.Settings_ShowLog_Windows, text.ShowLog);
        Assert.Equal(Strings.Contact_EmailOpened_Windows, text.EmailOpened);
        Assert.Equal(Strings.Settings_OpenAtLogin_OpenLoginItems_Windows, text.OpenLoginItems);
    }

    [Fact]
    public void ThePlatformsNeverShareAWordingByAccident()
    {
        foreach (((string name, string mac), (_, string windows)) in Wordings(new SystemText(Mac)).Zip(Wordings(new SystemText(Windows))))
        {
            Assert.True(mac != windows, $"{name} says the same on both");
        }
    }

    [Theory]
    [InlineData("en")]
    [InlineData("uk")]
    public void NoWordOfWindowsNamesAMacThing(string language)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        try
        {
            SystemText text = new(Windows);
            foreach ((string name, string said) in Wordings(text).Append((nameof(text.PinTheIcon), text.PinTheIcon)))
            {
                Assert.False(MacWords.IsMatch(said), $"{name} ({language}) names a Mac thing: {said}");
                Assert.False(string.IsNullOrWhiteSpace(said), $"{name} ({language}) says nothing");
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>The check above is only worth having if it catches what it is for: it must find the Mac's own words in the Mac's own text.</summary>
    [Theory]
    [InlineData("en")]
    [InlineData("uk")]
    public void TheCheckForMacWordsFindsThemInTheMacsText(string language)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        try
        {
            SystemText text = new(Mac);
            string[] saidWithAMacWord =
            [
                text.OneClickFlips, text.SwitchAnytime, text.MatchSystem, text.OpenAtLoginDescription,
                text.OpenAtLoginNeedsApproval, text.TransferDescription, text.LanguageRestartHint, text.ShowLog, text.EmailOpened
            ];

            foreach (string said in saidWithAMacWord)
            {
                Assert.True(MacWords.IsMatch(said), $"({language}) the check misses a Mac word in: {said}");
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void WindowsSaysWhatItIsAndWhereThingsAre()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        try
        {
            SystemText text = new(Windows);

            Assert.Contains("PC", text.OneClickFlips);
            Assert.Contains("tray", text.SwitchAnytime);
            Assert.Equal("Match Windows", text.MatchSystem);
            Assert.Contains("File Explorer", text.ShowLog);
            Assert.Contains("Ctrl+V", text.EmailOpened);
            Assert.Contains("Startup", text.OpenAtLoginNeedsApproval);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void OnlyWindowsSaysHowToKeepTheIconInSight()
    {
        Assert.False(new SystemText(Mac).HasPinTheIcon);
        Assert.Empty(new SystemText(Mac).PinTheIcon);
        Assert.True(new SystemText(Windows).HasPinTheIcon);
        Assert.NotEmpty(new SystemText(Windows).PinTheIcon);
    }

    [Fact]
    public void ThePlatformsSayWhoseWordsTheyUse()
    {
        Assert.Equal(SystemWording.Mac, MacPlatform.Capabilities.Wording);
        Assert.Equal(SystemWording.Mac, new PlatformCapabilities
        {
            Focus = false, AppleMusic = false, Spotify = false, Docker = false, DockIcon = false, AutomationPermission = false,
            BrowserPages = false, Browsers = [], FocusFilterBrowsers = []
        }.Wording);
    }

    // The files behind it.

    private static HashSet<string> KeysOf(CultureInfo culture, bool parents)
    {
        ResourceSet? set = Strings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: parents);
        Assert.NotNull(set);
        return [.. set.Cast<DictionaryEntry>().Select(entry => (string)entry.Key)];
    }

    [Fact]
    public void EveryWindowsWordHasItsMacCounterpartExceptWhatOnlyWindowsNeeds()
    {
        HashSet<string> english = KeysOf(CultureInfo.InvariantCulture, parents: false);
        string[] windowsOnly = ["Onboarding_PinTheIcon_Windows"];

        foreach (string key in english.Where(key => key.EndsWith("_Windows", StringComparison.Ordinal)))
        {
            string mac = key[..^"_Windows".Length];
            Assert.True(english.Contains(mac) || windowsOnly.Contains(key), $"{key} has no {mac} to stand in for");
        }
    }

    [Fact]
    public void EveryWindowsWordIsTranslatedIntoUkrainian()
    {
        HashSet<string> english = KeysOf(CultureInfo.InvariantCulture, parents: false);
        HashSet<string> ukrainian = KeysOf(CultureInfo.GetCultureInfo("uk"), parents: false);

        string[] missing = english.Where(key => key.EndsWith("_Windows", StringComparison.Ordinal) && !ukrainian.Contains(key)).ToArray();

        Assert.Empty(missing);
    }

    /// <summary>A pattern with {0} in it is filled in with a value; the Windows words must be fillable the same way.</summary>
    [Fact]
    public void AWindowsWordTakesTheSameFillingsAsItsMacCounterpart()
    {
        foreach (CultureInfo culture in new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("uk") })
        {
            foreach (string key in KeysOf(culture, parents: false).Where(key => key.EndsWith("_Windows", StringComparison.Ordinal)))
            {
                string mac = key[..^"_Windows".Length];
                string? windowsText = Strings.ResourceManager.GetString(key, culture);
                string? macText = Strings.ResourceManager.GetString(mac, culture);
                if (macText is null || windowsText is null)
                {
                    continue;
                }

                Assert.True(
                    Placeholders(macText).SequenceEqual(Placeholders(windowsText)),
                    $"{key} ({culture.Name}) takes different fillings from {mac}");
            }
        }
    }

    private static IEnumerable<string> Placeholders(string pattern) =>
        Regex.Matches(pattern, @"\{\d+\}").Select(match => match.Value).Order();
}
