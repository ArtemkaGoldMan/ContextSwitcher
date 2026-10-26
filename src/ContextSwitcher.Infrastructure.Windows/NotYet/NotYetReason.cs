namespace ContextSwitcher.Infrastructure.Windows.NotYet;

/// <summary>
/// What a stand-in says when asked for what Windows doesn't have yet, for the log. Each stand-in in
/// this folder goes when the stage of <c>docs/windows-plan.md</c> that brings its part arrives; Profile
/// Setup and Settings already hide what they stand in for, through the platform's capabilities.
/// </summary>
internal static class NotYetReason
{
    public const string Apps = "Apps can't be opened or closed on Windows yet.";

    public const string LoginItem = "Opening at sign-in isn't available on Windows yet.";

    public const string Music = "Music can't be played on Windows yet.";

    public const string Browser = "Browsers can't be used on Windows yet.";
}
