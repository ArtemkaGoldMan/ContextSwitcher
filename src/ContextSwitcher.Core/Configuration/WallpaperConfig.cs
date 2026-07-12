namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Configures the wallpaper that should be applied for a context.
/// </summary>
public sealed record WallpaperConfig
{
    /// <summary>
    /// Gets or sets the filesystem path to the wallpaper image.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the wallpaper should be applied to all spaces.
    /// </summary>
    public bool AllSpaces { get; init; } = true;
}
