namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Represents the persisted application configuration that drives context switching behavior.
/// </summary>
public sealed record AppConfiguration
{
    /// <summary>
    /// Gets or sets the schema version for the configuration document.
    /// </summary>
    /// <summary>
    /// The newest schema this build writes and understands. Anything higher was written by a later
    /// version and is refused rather than silently rewritten without its unknown fields.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>
    /// Gets or sets the currently active context identifier.
    /// </summary>
    public string ActiveContextId { get; init; } = "work";

    /// <summary>
    /// Gets or sets the default timeout used for switch automation steps.
    /// </summary>
    public int DefaultSwitchTimeoutSeconds { get; init; } = 45;

    /// <summary>
    /// Gets or sets a value indicating whether the Dock icon should be shown. On for a new install:
    /// on a MacBook with a notch the menu bar icon can be hidden, and the Dock is then the way back to
    /// the app. A settings file keeps the choice it was written with, so this changes no one's.
    /// </summary>
    public bool ShowDockIcon { get; init; } = true;

    /// <summary>
    /// Gets or sets the language the app shows: <c>system</c> for the first of the Mac's preferred
    /// languages the app has, or a language code such as <c>en</c> or <c>uk</c>. A string rather than
    /// an enum, so a new translation needs no change here. Read at startup.
    /// </summary>
    public string Language { get; init; } = "system";

    /// <summary>
    /// Gets or sets whether the app's windows are light, dark, or follow macOS. A file written before
    /// this setting existed reads as following macOS, which is what the app always did.
    /// </summary>
    public AppearanceMode Appearance { get; init; } = AppearanceMode.System;

    /// <summary>
    /// Gets or sets a value indicating whether the app looks for a new version on GitHub once a day.
    /// It only ever offers one; installing is always the user's click. Defaults to on, which a file
    /// written before this setting existed also reads as.
    /// </summary>
    public bool CheckForUpdates { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a switch started in the app first asks what to do with
    /// the apps and browser tabs open that neither profile in the switch has: keep them, close them,
    /// or add them to one of the two. Off unless turned on.
    /// </summary>
    public bool AskOnSwitch { get; init; }

    /// <summary>
    /// Gets or sets the moment until which everything the app does is written to the log, not only
    /// problems - turned on in Settings for a week of looking into something that is not a failure.
    /// Null, or a moment past, is the ordinary quiet log.
    /// </summary>
    public DateTimeOffset? DetailedLogUntil { get; init; }

    /// <summary>
    /// Gets or sets the apps ContextSwitcher never opens, closes, hides or asks about - one used in
    /// every profile, such as a messenger. Adding one takes it out of every profile; one still listed
    /// in a profile, by hand, is left alone all the same.
    /// </summary>
    public IReadOnlyList<string> IgnoredApps { get; init; } = [];

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
    /// are never shown the wizard. Only the app's start-up (<c>AppStartup</c>), creating a default
    /// configuration, writes <see langword="false"/> explicitly, so the wizard appears exactly once,
    /// on a genuinely new install.
    /// </remarks>
    public bool OnboardingCompleted { get; init; } = true;

    /// <summary>
    /// Gets or sets the collection of configured contexts.
    /// </summary>
    public IReadOnlyList<ContextDefinition> Contexts { get; init; } = [];
}
