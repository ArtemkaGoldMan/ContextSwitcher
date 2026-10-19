using System.Text.RegularExpressions;

namespace ContextSwitcher.Core.Configuration.Validation;

/// <summary>
/// Represents the outcome of a configuration validation pass.
/// </summary>
/// <param name="Errors">The validation errors produced by the validator.</param>
public sealed partial record ConfigurationValidationResult(IReadOnlyList<ConfigurationValidationError> Errors)
{
    /// <summary>
    /// Gets a value indicating whether the configuration is valid.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// The problems for a log line: each by its setting, without the profile's number, and the kind of
    /// problem - which settings, never what the person typed or named.
    /// </summary>
    public string DescribeForLog() =>
        string.Join("; ", this.Errors.Select(error => $"{ProfileIndex().Replace(error.Path, "[]")} ({error.Detail?.Key ?? "no key"})"));

    [GeneratedRegex(@"\[\d+\]")]
    private static partial Regex ProfileIndex();
}
