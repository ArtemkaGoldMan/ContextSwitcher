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
