namespace ContextSwitcher.Infrastructure.Files;

/// <summary>
/// The folder this system keeps apps' settings in, which <see cref="ConfigPaths"/> puts the app's own
/// folder in. Each platform's registrations choose theirs: <see cref="DotConfig"/> or
/// <see cref="AppData"/>.
/// </summary>
/// <param name="Path">The folder.</param>
public sealed record SettingsRoot(string Path)
{
    /// <summary><c>~/.config</c>, where the app has always kept everything on a Mac.</summary>
    public static SettingsRoot DotConfig { get; } =
        new(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"));

    /// <summary>
    /// <c>%APPDATA%</c>, for Windows - not <c>%LOCALAPPDATA%</c>, where Velopack puts the app and which
    /// it replaces on each update.
    /// </summary>
    public static SettingsRoot AppData { get; } =
        new(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
}
