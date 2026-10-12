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

    [Fact]
    public void ValidateRejectsClosingTabsInTheDefaultBrowser()
    {
        AppConfiguration configuration = CreateConfiguration(new BrowserManagementConfig
        {
            Mode = BrowserManagementMode.Urls,
            Browser = BrowserKind.Default,
            Urls = ["https://github.com/"],
            CloseUrls = ["https://github.com/"]
        });

        ConfigurationValidationResult result = this.validator.Validate(configuration);

        ConfigurationValidationError error = Assert.Single(result.Errors);
        Assert.Equal("contexts[0].browser_management.close_urls", error.Path);
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
