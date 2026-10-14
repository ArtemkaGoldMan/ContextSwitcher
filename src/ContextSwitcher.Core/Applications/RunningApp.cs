namespace ContextSwitcher.Core.Applications;

/// <summary>
/// An app running right now with a place in the Dock - VS Code, not Finder or a menu bar helper.
/// </summary>
/// <param name="Name">Its name as Finder shows it, without <c>.app</c> - what a profile lists.</param>
/// <param name="BundleName">The name in its bundle, which can differ ("Code" for Visual Studio Code).</param>
/// <param name="LocalizedName">The name macOS shows for it in the Mac's language.</param>
public sealed record RunningApp(string Name, string? BundleName = null, string? LocalizedName = null)
{
    /// <summary>Whether <paramref name="listed"/>, as a profile or a list names an app, is this one.</summary>
    public bool IsCalled(string listed) =>
        string.Equals(listed, this.Name, StringComparison.OrdinalIgnoreCase)
        || string.Equals(listed, this.BundleName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(listed, this.LocalizedName, StringComparison.OrdinalIgnoreCase);
}
