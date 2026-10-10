using System.Text.Json;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Serialization;

namespace ContextSwitcher.Tests.Configuration;

/// <summary>
/// The theme setting is new: a settings.json from an earlier build has no such field and must keep
/// following macOS, as the app always did, and the value is written as a readable word.
/// </summary>
public sealed class AppearanceSettingTests
{
    [Fact]
    public void ConfigWrittenBeforeTheSettingExistedFollowsMacOS()
    {
        const string json = """
        {
            "schemaVersion": 1,
            "activeContextId": "work",
            "contexts": [{ "id": "work", "displayName": "Work" }]
        }
        """;

        AppConfiguration? configuration = JsonSerializer.Deserialize<AppConfiguration>(json, ContextSwitcherJson.Options);

        Assert.Equal(AppearanceMode.System, configuration!.Appearance);
    }

    [Theory]
    [InlineData(AppearanceMode.System, "system")]
    [InlineData(AppearanceMode.Light, "light")]
    [InlineData(AppearanceMode.Dark, "dark")]
    public void IsWrittenAsAWordAndReadBack(AppearanceMode appearance, string word)
    {
        AppConfiguration original = new()
        {
            ActiveContextId = "work",
            Appearance = appearance,
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }]
        };

        string json = JsonSerializer.Serialize(original, ContextSwitcherJson.Options);

        Assert.Contains($"\"appearance\": \"{word}\"", json, StringComparison.Ordinal);
        Assert.Equal(appearance, JsonSerializer.Deserialize<AppConfiguration>(json, ContextSwitcherJson.Options)!.Appearance);
    }
}
