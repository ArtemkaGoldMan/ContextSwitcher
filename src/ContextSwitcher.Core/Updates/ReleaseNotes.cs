using System.Text;
using System.Text.RegularExpressions;

namespace ContextSwitcher.Core.Updates;

/// <summary>
/// The "What's new" list from a release's description on GitHub - the section at the top of
/// <c>.github/release-notes.md</c> - so Settings can show what an update brings before it is
/// installed, rather than only a link to the page.
/// </summary>
public static partial class ReleaseNotes
{
    /// <summary>
    /// The items of the description's "What's new" section, as plain text: one per bullet, Markdown
    /// emphasis, code and links reduced to their words. Empty when there is no such section.
    /// </summary>
    public static IReadOnlyList<string> WhatsNew(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return [];
        }

        List<string> items = [];
        StringBuilder? current = null;
        bool inSection = false;

        foreach (string raw in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith('#'))
            {
                if (inSection)
                {
                    break;
                }

                inSection = WhatsNewHeading().IsMatch(line);
                continue;
            }

            if (!inSection)
            {
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                Flush(items, current);
                current = new StringBuilder(line[2..].Trim());
            }
            else if (line.Length > 0 && current is not null)
            {
                // A bullet wrapped onto the next line carries on.
                current.Append(' ').Append(line);
            }
        }

        Flush(items, current);
        return items;
    }

    private static void Flush(List<string> items, StringBuilder? item)
    {
        if (item is null)
        {
            return;
        }

        string text = Link().Replace(item.ToString(), "$1");
        text = text.Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Trim();
        if (text.Length > 0)
        {
            items.Add(text);
        }
    }

    [GeneratedRegex(@"^#{1,6}\s*what[’']s new\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WhatsNewHeading();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex Link();
}
