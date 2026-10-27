namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>An app in the Start menu: what the person sees it called, and the AppUserModelID that starts it.</summary>
/// <param name="Name">The name in the Start menu, which is how a profile names the app.</param>
/// <param name="AppId">
/// What <c>shell:AppsFolder\</c> opens: an AppUserModelID for a Store app or one that registers one,
/// a path to the executable for a desktop app that doesn't.
/// </param>
public sealed record StartApp(string Name, string AppId)
{
    /// <summary>
    /// The executable the Start menu's shortcut of this name points at, when there is one; how an
    /// app with an AppUserModelID of its own is told from the others once it runs.
    /// </summary>
    public string? Target { get; init; }
}
