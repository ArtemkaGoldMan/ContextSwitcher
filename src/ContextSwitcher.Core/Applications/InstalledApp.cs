namespace ContextSwitcher.Core.Applications;

/// <summary>
/// An application discovered on the machine, offered in the Profile Setup app picker.
/// </summary>
/// <param name="Name">
/// The bundle name without the <c>.app</c> suffix. This is deliberately the exact string
/// <c>open -a &lt;name&gt;</c> and <c>tell application "&lt;name&gt;"</c> expect, so a picked app is
/// guaranteed to work with the existing launch/quit automation (agent.md sections 9.1 and 9.2).
/// </param>
/// <param name="IconPath">
/// Absolute path to a cached PNG of the app's icon, or <see langword="null"/> when the bundle has
/// no extractable icon. Never blocks picker display - a missing icon is a cosmetic fallback.
/// </param>
public sealed record InstalledApp(string Name, string? IconPath);
