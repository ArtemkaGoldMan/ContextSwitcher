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
        Assert.Equal("Work-Core", browserManagement.TabGroups[0]);
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
                        Mode = BrowserManagementMode.Groups,
                        Browser = BrowserKind.Safari,
                        TabGroups = ["Work-Core"],
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
        Assert.Contains("\"tab_groups\"", json, StringComparison.Ordinal);
        Assert.Contains("\"profile_directory\"", json, StringComparison.Ordinal);
        Assert.Contains("\"mode\": \"groups\"", json, StringComparison.Ordinal);
        Assert.Contains("\"browser\": \"Safari\"", json, StringComparison.Ordinal);
    }
}
