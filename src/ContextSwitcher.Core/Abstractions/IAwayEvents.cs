using ContextSwitcher.Core.Analytics;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Hears from the system when nobody can be using the computer - it sleeps, its screen locks, or
/// another user's session comes to the front - and when that ends. Events arrive on the UI thread.
/// </summary>
public interface IAwayEvents
{
    /// <summary>Nobody can be using the computer, for the reason given.</summary>
    event EventHandler<AwayReason>? Away;

    /// <summary>That reason no longer holds.</summary>
    event EventHandler<AwayReason>? Back;
}
