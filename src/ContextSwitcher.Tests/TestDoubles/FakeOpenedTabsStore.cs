using ContextSwitcher.Infrastructure.Browser;

namespace ContextSwitcher.Tests.TestDoubles;

public sealed class FakeOpenedTabsStore : IOpenedTabsStore
{
    public Dictionary<string, OpenedTabs> Tabs { get; } = new(StringComparer.Ordinal);

    public Task<OpenedTabs?> GetAsync(string contextId, CancellationToken cancellationToken) =>
        Task.FromResult(this.Tabs.GetValueOrDefault(contextId));

    public Task SetAsync(string contextId, OpenedTabs tabs, CancellationToken cancellationToken)
    {
        this.Tabs[contextId] = tabs;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string contextId, CancellationToken cancellationToken)
    {
        this.Tabs.Remove(contextId);
        return Task.CompletedTask;
    }
}

public sealed class FakeBrowserRuns : IBrowserRuns
{
    /// <summary>The run every browser reports; null for one that isn't running.</summary>
    public string? Run { get; set; } = "run-1";

    public string? Current(ContextSwitcher.Core.Configuration.BrowserKind browser) => this.Run;
}
