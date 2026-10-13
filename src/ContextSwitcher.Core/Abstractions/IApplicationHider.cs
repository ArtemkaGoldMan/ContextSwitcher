namespace ContextSwitcher.Core.Abstractions;

/// <summary>Hides running apps - as ⌘H does - so they keep their windows and come back at once.</summary>
public interface IApplicationHider
{
    /// <summary>
    /// Asks each of <paramref name="apps"/> that is running to hide; one that isn't running has
    /// nothing to hide. Returns how many were asked.
    /// </summary>
    int Hide(IReadOnlyList<string> apps);
}
