using ContextSwitcher.Core.Messages;
using System.Globalization;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Logging;

namespace ContextSwitcher.Core.Contexts;

/// <summary>
/// Orchestrates the context switch pipeline described in agent.md section 8: validate, lock,
/// close the previous analytics session, run the automation plan, persist state, start the next
/// analytics session, and report a structured result.
/// </summary>
public sealed class ContextSwitchService : IContextSwitchService
{
    /// <summary>Cap on logged stdout/stderr, so one chatty step cannot bloat app.log.jsonl.</summary>
    private const int MaxLoggedOutputLength = 500;

    private readonly IJsonStore jsonStore;
    private readonly IAutomationStepExecutor executor;
    private readonly AutomationPlanBuilder planBuilder;
    private readonly IAnalyticsService analyticsService;
    private readonly IClock clock;
    private readonly ILogger logger;
    private readonly ISwitchLock crossProcessLock;
    private readonly string settingsPath;
    private readonly string statePath;
    private readonly SemaphoreSlim switchLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextSwitchService"/> class.
    /// </summary>
    /// <param name="crossProcessLock">Keeps another process from switching at the same time.</param>
    public ContextSwitchService(
        IJsonStore jsonStore,
        IAutomationStepExecutor executor,
        AutomationPlanBuilder planBuilder,
        IAnalyticsService analyticsService,
        IClock clock,
        ILogger logger,
        ISwitchLock crossProcessLock,
        string settingsPath,
        string statePath)
    {
        ArgumentNullException.ThrowIfNull(jsonStore);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(planBuilder);
        ArgumentNullException.ThrowIfNull(analyticsService);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(crossProcessLock);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);

        this.jsonStore = jsonStore;
        this.executor = executor;
        this.planBuilder = planBuilder;
        this.analyticsService = analyticsService;
        this.clock = clock;
        this.logger = logger;
        this.crossProcessLock = crossProcessLock;
        this.settingsPath = settingsPath;
        this.statePath = statePath;
    }

    /// <inheritdoc />
    public async Task<ContextSwitchResult> SwitchAsync(ContextSwitchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string correlationId = request.CorrelationId ?? Guid.CreateVersion7().ToString();
        DateTimeOffset startedAt = this.clock.UtcNow;

        bool lockAcquired = await this.switchLock.WaitAsync(0, cancellationToken).ConfigureAwait(false);
        if (!lockAcquired)
        {
            await LogAsync(LogLevel.Warning, "SwitchRejected", "Another switch is already running.", request.TargetContextId, correlationId, cancellationToken)
                .ConfigureAwait(false);
            return new ContextSwitchResult(request.TargetContextId, null, ContextSwitchStatus.Cancelled, [], startedAt, this.clock.UtcNow, correlationId);
        }

        // The semaphore above only guards this process. Section 10 has macOS Shortcuts and Siri
        // triggering switches through the CLI, and every one of those is a separate process - two
        // fired together used to run their AppleScript concurrently and interleave their state
        // writes, with neither one rejected. The cross-process lock makes "one switch at a time" true
        // across processes; how it is held, and let go if a holder dies, is the platform's.
        IDisposable? processLock = this.crossProcessLock.TryAcquire();
        if (processLock is null)
        {
            this.switchLock.Release();
            await LogAsync(LogLevel.Warning, "SwitchRejected", "Another switch is already running.", request.TargetContextId, correlationId, cancellationToken)
                .ConfigureAwait(false);
            return new ContextSwitchResult(request.TargetContextId, null, ContextSwitchStatus.Cancelled, [], startedAt, this.clock.UtcNow, correlationId);
        }

        try
        {
            return await RunSwitchAsync(request, correlationId, startedAt, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await LogAsync(LogLevel.Warning, "SwitchCancelled", "Switch was cancelled.", request.TargetContextId, correlationId, CancellationToken.None)
                .ConfigureAwait(false);
            return new ContextSwitchResult(request.TargetContextId, null, ContextSwitchStatus.Cancelled, [], startedAt, this.clock.UtcNow, correlationId);
        }
        finally
        {
            processLock.Dispose();
            this.switchLock.Release();
        }
    }

    /// <summary>
    /// Changes the problems the last switch recorded - an app that was still closing has gone, say -
    /// as long as the switch that finished at <paramref name="completedAt"/> is still the last one and
    /// no other is running: it holds the same locks a switch does, so it never writes over a newer
    /// switch's state. With no problem left, a switch with warnings becomes one without.
    /// </summary>
    /// <returns>The state as written, or null when it was left as it was.</returns>
    public async Task<CurrentContextState?> AmendLastSwitchAsync(
        DateTimeOffset completedAt,
        Func<IReadOnlyList<StateError>, IReadOnlyList<StateError>> amend,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(amend);

        if (!await this.switchLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        IDisposable? processLock = this.crossProcessLock.TryAcquire();
        if (processLock is null)
        {
            this.switchLock.Release();
            return null;
        }

        try
        {
            CurrentContextState? state = await this.jsonStore.ReadAsync<CurrentContextState>(this.statePath, cancellationToken).ConfigureAwait(false);
            if (state is null || state.LastSwitchCompletedAt != completedAt)
            {
                return null;
            }

            IReadOnlyList<StateError> errors = amend(state.LastErrors);
            if (errors.SequenceEqual(state.LastErrors))
            {
                return null;
            }

            bool cleared = errors.Count == 0 && state.LastSwitchStatus == nameof(ContextSwitchStatus.SucceededWithWarnings);
            CurrentContextState amended = state with
            {
                LastErrors = errors,
                LastSwitchStatus = cleared ? nameof(ContextSwitchStatus.Succeeded) : state.LastSwitchStatus
            };
            await this.jsonStore.WriteAsync(this.statePath, amended, cancellationToken).ConfigureAwait(false);
            return amended;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.logger.Warn("ContextSwitch", "StateNotAmended", "Could not update the last switch's warnings.", ex);
            return null;
        }
        finally
        {
            processLock.Dispose();
            this.switchLock.Release();
        }
    }

    private async Task<ContextSwitchResult> RunSwitchAsync(
        ContextSwitchRequest request,
        string correlationId,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        await LogAsync(
            LogLevel.Information,
            "SwitchStarted",
            request.IsTurnOff ? "Turning the active context off." : $"Switching to '{request.TargetContextId}'.",
            request.TargetContextId,
            correlationId,
            cancellationToken,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["source"] = request.Source.ToString(),
                ["dryRun"] = request.DryRun.ToString(),
                ["force"] = request.Force.ToString()
            }).ConfigureAwait(false);

        AppConfiguration? configuration = await this.jsonStore.ReadAsync<AppConfiguration>(this.settingsPath, cancellationToken)
            .ConfigureAwait(false);

        // Null when turning off: nothing is entered.
        ContextDefinition? target = configuration?.Contexts.FirstOrDefault(context => context.Id == request.TargetContextId);

        if (configuration is null || (target is null && !request.IsTurnOff))
        {
            string message = configuration is null
                ? "Configuration could not be loaded."
                : $"Unknown context '{request.TargetContextId}'.";

            await LogAsync(LogLevel.Error, "SwitchFailed", message, request.TargetContextId, correlationId, cancellationToken).ConfigureAwait(false);
            return new ContextSwitchResult(request.TargetContextId, null, ContextSwitchStatus.Failed, [], startedAt, this.clock.UtcNow, correlationId);
        }

        CurrentContextState currentState = await this.jsonStore.ReadAsync<CurrentContextState>(this.statePath, cancellationToken).ConfigureAwait(false)
            ?? new CurrentContextState();
        string? previousContextId = string.IsNullOrEmpty(currentState.CurrentContextId) ? null : currentState.CurrentContextId;

        if (target is not null && previousContextId == target.Id && !request.Force)
        {
            await LogAsync(LogLevel.Information, "SwitchCompleted", $"Already in context '{target.Id}'.", target.Id, correlationId, cancellationToken)
                .ConfigureAwait(false);
            return new ContextSwitchResult(target.Id, previousContextId, ContextSwitchStatus.NoOp, [], startedAt, this.clock.UtcNow, correlationId);
        }

        if (target is null && previousContextId is null)
        {
            await LogAsync(LogLevel.Information, "SwitchCompleted", "No context is active, so there is nothing to turn off.", string.Empty, correlationId, cancellationToken)
                .ConfigureAwait(false);
            return new ContextSwitchResult(string.Empty, null, ContextSwitchStatus.NoOp, [], startedAt, this.clock.UtcNow, correlationId);
        }

        ContextDefinition? previous = previousContextId is null
            ? null
            : configuration.Contexts.FirstOrDefault(context => context.Id == previousContextId);

        await this.analyticsService.EndCurrentSessionAsync(SessionEndReason.Switch, cancellationToken).ConfigureAwait(false);

        AutomationPlan plan = this.planBuilder.Build(previous, target, request.Extras, configuration.IgnoredApps);

        // The steps' log entries go under the profile they act for: the one entered, or the one
        // being turned off.
        string subjectId = target?.Id ?? previousContextId ?? string.Empty;
        SwitchPolicyConfig policy = (target ?? previous)?.SwitchPolicy ?? new SwitchPolicyConfig();

        // What was decided, so a log that ends in a problem shows what the switch meant to do as well
        // as what went wrong: from where to where, which steps, and what the user chose to add.
        await LogAsync(
            LogLevel.Information,
            "SwitchPlanned",
            plan.Steps.Count == 0 ? "Planned no steps." : $"Planned {plan.Steps.Count} steps: {string.Join(", ", plan.Steps.Select(step => step.Type))}.",
            subjectId,
            correlationId,
            cancellationToken,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["from"] = previousContextId ?? string.Empty,
                ["to"] = target?.Id ?? string.Empty,
                ["extraApps"] = (request.Extras?.CloseApps.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                ["extraTabs"] = (request.Extras?.CloseTabs.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                ["neverTouch"] = configuration.IgnoredApps.Count.ToString(CultureInfo.InvariantCulture)
            }).ConfigureAwait(false);

        (List<AutomationResult> stepResults, bool criticalFailure) = await ExecutePlanAsync(plan, request.DryRun, policy, subjectId, correlationId, cancellationToken)
            .ConfigureAwait(false);

        ContextSwitchStatus overallStatus = DetermineStatus(request.DryRun, criticalFailure, stepResults);

        if (!request.DryRun)
        {
            overallStatus = await PersistOutcomeAsync(target, previousContextId, startedAt, overallStatus, stepResults, correlationId, cancellationToken)
                .ConfigureAwait(false);
        }

        DateTimeOffset completedAt = this.clock.UtcNow;
        string eventId = overallStatus == ContextSwitchStatus.Failed ? "SwitchFailed" : "SwitchCompleted";
        await LogAsync(
            overallStatus == ContextSwitchStatus.Failed ? LogLevel.Error : LogLevel.Information,
            eventId,
            target is null
                ? $"Turning '{previousContextId}' off finished with status {overallStatus}."
                : $"Switch to '{target.Id}' finished with status {overallStatus}.",
            subjectId,
            correlationId,
            cancellationToken,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["status"] = overallStatus.ToString(),
                ["durationMs"] = ((long)(completedAt - startedAt).TotalMilliseconds).ToString(CultureInfo.InvariantCulture)
            }).ConfigureAwait(false);

        return new ContextSwitchResult(target?.Id ?? string.Empty, previousContextId, overallStatus, stepResults, startedAt, completedAt, correlationId);
    }

    private async Task<(List<AutomationResult> StepResults, bool CriticalFailure)> ExecutePlanAsync(
        AutomationPlan plan,
        bool dryRun,
        SwitchPolicyConfig policy,
        string subjectId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        List<AutomationResult> stepResults = [];
        bool criticalFailure = false;

        foreach (AutomationStep step in plan.Steps)
        {
            if (dryRun)
            {
                DateTimeOffset now = this.clock.UtcNow;
                AutomationResult dryRunResult = new(
                    step.Id, step.Type, AutomationResultStatus.Skipped, "Dry run: no changes applied.", null, null, null, now, now);
                stepResults.Add(dryRunResult);
                await this.LogStepAsync(dryRunResult, subjectId, correlationId, cancellationToken).ConfigureAwait(false);
                continue;
            }

            AutomationResult result = await ExecuteWithTimeoutAsync(step, cancellationToken).ConfigureAwait(false);
            stepResults.Add(result);
            await this.LogStepAsync(result, subjectId, correlationId, cancellationToken).ConfigureAwait(false);

            bool failed = result.Status is AutomationResultStatus.Failed or AutomationResultStatus.TimedOut;
            if (!failed)
            {
                continue;
            }

            if (step.IsCritical)
            {
                criticalFailure = true;
                break;
            }

            if (!policy.ContinueOnNonCriticalFailure)
            {
                break;
            }
        }

        return (stepResults, criticalFailure);
    }

    private async Task<AutomationResult> ExecuteWithTimeoutAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutCts = new(step.Timeout);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        DateTimeOffset startedAt = this.clock.UtcNow;
        try
        {
            return await this.executor.ExecuteAsync(step, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            DateTimeOffset completedAt = this.clock.UtcNow;
            UserMessage timedOut = SwitchMessages.TimedOut(step.Type, ((int)step.Timeout.TotalSeconds).ToString(CultureInfo.InvariantCulture));
            return new AutomationResult(step.Id, step.Type, AutomationResultStatus.TimedOut, timedOut.English, null, null, null, startedAt, completedAt)
            {
                Problems = [timedOut]
            };
        }
    }

    /// <summary>
    /// Records the outcome as the current state. With no <paramref name="target"/> - a profile
    /// turned off - no context is current, and no time is counted until one is switched to.
    /// </summary>
    private async Task<ContextSwitchStatus> PersistOutcomeAsync(
        ContextDefinition? target,
        string? previousContextId,
        DateTimeOffset startedAt,
        ContextSwitchStatus overallStatus,
        IReadOnlyList<AutomationResult> stepResults,
        string correlationId,
        CancellationToken cancellationToken)
    {
        CurrentContextState newState = new()
        {
            CurrentContextId = target?.Id ?? string.Empty,
            PreviousContextId = previousContextId,
            LastSwitchStartedAt = startedAt,
            LastSwitchCompletedAt = this.clock.UtcNow,
            LastSwitchStatus = overallStatus.ToString(),
            LastErrors = BuildStateErrors(stepResults)
        };

        try
        {
            await this.jsonStore.WriteAsync(this.statePath, newState, cancellationToken).ConfigureAwait(false);
            if (target is not null)
            {
                await this.analyticsService.StartSessionAsync(target.Id, cancellationToken).ConfigureAwait(false);
            }

            return overallStatus;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await LogAsync(LogLevel.Error, "SwitchFailed", $"Failed to persist state: {ex.Message}", target?.Id ?? string.Empty, correlationId, CancellationToken.None)
                .ConfigureAwait(false);
            return ContextSwitchStatus.Failed;
        }
    }

    private static ContextSwitchStatus DetermineStatus(bool dryRun, bool criticalFailure, IReadOnlyList<AutomationResult> stepResults)
    {
        if (dryRun)
        {
            return ContextSwitchStatus.Succeeded;
        }

        if (criticalFailure)
        {
            return ContextSwitchStatus.Failed;
        }

        bool hasWarnings = stepResults.Any(result =>
            result.Status is AutomationResultStatus.Warning or AutomationResultStatus.Failed or AutomationResultStatus.TimedOut);

        return hasWarnings ? ContextSwitchStatus.SucceededWithWarnings : ContextSwitchStatus.Succeeded;
    }

    private static IReadOnlyList<StateError> BuildStateErrors(IReadOnlyList<AutomationResult> stepResults)
    {
        // One entry per problem, each with its key so the app can show it translated. A step with no
        // listed problems keeps its single English message.
        return stepResults
            .Where(result => result.Status is AutomationResultStatus.Warning or AutomationResultStatus.Failed or AutomationResultStatus.TimedOut)
            .SelectMany(result => result.Problems.Count == 0
                ? [new StateError { StepId = result.StepId, Message = result.Message, OccurredAt = result.CompletedAt }]
                : result.Problems.Select(problem => new StateError
                {
                    StepId = result.StepId,
                    Message = problem.English,
                    MessageKey = problem.Key,
                    MessageArguments = problem.Arguments,
                    OccurredAt = result.CompletedAt
                }))
            .ToList();
    }

    /// <summary>
    /// Writes one log entry per executed step, carrying the step id, exit code and trimmed
    /// stdout/stderr that agent.md section 12 requires of the developer log. Without this a
    /// completed switch left no record of which steps ran or how they ended - only failures
    /// reached <c>state.json</c>, and the next switch overwrote those.
    /// </summary>
    private async Task LogStepAsync(
        AutomationResult result,
        string contextId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> data = new(StringComparer.Ordinal)
        {
            ["stepId"] = result.StepId,
            ["stepType"] = result.Type.ToString(),
            ["status"] = result.Status.ToString(),
            ["durationMs"] = ((long)(result.CompletedAt - result.StartedAt).TotalMilliseconds)
                .ToString(CultureInfo.InvariantCulture)
        };

        if (result.ExitCode is int exitCode)
        {
            data["exitCode"] = exitCode.ToString(CultureInfo.InvariantCulture);
        }

        AddIfPresent(data, "stdout", result.StandardOutput);
        AddIfPresent(data, "stderr", result.StandardError);

        LogLevel level = result.Status switch
        {
            AutomationResultStatus.Failed or AutomationResultStatus.TimedOut => LogLevel.Error,
            AutomationResultStatus.Warning => LogLevel.Warning,
            _ => LogLevel.Information
        };

        await this.logger.LogAsync(
            new LogEntry
            {
                Timestamp = this.clock.UtcNow,
                Level = level,
                Category = "ContextSwitch",
                EventId = "StepCompleted",
                Message = $"{result.Type} finished with status {result.Status}. {result.Message}".TrimEnd(),
                ContextId = contextId,
                CorrelationId = correlationId,
                Data = data
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds trimmed process output, capped so a chatty step cannot bloat the log file.
    /// </summary>
    private static void AddIfPresent(Dictionary<string, string> data, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string trimmed = value.Trim();
        data[key] = trimmed.Length > MaxLoggedOutputLength
            ? string.Concat(trimmed.AsSpan(0, MaxLoggedOutputLength), "…")
            : trimmed;
    }

    private async Task LogAsync(
        LogLevel level,
        string eventId,
        string message,
        string? contextId,
        string correlationId,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? data = null)
    {
        await this.logger.LogAsync(
            new LogEntry
            {
                Timestamp = this.clock.UtcNow,
                Level = level,
                Category = "ContextSwitch",
                EventId = eventId,
                Message = message,
                ContextId = contextId,
                CorrelationId = correlationId,
                Data = data
            },
            cancellationToken).ConfigureAwait(false);
    }
}
