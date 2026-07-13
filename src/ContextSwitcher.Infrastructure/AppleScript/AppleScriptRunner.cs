using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Infrastructure.AppleScript;

/// <summary>
/// Runs AppleScript source through <c>osascript</c> via <see cref="IProcessRunner"/>.
/// </summary>
public sealed class AppleScriptRunner : IScriptRunner
{
    private readonly IProcessRunner processRunner;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppleScriptRunner"/> class.
    /// </summary>
    public AppleScriptRunner(IProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        this.processRunner = processRunner;
    }

    /// <inheritdoc />
    public Task<ProcessResult> RunAsync(string script, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);

        ProcessStartOptions options = new("osascript", ["-e", script], timeout);
        return this.processRunner.RunAsync(options, cancellationToken);
    }
}
