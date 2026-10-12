namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Whether a browser tab shows one of a profile's pages. A page rarely stays at the address it was
/// given: sites redirect (<c>mail.google.com</c> lands on <c>…/mail/u/0/#inbox</c>), add to the
/// query, and change the address as you click around. Comparing addresses exactly - which "avoid
/// duplicate tabs" did - missed most of them and opened a second tab.
///
/// So a tab matches a page when it is on the same site, at the page's path or anywhere below it:
/// <c>https://github.com/ArtemkaGoldMan</c> matches every page of that account and nothing else on
/// GitHub, and <c>https://mail.google.com/</c> matches all of Gmail. <c>http</c> and <c>https</c>,
/// a leading <c>www.</c>, a trailing slash, letter case and the <c>#</c> part don't count. A page
/// given with a query - a particular video - matches only tabs with that query or more of it.
/// </summary>
public static class PageMatch
{
    /// <summary>Whether <paramref name="tabUrl"/> shows <paramref name="page"/> or a page under it.</summary>
    public static bool Matches(string page, string tabUrl)
    {
        if (!TryParse(page, out Uri? wanted) || !TryParse(tabUrl, out Uri? tab))
        {
            return false;
        }

        bool sameSite = string.Equals(Site(wanted), Site(tab), StringComparison.OrdinalIgnoreCase)
            && (wanted.Port == tab.Port || (wanted.IsDefaultPort && tab.IsDefaultPort));
        if (!sameSite)
        {
            return false;
        }

        string wantedPath = wanted.AbsolutePath.TrimEnd('/');
        string tabPath = tab.AbsolutePath.TrimEnd('/');
        bool samePath = tabPath.Equals(wantedPath, StringComparison.OrdinalIgnoreCase);
        if (!samePath && !tabPath.StartsWith(wantedPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string wantedQuery = wanted.Query.TrimStart('?');
        return wantedQuery.Length == 0
            || (samePath && tab.Query.TrimStart('?').StartsWith(wantedQuery, StringComparison.Ordinal));
    }

    /// <summary>Whether <paramref name="tabUrl"/> shows any of <paramref name="pages"/>.</summary>
    public static bool MatchesAny(IEnumerable<string> pages, string tabUrl) =>
        pages.Any(page => Matches(page, tabUrl));

    private static bool TryParse(string value, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Uri? uri) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string Site(Uri uri) =>
        uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
}
