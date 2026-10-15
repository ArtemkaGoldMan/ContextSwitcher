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
