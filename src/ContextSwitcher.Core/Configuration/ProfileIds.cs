using System.Text.RegularExpressions;

namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Ids for new profiles - from a name, for a copy, or before a profile is named - never one already
/// taken: the first free of the base, then <c>base-2</c>, <c>base-3</c> and so on. One place, so what
/// an id may contain and how a clash is avoided can't drift apart between the pages that make them.
/// </summary>
public static partial class ProfileIds
{
    /// <summary>
    /// An id from a name: lowercased, its letters and digits joined with hyphens - exactly what the
    /// id pattern accepts. A name with nothing usable in it (all punctuation, or a non-Latin script)
    /// gives <c>profile</c>.
    /// </summary>
    public static string FromName(string displayName, IEnumerable<ContextDefinition> existing)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        string slug = NotIdCharacters().Replace(displayName.Trim().ToLowerInvariant(), "-").Trim('-');
        return Unique(slug.Length == 0 ? "profile" : slug, existing);
    }

    /// <summary>A copy's id: <c>work-copy</c>, then <c>work-copy-2</c> and so on.</summary>
    public static string ForCopyOf(string id, IEnumerable<ContextDefinition> existing) => Unique($"{id}-copy", existing);

    /// <summary>A profile's id before it is named: <c>profile</c>, then <c>profile-2</c> and so on.</summary>
    public static string ForNewProfile(IEnumerable<ContextDefinition> existing) => Unique("profile", existing);

    /// <summary>The first of <paramref name="baseId"/>, <c>baseId-2</c>, <c>baseId-3</c>… no profile has.</summary>
    public static string Unique(string baseId, IEnumerable<ContextDefinition> existing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseId);
        ArgumentNullException.ThrowIfNull(existing);

        HashSet<string> taken = existing.Select(context => context.Id).ToHashSet(StringComparer.Ordinal);
        string candidate = baseId;
        for (int suffix = 2; taken.Contains(candidate); suffix++)
        {
            candidate = $"{baseId}-{suffix}";
        }

        return candidate;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NotIdCharacters();
}
