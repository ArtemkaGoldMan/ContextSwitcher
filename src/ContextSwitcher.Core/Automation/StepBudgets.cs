namespace ContextSwitcher.Core.Automation;

/// <summary>
/// How long each step of a switch may take, as the plan gives it; the step's handler shares it out.
/// Data, chosen with the platform's services - <see cref="Default"/> is what the app has always used.
/// </summary>
public sealed record StepBudgets
{
    public static StepBudgets Default { get; } = new();

    /// <summary>For each app asked to quit: section 9.1's ten seconds, most of it spent seeing it go.</summary>
    public TimeSpan QuitPerApp { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>For each app opened.</summary>
    public TimeSpan LaunchPerApp { get; init; } = TimeSpan.FromSeconds(15);

    public TimeSpan HideApps { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Opening a profile's pages or browser profiles.</summary>
    public TimeSpan BrowserContext { get; init; } = TimeSpan.FromSeconds(20);

    public TimeSpan CloseTabs { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan Focus { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan Media { get; init; } = TimeSpan.FromSeconds(8);

    /// <summary>Starting or stopping containers, which can wait on Docker itself starting.</summary>
    public TimeSpan Docker { get; init; } = TimeSpan.FromSeconds(45);
}
