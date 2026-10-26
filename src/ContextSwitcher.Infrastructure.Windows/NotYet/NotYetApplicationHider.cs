using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Windows.NotYet;

/// <summary>Minimising apps on Windows comes with the apps part of stage 1; until then none is.</summary>
public sealed class NotYetApplicationHider : IApplicationHider
{
    /// <inheritdoc />
    public int Hide(IReadOnlyList<string> apps) => 0;
}
