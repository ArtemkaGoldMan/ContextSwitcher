using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>Which browser macOS opens web links in.</summary>
public interface IDefaultBrowser
{
    /// <summary>The default browser now, or null where it can't be found out.</summary>
    DefaultBrowser? Get();
}

/// <summary>
/// The default browser: <see cref="Kind"/> is the one ContextSwitcher can script, or
/// <see cref="BrowserKind.Default"/> for one it can't (Firefox, Arc…); <see cref="Name"/> is what the
/// app is called, for saying so.
/// </summary>
public sealed record DefaultBrowser(BrowserKind Kind, string Name);
