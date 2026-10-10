namespace ContextSwitcher.Core.Catalog;

/// <summary>A tab open in a browser right now, offered as a startup URL or quick link.</summary>
public sealed record OpenTab(string Title, string Url);
