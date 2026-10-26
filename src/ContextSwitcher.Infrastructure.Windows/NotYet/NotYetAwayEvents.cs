using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Analytics;

namespace ContextSwitcher.Infrastructure.Windows.NotYet;

/// <summary>
/// Hearing that Windows sleeps or locks its screen comes later in stage 1; until then time counts
/// through both.
/// </summary>
public sealed class NotYetAwayEvents : IAwayEvents
{
    /// <inheritdoc />
    public event EventHandler<AwayReason>? Away
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public event EventHandler<AwayReason>? Back
    {
        add { }
        remove { }
    }
}
