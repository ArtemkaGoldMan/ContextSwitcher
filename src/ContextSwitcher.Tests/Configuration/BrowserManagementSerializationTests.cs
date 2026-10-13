using System.Text.Json;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Serialization;

namespace ContextSwitcher.Tests.Configuration;

public sealed class BrowserManagementSerializationTests
{
    [Fact]
    public void DeserializesSnakeCaseBrowserManagementFields()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "activeContextId": "work",
          "contexts": [
            {
              "id": "work",
              "displayName": "Work",
              "browser_management": {
                "mode": "profiles",
                "browser": "Chrome",
                "urls": ["https://github.com/"],
                "tab_groups": ["Work-Core"],
                "profiles": [
                  {
                    "browser": "Brave",
                    "profile_directory": "Profile 1",
                    "urls": ["https://example.com/"]
                  }
                ],
                "avoid_duplicate_tabs": false
              }
            }
          ]
        }
        """;

        AppConfiguration? configuration = JsonSerializer.Deserialize<AppConfiguration>(
            json,
            ContextSwitcherJson.Options);

        Assert.NotNull(configuration);
        BrowserManagementConfig browserManagement = configuration.Contexts[0].BrowserManagement;
        Assert.Equal(BrowserManagementMode.Profiles, browserManagement.Mode);
        Assert.Equal(BrowserKind.Chrome, browserManagement.Browser);
        Assert.False(browserManagement.AvoidDuplicateTabs);
        Assert.Equal(BrowserKind.Brave, browserManagement.Profiles[0].Browser);
        Assert.Equal("Profile 1", browserManagement.Profiles[0].ProfileDirectory);
    }

    [Fact]
    public void SerializesSnakeCaseBrowserManagementFields()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts =
            [
                new ContextDefinition
                {
                    Id = "work",
                    DisplayName = "Work",
                    BrowserManagement = new BrowserManagementConfig
                    {
                        Mode = BrowserManagementMode.Profiles,
                        Browser = BrowserKind.Safari,
                        Profiles =
                        [
                            new BrowserProfileConfig
                            {
                                Browser = BrowserKind.Chrome,
                                ProfileDirectory = "Profile 1"
                            }
                        ]
                    }
                }
            ]
        };

        string json = JsonSerializer.Serialize(configuration, ContextSwitcherJson.Options);

        Assert.Contains("\"browser_management\"", json, StringComparison.Ordinal);
        Assert.Contains("\"profile_directory\"", json, StringComparison.Ordinal);
        Assert.Contains("\"mode\": \"profiles\"", json, StringComparison.Ordinal);
        Assert.Contains("\"browser\": \"Safari\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("tab_groups", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tab groups were dropped: no browser lets another app open one, so a "groups" profile only
    /// ever opened the pages it fell back to. Settings saved that way load as what they did, and
    /// the group names - no longer a setting - are skipped.
    /// </summary>
    [Theory]
    [InlineData("""["https://fallback.example/"]""", "[]", BrowserManagementMode.Urls)]
    [InlineData("[]", """["https://mail.example/"]""", BrowserManagementMode.Urls)]
    [InlineData("[]", "[]", BrowserManagementMode.None)]
    public void ATabGroupsProfileIsReadAsThePagesItOpened(string urls, string closeUrls, BrowserManagementMode expected)
    {
        string json = $$"""
        {
          "mode": "groups",
          "browser": "Safari",
          "urls": {{urls}},
          "close_urls": {{closeUrls}},
          "tab_groups": ["Generation", "Work Services"]
        }
        """;

        BrowserManagementConfig? browserManagement = JsonSerializer.Deserialize<BrowserManagementConfig>(json, ContextSwitcherJson.Options);

        Assert.NotNull(browserManagement);
        Assert.Equal(expected, browserManagement.Mode);
        Assert.Equal(BrowserKind.Safari, browserManagement.Browser);
        Assert.DoesNotContain("tab_groups", JsonSerializer.Serialize(browserManagement, ContextSwitcherJson.Options), StringComparison.Ordinal);
    }

    /// <summary>A settings file from before tab groups were dropped still passes its check, so the app starts with it.</summary>
    [Fact]
    public void SettingsWithATabGroupsProfileStillLoadAndPassTheirCheck()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "activeContextId": "work",
          "contexts": [
            {
              "id": "work",
              "displayName": "Work",
              "accentColor": "#2F6FED",
              "browser_management": { "mode": "groups", "browser": "Chrome", "tab_groups": ["Generation", "Work Services", "AI Blogging Tables"] }
            },
            {
              "id": "personal",
              "displayName": "Personal",
              "accentColor": "#22A06B",
              "browser_management": { "mode": "groups", "browser": "Safari", "urls": ["https://example.com/"], "tab_groups": ["Home"] }
            }
          ]
        }
        """;

        AppConfiguration? configuration = JsonSerializer.Deserialize<AppConfiguration>(json, ContextSwitcherJson.Options);

        Assert.NotNull(configuration);
        Assert.Equal(BrowserManagementMode.None, configuration.Contexts[0].BrowserManagement.Mode);
        Assert.Equal(BrowserManagementMode.Urls, configuration.Contexts[1].BrowserManagement.Mode);
        Assert.True(new ContextSwitcher.Core.Configuration.Validation.ConfigurationValidator().Validate(configuration).IsValid);
    }

    [Fact]
    public void PagesToCloseAreReadFromCloseUrls()
    {
        const string json = """
        { "mode": "urls", "browser": "Chrome", "urls": ["https://github.com/"], "close_urls": ["https://youtube.com/"] }
        """;

        BrowserManagementConfig? browser = JsonSerializer.Deserialize<BrowserManagementConfig>(json, ContextSwitcherJson.Options);

        Assert.Equal(["https://youtube.com/"], browser?.CloseUrls);
        Assert.Contains("\"close_urls\"", JsonSerializer.Serialize(browser, ContextSwitcherJson.Options), StringComparison.Ordinal);
    }
}
