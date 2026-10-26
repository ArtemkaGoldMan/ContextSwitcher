using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Windows.NotYet;

/// <summary>Quitting apps on Windows comes with the apps part of stage 1; until then none is asked to.</summary>
public sealed class NotYetAppQuitter : IAppQuitter
{
    /// <inheritdoc />
    public Task<string> QuitAsync(string app, TimeSpan timeout, CancellationToken cancellationToken) => Task.FromResult(NotYetReason.Apps);

    /// <inheritdoc />
    public Task<bool?> IsRunningAsync(string app, TimeSpan timeout, CancellationToken cancellationToken) => Task.FromResult<bool?>(null);
}
