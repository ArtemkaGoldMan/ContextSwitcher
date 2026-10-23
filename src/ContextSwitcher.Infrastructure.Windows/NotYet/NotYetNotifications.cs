using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Windows.NotYet;

/// <summary>Windows' notifications come in stage 3; until then none is shown.</summary>
public sealed class NotYetNotifications : ISystemNotifications
{
    /// <inheritdoc />
    public event EventHandler<string>? Clicked
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public void Show(string id, string title, string body)
    {
    }

    /// <inheritdoc />
    public void Remove(string id)
    {
    }
}
