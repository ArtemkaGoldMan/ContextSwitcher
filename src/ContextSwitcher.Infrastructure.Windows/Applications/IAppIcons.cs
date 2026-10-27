namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>Draws an app's icon as Windows shows it in the Start menu. Behind an interface so that the picker is tested without drawing any.</summary>
public interface IAppIcons
{
    /// <summary>
    /// The icon of the app that <paramref name="appId"/> opens - an AppUserModelID or a path, as
    /// <c>shell:AppsFolder\</c> takes it - as a square PNG of <paramref name="size"/> pixels; null when
    /// Windows has no icon to give or won't draw it.
    /// </summary>
    byte[]? RenderPng(string appId, int size);
}
