namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Configures the desired theme mode for a context.
/// </summary>
public sealed record ThemeConfig
{
    /// <summary>
    /// Gets or sets the theme mode to apply when switching to the context.
    /// </summary>
    public ThemeMode Mode { get; init; } = ThemeMode.System;
}
