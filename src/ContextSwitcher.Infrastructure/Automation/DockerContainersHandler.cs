using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Infrastructure.Automation;

/// <summary>
/// Starts or stops the Docker containers a profile lists, with the <c>docker</c> command, which is
/// the same on every platform. One is registered for each of the two step types.
/// </summary>
public sealed class DockerContainersHandler : IStepHandler
{
    private readonly IProcessRunner processRunner;
    private readonly IClock clock;
    private readonly string command;

    /// <param name="type"><see cref="AutomationStepType.StartDockerResources"/> or <see cref="AutomationStepType.StopDockerResources"/>.</param>
    public DockerContainersHandler(AutomationStepType type, IProcessRunner processRunner, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(clock);

        this.Type = type;
        this.command = type switch
        {
            AutomationStepType.StartDockerResources => "start",
            AutomationStepType.StopDockerResources => "stop",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
        this.processRunner = processRunner;
        this.clock = clock;
    }

    /// <inheritdoc />
    public AutomationStepType Type { get; }

    /// <inheritdoc />
    public async Task<AutomationResult> ExecuteAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);

        DateTimeOffset startedAt = this.clock.UtcNow;
        string[] containers = StepArguments.List(step.Arguments, "containers");

        List<string> arguments = [this.command, .. containers];
        ProcessResult result = await this.processRunner
            .RunAsync(new ProcessStartOptions("docker", arguments, StepResults.WithReportingHeadroom(step.Timeout)), cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset completedAt = this.clock.UtcNow;

        if (result.ExitCode == 0)
        {
            return StepResults.Succeeded(step, $"Docker containers {this.command}ed.", startedAt, completedAt);
        }

        MessageTemplate failure = this.Type == AutomationStepType.StopDockerResources ? SwitchMessages.CouldNotStopContainers : SwitchMessages.CouldNotStartContainers;
        return StepResults.WithProblem(step, failure.With(string.Join(", ", containers)), result, startedAt, completedAt);
    }
}
