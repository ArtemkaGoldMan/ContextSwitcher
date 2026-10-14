using System.Text.RegularExpressions;

namespace ContextSwitcher.Core.Logging;

/// <summary>
/// Takes what is private out of a log entry before it is kept. A log is something a person is asked
/// to send when the app misbehaves, and the process output in it - an osascript error, a failed
/// shortcut - can quote the address of a tab, the email of a browser profile or a path through the
/// home folder. The site is kept, since it is what tells one failure from another; the rest is not.
/// </summary>
public static partial class LogRedactor
{
    /// <summary>The entry with its message and its data cleaned.</summary>
    public static LogEntry Redact(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return entry with
        {
            Message = Redact(entry.Message),
            Data = entry.Data?.ToDictionary(pair => pair.Key, pair => Redact(pair.Value), StringComparer.Ordinal)
        };
    }

    /// <summary>
    /// The text with web addresses cut to their site ("https://linear.app/…"), email addresses and the
    /// home folder's name taken out.
    /// </summary>
    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        string text = UrlPattern().Replace(value, Site);
        text = EmailPattern().Replace(text, "<email>");
        return HomePattern().Replace(text, "~");
    }

    private static string Site(Match match)
    {
        // "user:password@host" is a login, not a host.
        string host = match.Groups["host"].Value;
        host = host[(host.LastIndexOf('@') + 1)..];
        string rest = match.Groups["rest"].Value;
        return $"{match.Groups["scheme"].Value.ToLowerInvariant()}://{host}{(rest.Length > 1 ? "/…" : rest)}";
    }

    [GeneratedRegex(@"(?<scheme>https?)://(?<host>[^/\s'""?#]+)(?<rest>[/?#][^\s'""]*)?", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"/Users/[^/\s'""]+")]
    private static partial Regex HomePattern();
}
