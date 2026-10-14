using ContextSwitcher.Core.Logging;
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
    public async Task<ProcessResult> RunAsync(string script, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);

        ProcessStartOptions options = new("osascript", ["-e", script], timeout);
        ProcessResult result = await this.processRunner.RunAsync(options, cancellationToken).ConfigureAwait(false);

        // The one failure every step that scripts an app would otherwise report in its own words, or
        // not at all: macOS saying no. Written once here, with whom it was said about.
        if (result.ExitCode != 0 && DeniedTarget(result.StandardError) is { } target)
        {
            AppLog.Warn("Permissions", "AutomationDenied", $"macOS did not let Context Switcher control {target}.", data: new Dictionary<string, string> { ["target"] = target });
        }

        return result;
    }

    /// <summary>The app macOS refused to let Context Switcher control, from osascript's "Not authorized to send Apple events to …", or null if it was not that.</summary>
    internal static string? DeniedTarget(string standardError)
    {
        if (!standardError.Contains("-1743", StringComparison.Ordinal) && !standardError.Contains("Not authorized", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(standardError, @"Apple events to (.+?)(?:\.|\s*\(|$)");
        return match.Success ? match.Groups[1].Value.Trim() : "an app";
    }
}
