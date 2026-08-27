using System.Text.Json;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Serialization;

namespace ContextSwitcher.Tests.Configuration;

/// <summary>
/// Guards the first-run wizard's trigger. Getting this wrong is silently bad in both directions:
/// too eager and every existing user is re-onboarded on upgrade, too lazy and a fresh install
/// lands on one empty profile with no guidance.
/// </summary>
public sealed class OnboardingCompletedTests
{
    [Fact]
    public void ConfigWrittenBeforeTheFieldExistedDeserializesAsAlreadyOnboarded()
    {
        // Exactly the shape of a settings.json written by an earlier build: no such property.
        const string json = """
        {
            "schemaVersion": 1,
            "activeContextId": "work",
            "contexts": [{ "id": "work", "displayName": "Work" }]
        }
        """;

        AppConfiguration? configuration = JsonSerializer.Deserialize<AppConfiguration>(json, ContextSwitcherJson.Options);

        Assert.NotNull(configuration);
        Assert.True(configuration.OnboardingCompleted);
    }

    [Fact]
    public void ExplicitFalseIsPreservedThroughARoundTrip()
    {
        AppConfiguration original = new()
        {
            ActiveContextId = "work",
            OnboardingCompleted = false,
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }]
        };

        string json = JsonSerializer.Serialize(original, ContextSwitcherJson.Options);
        AppConfiguration? roundTripped = JsonSerializer.Deserialize<AppConfiguration>(json, ContextSwitcherJson.Options);

        Assert.NotNull(roundTripped);
        Assert.False(roundTripped.OnboardingCompleted);
    }
}
