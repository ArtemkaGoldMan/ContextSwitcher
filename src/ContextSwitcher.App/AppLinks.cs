namespace ContextSwitcher.App;

/// <summary>
/// Outbound links the UI opens, kept in one place so the donation target can be repointed without
/// hunting through view models.
/// </summary>
public static class AppLinks
{
    /// <summary>
    /// Where "Support the developer" goes - in Settings and in the dashboard: the developer's own
    /// support page, every way to donate in one place, shared by all their projects. It is in every
    /// release, so it is a page that stays put while the ways on it change - a new one is added
    /// there, not here.
    /// </summary>
    public const string Support = "https://artemkagoldman.github.io/support/";

    /// <summary>
    /// Where people write about a problem or an idea, no account needed. It is in every release and
    /// in the README, so it stays: a copy nobody updated still writes here.
    /// </summary>
    public const string ContactEmail = "contextswitcher.app@gmail.com";

    /// <summary>GitHub's "Something doesn't work" issue form, for people with an account.</summary>
    public const string NewIssue = "https://github.com/ArtemkaGoldMan/ContextSwitcher/issues/new?template=bug_report.yml";

    /// <summary>The README's Contact section: what to write, and what to attach.</summary>
    public const string Contact = "https://github.com/ArtemkaGoldMan/ContextSwitcher#contact";
}
