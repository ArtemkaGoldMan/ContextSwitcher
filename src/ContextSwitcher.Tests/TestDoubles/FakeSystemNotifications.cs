using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>Records what would have been shown in Notification Center, and plays a click on one.</summary>
public sealed class FakeSystemNotifications : ISystemNotifications
{
    public event EventHandler<string>? Clicked;

    /// <summary>Each notification shown, in order.</summary>
    public List<(string Id, string Title, string Body)> Shown { get; } = [];

    /// <summary>Each id taken back out.</summary>
    public List<string> Removed { get; } = [];

    public void Show(string id, string title, string body) => this.Shown.Add((id, title, body));

    public void Remove(string id) => this.Removed.Add(id);

    public void Click(string id) => this.Clicked?.Invoke(this, id);
}
