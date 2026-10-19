using System.Text.Json;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Serialization;

namespace ContextSwitcher.Tests.Configuration;

/// <summary>
/// The map from an app's name to how Windows finds it. A settings file with it, and one without it,
/// both read and save unchanged on the Mac.
/// </summary>
public sealed class AppIdentityTests
{
    private static readonly AppConfiguration Configuration = new()
    {
        ActiveContextId = "work",
        Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work", LaunchApps = ["Slack", "Visual Studio Code"] }]
    };

    [Fact]
    public void AFileWithoutTheMapIsSavedWithoutIt()
    {
        string json = JsonSerializer.Serialize(Configuration, ContextSwitcherJson.Options);

        Assert.DoesNotContain("\"apps\"", json, StringComparison.Ordinal);
        Assert.Equal(json, RoundTrip(json));
    }

    [Fact]
    public void TheMapIsReadAndSavedAsItWas()
    {
        string json = JsonSerializer.Serialize(
            Configuration with
            {
                Apps = new Dictionary<string, AppIdentity>
                {
                    ["Slack"] = new() { WindowsAppId = "com.squirrel.slack.slack", WindowsExe = "slack.exe" },
                    ["Visual Studio Code"] = new() { WindowsExe = "Code.exe" }
                }
            },
            ContextSwitcherJson.Options);

        Assert.Equal(json, RoundTrip(json));
        AppConfiguration read = JsonSerializer.Deserialize<AppConfiguration>(json, ContextSwitcherJson.Options)!;
        Assert.Equal("com.squirrel.slack.slack", read.Apps!["Slack"].WindowsAppId);
        Assert.Null(read.Apps["Visual Studio Code"].WindowsAppId);
    }

    /// <summary>As the Windows plan writes it: the app's name as the key, as the profiles list it.</summary>
    [Fact]
    public void TheMapIsWrittenAsPlanned()
    {
        AppConfiguration read = JsonSerializer.Deserialize<AppConfiguration>(
            """{ "apps": { "Slack": { "windows_app_id": "com.squirrel.slack.slack", "windows_exe": "slack.exe" } } }""",
            ContextSwitcherJson.Options)!;

        Assert.Equal(new AppIdentity { WindowsAppId = "com.squirrel.slack.slack", WindowsExe = "slack.exe" }, read.Apps!["Slack"]);
    }

    private static string RoundTrip(string json) =>
        JsonSerializer.Serialize(JsonSerializer.Deserialize<AppConfiguration>(json, ContextSwitcherJson.Options), ContextSwitcherJson.Options);
}
