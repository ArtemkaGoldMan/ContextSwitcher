namespace ContextSwitcher.Core.Updates;

/// <summary>
/// Reads a version from a release tag or a bundle version - "v0.2.0", "0.2.0", "0.2.0+abc123" - and
/// writes one back as the three numbers people see.
/// </summary>
public static class ReleaseVersion
{
    /// <summary>
    /// Parses <paramref name="text"/>, ignoring a leading "v" and anything after a "-" or "+" (a
    /// pre-release or build label). Missing parts count as zero, so "1.2" and "1.2.0" compare equal.
    /// </summary>
    public static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string core = text.Trim().TrimStart('v', 'V');
        int label = core.IndexOfAny(['-', '+']);
        if (label >= 0)
        {
            core = core[..label];
        }

        if (!Version.TryParse(core.Contains('.', StringComparison.Ordinal) ? core : core + ".0", out Version? parsed))
        {
            return false;
        }

        version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
        return true;
    }

    /// <summary>"0.2.0".</summary>
    public static string Format(Version version) => version.ToString(3);
}
