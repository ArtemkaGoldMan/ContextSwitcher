using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;

namespace ContextSwitcher.Tests.Configuration;

/// <summary>An app never touched is taken out of every profile, and the switch leaves it alone even if one still lists it.</summary>
public sealed class IgnoredAppsTests
{
    private static readonly AppConfiguration Configuration = new()
    {
        ActiveContextId = "work",
        Contexts =
        [
            new ContextDefinition { Id = "work", DisplayName = "Work", AccentColor = "#2F6FED", LaunchApps = ["Telegram", "Slack"], CloseApps = ["telegram"] },
            new ContextDefinition { Id = "personal", DisplayName = "Personal", AccentColor = "#20A67A", HideApps = ["Telegram"] },
            new ContextDefinition { Id = "study", DisplayName = "Study", AccentColor = "#7C5CFF", LaunchApps = ["Notes"] }
        ]
    };

    [Fact]
    public void IgnoringAnAppTakesItOutOfEveryProfile()
    {
        (AppConfiguration updated, IReadOnlyList<string> takenOutOf) = IgnoredApps.Ignore(Configuration, "Telegram");

        Assert.Equal(["Telegram"], updated.IgnoredApps);
        Assert.Equal(["Work", "Personal"], takenOutOf);
        Assert.Equal(["Slack"], updated.Contexts[0].LaunchApps);
        Assert.Empty(updated.Contexts[0].CloseApps);
        Assert.Empty(updated.Contexts[1].HideApps);
        Assert.Same(Configuration.Contexts[2], updated.Contexts[2]);
        Assert.True(new ConfigurationValidator().Validate(updated).IsValid);

        Assert.Equal(["Telegram"], IgnoredApps.Ignore(updated, "telegram").Configuration.IgnoredApps);
        Assert.Empty(IgnoredApps.StopIgnoring(updated, "TELEGRAM").IgnoredApps);
    }

    [Fact]
    public void AnIgnoredAppNeedsAName()
    {
        ConfigurationValidationResult result = new ConfigurationValidator().Validate(Configuration with { IgnoredApps = ["Telegram", " "] });

        Assert.Contains(result.Errors, error => error.Path == "ignoredApps[1]");
    }

    [Fact]
    public void BothSettingsAreReadAndWritten()
    {
        string json = System.Text.Json.JsonSerializer.Serialize(Configuration with { AskOnSwitch = true, IgnoredApps = ["Telegram"] }, Core.Serialization.ContextSwitcherJson.Options);
        AppConfiguration read = System.Text.Json.JsonSerializer.Deserialize<AppConfiguration>(json, Core.Serialization.ContextSwitcherJson.Options)!;

        Assert.True(read.AskOnSwitch);
        Assert.Equal(["Telegram"], read.IgnoredApps);
        Assert.False(System.Text.Json.JsonSerializer.Deserialize<AppConfiguration>("{}", Core.Serialization.ContextSwitcherJson.Options)!.AskOnSwitch);
    }
}
