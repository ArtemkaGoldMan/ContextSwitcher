using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;

namespace ContextSwitcher.Tests.Configuration;

public sealed class ConfigurationValidatorTests
{
    private readonly ConfigurationValidator validator = new();

    [Fact]
    public void ValidateAcceptsUrlsModeWithDefaultBrowser()
    {
        AppConfiguration configuration = CreateConfiguration(new BrowserManagementConfig
        {
            Mode = BrowserManagementMode.Urls,
            Browser = BrowserKind.Default,
            Urls = ["https://github.com/", "https://mail.google.com/"]
        });

        ConfigurationValidationResult result = this.validator.Validate(configuration);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateAcceptsGroupsModeWithTabGroupsAndFallbackUrls()
    {
        AppConfiguration configuration = CreateConfiguration(new BrowserManagementConfig
        {
            Mode = BrowserManagementMode.Groups,
            Browser = BrowserKind.Safari,
            TabGroups = ["Work-Core", "Finance"],
            Urls = ["https://github.com/"]
        });

        ConfigurationValidationResult result = this.validator.Validate(configuration);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateAcceptsProfilesModeWithChromeProfile()
    {
        AppConfiguration configuration = CreateConfiguration(new BrowserManagementConfig
        {
            Mode = BrowserManagementMode.Profiles,
            Profiles =
            [
                new BrowserProfileConfig
                {
                    Browser = BrowserKind.Chrome,
                    ProfileDirectory = "Profile 1",
                    Urls = ["https://github.com/notifications"]
                }
            ]
        });

        ConfigurationValidationResult result = this.validator.Validate(configuration);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsUrlsModeWithoutUrls()
    {
        AppConfiguration configuration = CreateConfiguration(new BrowserManagementConfig
        {
            Mode = BrowserManagementMode.Urls
        });

        ConfigurationValidationResult result = this.validator.Validate(configuration);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == "contexts[0].browser_management.urls");
    }

    [Fact]
    public void ValidateRejectsGroupsModeWithoutTabGroups()
    {
        AppConfiguration configuration = CreateConfiguration(new BrowserManagementConfig
        {
            Mode = BrowserManagementMode.Groups,
            Browser = BrowserKind.Chrome
        });

        ConfigurationValidationResult result = this.validator.Validate(configuration);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == "contexts[0].browser_management.tab_groups");
    }

    [Fact]
    public void ValidateRejectsSafariProfile()
    {
        AppConfiguration configuration = CreateConfiguration(new BrowserManagementConfig
        {
            Mode = BrowserManagementMode.Profiles,
            Profiles =
            [
                new BrowserProfileConfig
                {
                    Browser = BrowserKind.Safari,
                    ProfileDirectory = "Default",
                    Urls = ["https://example.com/"]
                }
            ]
        });

        ConfigurationValidationResult result = this.validator.Validate(configuration);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == "contexts[0].browser_management.profiles[0].browser");
    }

    [Fact]
    public void ValidateRejectsNonHttpBrowserUrls()
    {
        AppConfiguration configuration = CreateConfiguration(new BrowserManagementConfig
        {
            Mode = BrowserManagementMode.Urls,
            Urls = ["file:///Users/artem/secrets.txt"]
        });

        ConfigurationValidationResult result = this.validator.Validate(configuration);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == "contexts[0].browser_management.urls[0]");
    }


    /// <summary>A profile can list pages only to close them when it is left.</summary>
    [Fact]
    public void ValidateAcceptsUrlsModeWithPagesOnlyToClose()
    {
        AppConfiguration configuration = CreateConfiguration(new BrowserManagementConfig
        {
            Mode = BrowserManagementMode.Urls,
            Browser = BrowserKind.Chrome,
            CloseUrls = ["https://youtube.com/"]
        });

        Assert.True(this.validator.Validate(configuration).IsValid);
    }

    /// <summary>
    /// "Default browser" can close tabs when it is Chrome, Brave or Safari, which only the Mac running
    /// the switch knows - so the configuration is fine, and a switch says if it can't.
    /// </summary>
    [Fact]
    public void ValidateAcceptsClosingTabsInTheDefaultBrowser()
    {
        AppConfiguration configuration = CreateConfiguration(new BrowserManagementConfig
        {
            Mode = BrowserManagementMode.Urls,
            Browser = BrowserKind.Default,
            Urls = ["https://github.com/"],
            CloseUrls = ["https://github.com/"]
        });

        Assert.True(this.validator.Validate(configuration).IsValid);
    }

    [Fact]
    public void ValidateRejectsAPageToCloseThatIsntAWebAddress()
    {
        AppConfiguration configuration = CreateConfiguration(new BrowserManagementConfig
        {
            Mode = BrowserManagementMode.Urls,
            Browser = BrowserKind.Safari,
            CloseUrls = ["file:///etc/hosts"]
        });

        ConfigurationValidationResult result = this.validator.Validate(configuration);

        Assert.Contains(result.Errors, error => error.Path == "contexts[0].browser_management.close_urls[0]");
    }
    [Fact]
    public void ValidateRejectsAnAppBothClosedAndHidden()
    {
        AppConfiguration configuration = CreateConfiguration(new BrowserManagementConfig()) with { };
        configuration = configuration with
        {
            Contexts = [configuration.Contexts[0] with { CloseApps = ["Slack"], HideApps = ["slack", "Notion"] }]
        };

        ConfigurationValidationResult result = this.validator.Validate(configuration);

        ConfigurationValidationError error = Assert.Single(result.Errors);
        Assert.Equal("contexts[0].hideApps", error.Path);
    }

    /// <summary>
    /// What a hand-edited file most often gets wrong, each named by where it is so the dashboard's
    /// warning points at the line to fix.
    /// </summary>
    [Theory]
    [InlineData("""{ "id": "Work Profile", "displayName": "Work", "accentColor": "#2F6FED" }""", "contexts[0].id")]
    [InlineData("""{ "id": "work", "displayName": "Work", "accentColor": "blue" }""", "contexts[0].accentColor")]
    [InlineData("""{ "id": "work", "displayName": "Work", "accentColor": "#2F6FED", "browser_management": { "mode": "groups", "browser": "Chrome", "tab_groups": [" "] } }""", "contexts[0].browser_management.tab_groups[0]")]
    [InlineData("""{ "id": "work", "displayName": "Work", "accentColor": "#2F6FED", "browser_management": { "mode": "profiles" } }""", "contexts[0].browser_management.profiles")]
    [InlineData("""{ "id": "work", "displayName": "Work", "accentColor": "#2F6FED", "browser_management": { "mode": "profiles", "profiles": [ { "browser": "Chrome", "profile_directory": "" } ] } }""", "contexts[0].browser_management.profiles[0].profile_directory")]
    [InlineData("""{ "id": "work", "displayName": "Work", "accentColor": "#2F6FED", "browser_management": { "mode": "profiles", "profiles": [ { "browser": "Safari", "profile_directory": "Default" } ] } }""", "contexts[0].browser_management.profiles[0].browser")]
    [InlineData("""{ "id": "work", "displayName": "Work", "accentColor": "#2F6FED", "quickLinks": [ { "title": "Docs", "url": "ftp://example.com/" } ] }""", "contexts[0].quickLinks[0].url")]
    public void AMistakeIsReportedWhereItIs(string context, string path)
    {
        AppConfiguration? configuration = System.Text.Json.JsonSerializer.Deserialize<AppConfiguration>(
            "{ \"activeContextId\": \"work\", \"contexts\": [ " + context + " ] }", ContextSwitcher.Core.Serialization.ContextSwitcherJson.Options);

        ConfigurationValidationResult result = this.validator.Validate(configuration!);

        Assert.Contains(result.Errors, error => error.Path == path);
    }

    [Fact]
    public void TwoProfilesCantShareAnId()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts =
            [
                new ContextDefinition { Id = "work", DisplayName = "Work", AccentColor = "#2F6FED" },
                new ContextDefinition { Id = "work", DisplayName = "Work again", AccentColor = "#2F6FED" }
            ]
        };

        ConfigurationValidationResult result = this.validator.Validate(configuration);

        Assert.Contains(result.Errors, error => error.Path == "contexts[1].id");
    }

    private static AppConfiguration CreateConfiguration(BrowserManagementConfig browserManagement)
    {
        return new AppConfiguration
        {
            ActiveContextId = "work",
            Contexts =
            [
                new ContextDefinition
                {
                    Id = "work",
                    DisplayName = "Work",
                    AccentColor = "#2F6FED",
                    BrowserManagement = browserManagement
                }
            ]
        };
    }
}
