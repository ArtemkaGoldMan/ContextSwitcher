using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>Opens apps with <c>open -a</c>, which finds an app by its name in Applications.</summary>
public sealed class MacAppLauncher : IAppLauncher
{
    private readonly IProcessRunner processRunner;

    public MacAppLauncher(IProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(processRunner);

        this.processRunner = processRunner;
    }

    /// <inheritdoc />
    public async Task<AppLaunch> LaunchAsync(string app, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ProcessResult result = await this.processRunner.RunAsync(new ProcessStartOptions("open", ["-a", app], timeout), cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode == 0)
        {
            return new AppLaunch(AppLaunchOutcome.Launched, result.StandardError);
        }

        // `open -a` says so when no installed app has that name.
        AppLaunchOutcome outcome = result.StandardError.Contains("Unable to find application", StringComparison.OrdinalIgnoreCase)
            ? AppLaunchOutcome.NotInstalled
            : AppLaunchOutcome.Failed;
        return new AppLaunch(outcome, result.StandardError);
    }
}
