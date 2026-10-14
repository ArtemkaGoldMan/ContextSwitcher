using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Core.Catalog;

/// <summary>A tab open right now in a browser that can be asked - Chrome, Brave or Safari - outside private windows.</summary>
public sealed record OpenBrowserTab(BrowserKind Browser, string Title, string Url);
