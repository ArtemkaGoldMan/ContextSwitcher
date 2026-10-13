using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Infrastructure.Files;

namespace ContextSwitcher.Infrastructure.Browser;

/// <summary>
/// Keeps <see cref="OpenedTabs"/> in <c>opened-tabs.json</c>, beside <c>state.json</c>: a profile is
/// often left by a later run of the app than the one that entered it, or by the CLI.
/// </summary>
public sealed class OpenedTabsStore : IOpenedTabsStore
{
    private readonly IJsonStore jsonStore;
    private readonly string path;

    public OpenedTabsStore(IJsonStore jsonStore, ConfigPaths configPaths)
    {
        ArgumentNullException.ThrowIfNull(jsonStore);
        ArgumentNullException.ThrowIfNull(configPaths);

        this.jsonStore = jsonStore;
        this.path = configPaths.OpenedTabsPath;
    }

    /// <inheritdoc />
    public async Task<OpenedTabs?> GetAsync(string contextId, CancellationToken cancellationToken)
    {
        Dictionary<string, OpenedTabs> all = await this.ReadAsync(cancellationToken).ConfigureAwait(false);
        return all.GetValueOrDefault(contextId);
    }

    /// <inheritdoc />
    public async Task SetAsync(string contextId, OpenedTabs tabs, CancellationToken cancellationToken)
    {
        Dictionary<string, OpenedTabs> all = await this.ReadAsync(cancellationToken).ConfigureAwait(false);
        all[contextId] = tabs;
        await this.jsonStore.WriteAsync(this.path, all, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string contextId, CancellationToken cancellationToken)
    {
        Dictionary<string, OpenedTabs> all = await this.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (all.Remove(contextId))
        {
            await this.jsonStore.WriteAsync(this.path, all, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Dictionary<string, OpenedTabs>> ReadAsync(CancellationToken cancellationToken) =>
        await this.jsonStore.ReadAsync<Dictionary<string, OpenedTabs>>(this.path, cancellationToken).ConfigureAwait(false)
        ?? new Dictionary<string, OpenedTabs>(StringComparer.Ordinal);
}
