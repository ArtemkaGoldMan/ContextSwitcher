using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;

namespace ContextSwitcher.Tests.Configuration;

public sealed class SchemaVersionTests
{
    /// <summary>
    /// Any version but the current one used to be a hard validation failure, which would have left
    /// the first schema change with no upgrade path: every existing config would simply stop being
    /// valid. Older is now read as-is; only newer is refused, and with a message that says why.
    /// </summary>
    [Fact]
    public void AConfigurationFromANewerBuildIsRefusedWithAnExplanation()
    {
        ConfigurationValidator validator = new();

        ConfigurationValidationResult result = validator.Validate(Configuration(AppConfiguration.CurrentSchemaVersion + 1));

        Assert.False(result.IsValid);
        ConfigurationValidationError error = Assert.Single(result.Errors, e => e.Path == "schemaVersion");
        Assert.Contains("newer version", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheCurrentSchemaIsValid()
    {
        Assert.True(new ConfigurationValidator().Validate(Configuration(AppConfiguration.CurrentSchemaVersion)).IsValid);
    }

    [Fact]
    public void AZeroOrNegativeVersionIsStillRejected()
    {
        Assert.False(new ConfigurationValidator().Validate(Configuration(0)).IsValid);
    }

    private static AppConfiguration Configuration(int schemaVersion) => new()
    {
        SchemaVersion = schemaVersion,
        ActiveContextId = "work",
        Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }]
    };
}
