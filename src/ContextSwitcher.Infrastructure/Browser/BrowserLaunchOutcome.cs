namespace ContextSwitcher.Infrastructure.Browser;

/// <summary>
/// The outcome of a <see cref="BrowserLauncher"/> call. Empty <see cref="Warnings"/> means every
/// URL, tab group, or profile in the request was handled successfully.
/// </summary>
public sealed record BrowserLaunchOutcome(IReadOnlyList<string> Warnings)
{
    public static BrowserLaunchOutcome Empty { get; } = new([]);
}
