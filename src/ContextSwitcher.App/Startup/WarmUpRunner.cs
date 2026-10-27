using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;

namespace ContextSwitcher.App.Startup;

/// <summary>
/// Starts everything the platform asks to have read before it is needed (<see cref="IWarmUp"/>), in
/// the background, each on its own: one that fails is logged and the others still run.
/// </summary>
public sealed class WarmUpRunner(IEnumerable<IWarmUp> warmUps, ILogger logger) : IHousekeeping
{
    /// <inheritdoc />
    public void Start() => _ = Task.Run(this.RunAsync);

    /// <summary>Runs them all, and returns when each has finished or failed.</summary>
    public Task RunAsync() => Task.WhenAll(warmUps.Select(this.RunOneAsync));

    private async Task RunOneAsync(IWarmUp warmUp)
    {
        try
        {
            await warmUp.WarmUpAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Warn("App", "WarmUpFailed", $"{warmUp.GetType().Name} couldn't read what it was to have ready.", ex);
        }
    }
}
