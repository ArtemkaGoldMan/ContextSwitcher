namespace ContextSwitcher.Core.Abstractions;

/// <summary>The small icon a website shows in its tab, kept on this Mac once found.</summary>
public interface ISiteIcons
{
    /// <summary>A PNG of the icon of <paramref name="pageUrl"/>'s site, or <see langword="null"/> when it has none to give.</summary>
    Task<string?> GetIconPathAsync(string pageUrl, CancellationToken cancellationToken);
}
