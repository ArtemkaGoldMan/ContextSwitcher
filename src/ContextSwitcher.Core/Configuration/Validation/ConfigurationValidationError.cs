namespace ContextSwitcher.Core.Configuration.Validation;

public sealed record ConfigurationValidationError(string Path, string Message);
