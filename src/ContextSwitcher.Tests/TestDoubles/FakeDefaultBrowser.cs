using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>A default browser chosen by the test.</summary>
public sealed class FakeDefaultBrowser(BrowserKind kind, string name) : IDefaultBrowser
{
    public DefaultBrowser? Get() => new(kind, name);
}
