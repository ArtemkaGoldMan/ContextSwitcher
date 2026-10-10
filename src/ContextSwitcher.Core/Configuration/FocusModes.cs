namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// The Focus modes macOS ships with, and the identifiers the Shortcuts "Set Focus" action uses for
/// them. The identifiers are not documented; these were read out of macOS 26's own system libraries
/// rather than guessed, and are what lets Context Switcher write a working Focus Shortcut for the
/// user instead of asking them to build one by hand. A mode the user made themselves has an
/// identifier of its own that no other app can see, so it is not here.
/// </summary>
public static class FocusModes
{
    /// <summary>What every Focus Shortcut's name starts with; the mode name follows.</summary>
    public const string ShortcutPrefix = "ContextSwitcher - Focus ";

    /// <summary>
    /// The mode name of the original, single Shortcut that turns Focus off. Still run when the
    /// mode-specific one is missing, for anyone who built it by hand as the docs used to say.
    /// </summary>
    public const string OffMode = "Off";

    private const string OffPrefix = OffMode + " - ";

    public static IReadOnlyList<FocusMode> BuiltIn { get; } =
    [
        new("Do Not Disturb", "com.apple.donotdisturb.mode.default"),
        new("Work", "com.apple.focus.work"),
        new("Personal", "com.apple.focus.personal-time"),
        new("Sleep", "com.apple.sleep.sleep-mode"),
        new("Reduce Interruptions", "com.apple.focus.reduce-interruptions"),
        new("Reading", "com.apple.focus.reading"),
        new("Fitness", "com.apple.donotdisturb.mode.workout"),
        new("Gaming", "com.apple.focus.gaming"),
        new("Mindfulness", "com.apple.focus.mindfulness")
    ];

    /// <summary>The built-in mode with this name, or null for a mode the user made.</summary>
    public static FocusMode? Find(string? name) =>
        BuiltIn.FirstOrDefault(mode => string.Equals(mode.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The Shortcut a switch runs to turn <paramref name="modeName"/> on.</summary>
    public static string ShortcutName(string modeName) => ShortcutPrefix + modeName.Trim();

    /// <summary>
    /// The Shortcut a switch runs to turn <paramref name="modeName"/> off, when leaving a profile
    /// that turned it on for one with no Focus. One per mode, because turning off a mode macOS has
    /// not been set up with is an error that stops the Shortcut: a single Shortcut turning off every
    /// built-in mode failed on the first one the user had never created ("Reading").
    /// </summary>
    public static string OffShortcutName(string modeName) => ShortcutPrefix + OffPrefix + modeName.Trim();

    /// <summary>The original single "Focus Off" Shortcut's name.</summary>
    public static string LegacyOffShortcutName => ShortcutName(OffMode);

    /// <summary>
    /// What a Context Switcher Focus Shortcut is for, from its name: turning a mode on, turning one
    /// off, or the original Off (an empty mode). False for any other Shortcut.
    /// </summary>
    public static bool TryParseShortcutName(string name, out string modeName, out bool turnsOff)
    {
        modeName = string.Empty;
        turnsOff = false;
        if (!name.StartsWith(ShortcutPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string rest = name[ShortcutPrefix.Length..].Trim();
        if (rest == OffMode)
        {
            turnsOff = true;
            return true;
        }

        if (rest.StartsWith(OffPrefix, StringComparison.Ordinal))
        {
            modeName = rest[OffPrefix.Length..].Trim();
            turnsOff = true;
            return modeName.Length > 0;
        }

        modeName = rest;
        return rest.Length > 0;
    }
}

/// <param name="Name">As macOS shows it, and as a profile's <c>focus.modeName</c> stores it.</param>
/// <param name="Identifier">What the Shortcuts "Set Focus" action calls it.</param>
public sealed record FocusMode(string Name, string Identifier);
