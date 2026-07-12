namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Configures macOS Focus mode automation for a context.
/// </summary>
public sealed record FocusConfig
{
    /// <summary>
    /// Gets or sets a value indicating whether Focus mode automation is enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets or sets the name of the Focus mode shortcut or preset to activate.
    /// </summary>
    public string ModeName { get; init; } = string.Empty;
}
