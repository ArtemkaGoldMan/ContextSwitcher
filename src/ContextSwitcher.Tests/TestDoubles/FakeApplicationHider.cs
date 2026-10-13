using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>Records what would have been hidden.</summary>
public sealed class FakeApplicationHider : IApplicationHider
{
    public List<string> Hidden { get; } = [];

    public int Hide(IReadOnlyList<string> apps)
    {
        this.Hidden.AddRange(apps);
        return apps.Count;
    }
}
