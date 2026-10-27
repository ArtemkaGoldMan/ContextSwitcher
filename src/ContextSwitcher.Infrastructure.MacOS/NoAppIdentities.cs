using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>The Mac finds an app by its name alone, so there is nothing to write into the <c>apps</c> map.</summary>
public sealed class NoAppIdentities : IAppIdentityCatalog
{
    /// <inheritdoc />
    public Task<AppIdentity?> IdentifyAsync(string appName, CancellationToken cancellationToken) => Task.FromResult<AppIdentity?>(null);
}
