namespace ContextSwitcher.Core.Catalog;

/// <summary>
/// A Chromium browser profile found on this Mac, offered in Profile Setup instead of a free-text
/// profile directory.
/// </summary>
/// <param name="Directory">
/// The folder name under the browser's data directory - <c>Default</c>, <c>Profile 1</c> - which is
/// what <c>--profile-directory</c> and <c>BrowserProfileConfig.ProfileDirectory</c> expect.
/// </param>
/// <param name="Name">The name the browser shows for the profile, such as the person's name.</param>
public sealed record BrowserProfile(string Directory, string Name);
