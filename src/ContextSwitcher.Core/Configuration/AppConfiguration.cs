namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Represents the persisted application configuration that drives context switching behavior.
/// </summary>
public sealed record AppConfiguration
{
    /// <summary>
    /// Gets or sets the schema version for the configuration document.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Gets or sets the currently active context identifier.
    /// </summary>
    public string ActiveContextId { get; init; } = "work";

    /// <summary>
    /// Gets or sets the default timeout used for switch automation steps.
    /// </summary>
    public int DefaultSwitchTimeoutSeconds { get; init; } = 45;

    /// <summary>
    /// Gets or sets a value indicating whether the Dock icon should be shown.
    /// </summary>
    public bool ShowDockIcon { get; init; }

    /// <summary>
    /// Gets or sets analytics configuration for the app.
    /// </summary>
    public AnalyticsConfiguration Analytics { get; init; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the first-run wizard has been completed.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/> on purpose. A <c>settings.json</c> written before this
    /// field existed simply omits it, and must deserialize as already-onboarded so upgrading users
    /// are never shown the wizard. Only <see cref="AppHost"/>'s freshly-created default
    /// configuration writes <see langword="false"/> explicitly, so the wizard appears exactly once,
    /// on a genuinely new install.
    /// </remarks>
    public bool OnboardingCompleted { get; init; } = true;

    /// <summary>
    /// Gets or sets the list of registered hotkeys for context switching.
    /// </summary>
    public IReadOnlyList<HotkeyConfig> Hotkeys { get; init; } = [];

    /// <summary>
    /// Gets or sets the collection of configured contexts.
    /// </summary>
    public IReadOnlyList<ContextDefinition> Contexts { get; init; } = [];
}
