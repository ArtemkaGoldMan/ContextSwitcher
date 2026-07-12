namespace ContextSwitcher.Core.Configuration.Validation;

/// <summary>
/// Represents the outcome of a configuration validation pass.
/// </summary>
/// <param name="Errors">The validation errors produced by the validator.</param>
public sealed record ConfigurationValidationResult(IReadOnlyList<ConfigurationValidationError> Errors)
{
    /// <summary>
    /// Gets a value indicating whether the configuration is valid.
    /// </summary>
    public bool IsValid => Errors.Count == 0;
}
