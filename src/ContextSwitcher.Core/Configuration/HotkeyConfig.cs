namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Represents a global hotkey that can switch to a specific context.
/// </summary>
public sealed record HotkeyConfig
{
    /// <summary>
    /// Gets or sets the unique identifier for the hotkey definition.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the context identifier that the hotkey targets.
    /// </summary>
    public string ContextId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the accelerator string for the hotkey.
    /// </summary>
    public string Accelerator { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the hotkey is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;
}
