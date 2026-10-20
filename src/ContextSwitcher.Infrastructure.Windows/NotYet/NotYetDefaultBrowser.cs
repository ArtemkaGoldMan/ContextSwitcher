using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Windows.NotYet;

/// <summary>Which browser is Windows' default is asked in stage 2, with the browser; until then it isn't known.</summary>
public sealed class NotYetDefaultBrowser : IDefaultBrowser
{
    /// <inheritdoc />
    public DefaultBrowser? Get() => null;
}
