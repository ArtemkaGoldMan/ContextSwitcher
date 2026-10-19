using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>A default browser that can't be found out, as on a computer that won't say.</summary>
public sealed class FakeUnknownDefaultBrowser : IDefaultBrowser
{
    public DefaultBrowser? Get() => null;
}
