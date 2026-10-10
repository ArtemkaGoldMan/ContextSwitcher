namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// The icons a profile can use, keyed by the name stored in <c>ContextDefinition.Icon</c>.
///
/// Geometry is Lucide (https://lucide.dev, ISC licensed - see THIRD-PARTY-NOTICES.md), drawn on the
/// same 24x24 stroked grid as the rest of the app's icons. It is kept here as path data rather than
/// as Avalonia geometry objects: a Geometry belongs to the thread that created it, and view models
/// get rebuilt in response to configuration changes, so they only ever carry the name and
/// <see cref="Converters.ProfileIconConverter"/> turns it into geometry while the UI draws.
/// </summary>
public static class ProfileIcons
{
    /// <summary>Used for an empty name, and for any name this build does not know.</summary>
    public const string Fallback = "circle";

    public static IReadOnlyList<ProfileIcon> All { get; } =
    [
        new("circle", "Circle", "M22 12A10 10 0 1 1 2 12A10 10 0 1 1 22 12Z"),
        new("briefcase", "Work", "M16 20V4a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16 M4 6H20A2 2 0 0 1 22 8V18A2 2 0 0 1 20 20H4A2 2 0 0 1 2 18V8A2 2 0 0 1 4 6Z"),
        new("house", "Home", "M15 21v-8a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v8 M3 10a2 2 0 0 1 .709-1.528l7-6a2 2 0 0 1 2.582 0l7 6A2 2 0 0 1 21 10v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"),
        new("code", "Code", "M16 18 22 12 16 6 M8 6 2 12 8 18"),
        new("laptop", "Laptop", "M20 16V7a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v9m16 0H4m16 0 1.28 2.55a1 1 0 0 1-.9 1.45H3.62a1 1 0 0 1-.9-1.45L4 16"),
        new("book", "Study", "M12 7v14 M3 18a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1h5a4 4 0 0 1 4 4 4 4 0 0 1 4-4h5a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1h-6a3 3 0 0 0-3 3 3 3 0 0 0-3-3z"),
        new("coffee", "Break", "M10 2v2 M14 2v2 M16 8a1 1 0 0 1 1 1v8a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4V9a1 1 0 0 1 1-1h14a4 4 0 1 1 0 8h-1 M6 2v2"),
        new("moon", "Evening", "M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"),
        new("sun", "Morning", "M16 12A4 4 0 1 1 8 12A4 4 0 1 1 16 12Z M12 2v2 M12 20v2 M4.93 4.93l1.41 1.41 M17.66 17.66l1.41 1.41 M2 12h2 M20 12h2 M6.34 17.66l-1.41 1.41 M19.07 4.93l-1.41 1.41"),
        new("heart", "Personal", "M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z"),
        new("music", "Music", "M9 18V5l12-2v13 M9 18A3 3 0 1 1 3 18A3 3 0 1 1 9 18Z M21 16A3 3 0 1 1 15 16A3 3 0 1 1 21 16Z"),
        new("gamepad", "Games", "M6 12h4 M8 10v4 M15 13h.01 M18 11h.01 M17.32 5H6.68a4 4 0 0 0-3.978 3.59c-.006.052-.01.101-.017.152C2.604 9.416 2 14.456 2 16a3 3 0 0 0 3 3c1 0 1.5-.5 2-1l1.414-1.414A2 2 0 0 1 9.828 16h4.344a2 2 0 0 1 1.414.586L17 18c.5.5 1 1 2 1a3 3 0 0 0 3-3c0-1.545-.604-6.584-.685-7.258-.007-.05-.011-.1-.017-.151A4 4 0 0 0 17.32 5z"),
        new("star", "Favourite", "M12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26Z"),
        new("zap", "Focus", "M4 14a1 1 0 0 1-.78-1.63l9.9-10.2a.5.5 0 0 1 .86.46l-1.92 6.02A1 1 0 0 0 13 10h7a1 1 0 0 1 .78 1.63l-9.9 10.2a.5.5 0 0 1-.86-.46l1.92-6.02A1 1 0 0 0 11 14z"),
        new("leaf", "Calm", "M11 20A7 7 0 0 1 9.8 6.1C15.5 5 17 4.48 19 2c1 2 2 4.18 2 8 0 5.5-4.78 10-10 10Z M2 21c0-3 1.85-5.36 5.08-6C9.5 14.52 12 13 13 12"),
        new("target", "Goals", "M22 12A10 10 0 1 1 2 12A10 10 0 1 1 22 12Z M18 12A6 6 0 1 1 6 12A6 6 0 1 1 18 12Z M14 12A2 2 0 1 1 10 12A2 2 0 1 1 14 12Z")
    ];

    /// <summary>The icon for a stored name, falling back to <see cref="Fallback"/> for unknown ones.</summary>
    public static ProfileIcon Find(string? name) =>
        All.FirstOrDefault(icon => string.Equals(icon.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? All.First(icon => icon.Name == Fallback);
}

/// <param name="Name">What <c>ContextDefinition.Icon</c> stores.</param>
/// <param name="Label">Shown as the picker's tooltip and read out by VoiceOver.</param>
/// <param name="PathData">Lucide path data on a 24x24 grid.</param>
public sealed record ProfileIcon(string Name, string Label, string PathData);
