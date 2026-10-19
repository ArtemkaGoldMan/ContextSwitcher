using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Messages;

namespace ContextSwitcher.Tests.Configuration;

public sealed class ConfigurationValidationResultTests
{
    /// <summary>A refused save or broken settings are logged by the settings and the kind of problem - never by what was typed.</summary>
    [Fact]
    public void ProblemsAreDescribedForTheLogBySettingAlone()
    {
        ConfigurationValidationResult result = new(
        [
            ConfigurationValidationError.Of("contexts[3].accentColor", ConfigurationMessages.AccentColorInvalid.With()),
            new ConfigurationValidationError("contexts", "broken")
        ]);

        Assert.Equal("contexts[].accentColor (Config_AccentColorInvalid); contexts (no key)", result.DescribeForLog());
    }
}
