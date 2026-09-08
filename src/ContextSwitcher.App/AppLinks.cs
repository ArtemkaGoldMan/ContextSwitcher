namespace ContextSwitcher.App;

/// <summary>
/// Outbound links the UI opens, kept in one place so the donation target can be repointed without
/// hunting through view models.
/// </summary>
public static class AppLinks
{
    /// <summary>
    /// Where "Support the developer" goes. agent.md section 19 plans a Polar.sh donation page;
    /// until that exists this opens the project itself, whose README carries the support section.
    /// Both buttons were previously wired to an empty lambda, so they were visible, clickable and
    /// did nothing at all - pointing them somewhere real is the smaller of the two evils until
    /// there is a page to point at.
    /// </summary>
    public const string Support = "https://github.com/ArtemkaGoldMan/ContextSwitcher#support";
}
