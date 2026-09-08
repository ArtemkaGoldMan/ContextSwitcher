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
    private readonly string settingsPath;
    private readonly string statePath;
    private readonly SemaphoreSlim switchLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextSwitchService"/> class.
    /// </summary>
    public ContextSwitchService(
        IJsonStore jsonStore,
        IAutomationStepExecutor executor,
        AutomationPlanBuilder planBuilder,
        IAnalyticsService analyticsService,
        IClock clock,
        ILogger logger,
        string settingsPath,
        string statePath)
    {
        ArgumentNullException.ThrowIfNull(jsonStore);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(planBuilder);
        ArgumentNullException.ThrowIfNull(analyticsService);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);

        this.jsonStore = jsonStore;
        this.executor = executor;
        this.planBuilder = planBuilder;
        this.analyticsService = analyticsService;
        this.clock = clock;
        this.logger = logger;
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
        // writes, with neither one rejected. The file lock makes "one switch at a time" true across
        // processes; the OS releases it if a holder dies, so a crash cannot wedge it.
        FileStream? processLock = this.TryAcquireCrossProcessLock();
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
    /// Takes an exclusive lock on a file beside <c>state.json</c>, or returns <see langword="null"/>
    /// when another process already holds it. <see cref="FileShare.None"/> is enforced with an
    /// advisory lock on Unix, which the kernel drops when the owning process exits.
    /// </summary>
    private FileStream? TryAcquireCrossProcessLock()
    {
        try
        {
            // A bare filename has an empty directory part, not a null one - Directory.CreateDirectory
            // throws on empty, so fall back to the working directory explicitly.
            string directory = Path.GetDirectoryName(this.statePath) is { Length: > 0 } parent
                ? parent
                : Directory.GetCurrentDirectory();

            Directory.CreateDirectory(directory);

            return new FileStream(
                Path.Combine(directory, $"{Path.GetFileName(this.statePath)}.lock"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (IOException)
        {
            // Held by another switch.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // Can't create the lock file at all - a read-only config directory, say. Serialising
            // across processes is a safeguard, not a precondition, so let the switch proceed
            // rather than making the app unusable.
            return NoLockNeeded();
        }
    }

    /// <summary>
    /// A disposable stand-in used when the lock file cannot be created, so the caller's
    /// <c>finally</c> stays uniform.
    /// </summary>
    private static FileStream NoLockNeeded() =>
        new(Path.Combine(Path.GetTempPath(), $"cs-switch-{Guid.NewGuid():N}.lock"), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 1, FileOptions.DeleteOnClose);

    private async Task<ContextSwitchResult> RunSwitchAsync(
        ContextSwitchRequest request,
        string correlationId,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        await LogAsync(LogLevel.Information, "SwitchStarted", $"Switching to '{request.TargetContextId}'.", request.TargetContextId, correlationId, cancellationToken)
            .ConfigureAwait(false);

        AppConfiguration? configuration = await this.jsonStore.ReadAsync<AppConfiguration>(this.settingsPath, cancellationToken)
            .ConfigureAwait(false);
        ContextDefinition? target = configuration?.Contexts.FirstOrDefault(context => context.Id == request.TargetContextId);

        if (configuration is null || target is null)
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

        if (previousContextId == target.Id && !request.Force)
        {
            await LogAsync(LogLevel.Information, "SwitchCompleted", $"Already in context '{target.Id}'.", target.Id, correlationId, cancellationToken)
                .ConfigureAwait(false);
            return new ContextSwitchResult(target.Id, previousContextId, ContextSwitchStatus.NoOp, [], startedAt, this.clock.UtcNow, correlationId);
        }

        ContextDefinition? previous = previousContextId is null
            ? null
            : configuration.Contexts.FirstOrDefault(context => context.Id == previousContextId);

        await this.analyticsService.EndCurrentSessionAsync(SessionEndReason.Switch, cancellationToken).ConfigureAwait(false);

        AutomationPlan plan = this.planBuilder.Build(previous, target);
        (List<AutomationResult> stepResults, bool criticalFailure) = await ExecutePlanAsync(plan, request.DryRun, target, correlationId, cancellationToken)
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
            $"Switch to '{target.Id}' finished with status {overallStatus}.",
            target.Id,
            correlationId,
            cancellationToken).ConfigureAwait(false);

        return new ContextSwitchResult(target.Id, previousContextId, overallStatus, stepResults, startedAt, completedAt, correlationId);
    }

    private async Task<(List<AutomationResult> StepResults, bool CriticalFailure)> ExecutePlanAsync(
        AutomationPlan plan,
        bool dryRun,
        ContextDefinition target,
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
                await this.LogStepAsync(dryRunResult, target.Id, correlationId, cancellationToken).ConfigureAwait(false);
                continue;
            }

            AutomationResult result = await ExecuteWithTimeoutAsync(step, cancellationToken).ConfigureAwait(false);
            stepResults.Add(result);
            await this.LogStepAsync(result, target.Id, correlationId, cancellationToken).ConfigureAwait(false);

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

            if (!target.SwitchPolicy.ContinueOnNonCriticalFailure)
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
            return new AutomationResult(
                step.Id, step.Type, AutomationResultStatus.TimedOut, $"Step timed out after {step.Timeout}.", null, null, null, startedAt, completedAt);
        }
    }

    private async Task<ContextSwitchStatus> PersistOutcomeAsync(
        ContextDefinition target,
        string? previousContextId,
        DateTimeOffset startedAt,
        ContextSwitchStatus overallStatus,
        IReadOnlyList<AutomationResult> stepResults,
        string correlationId,
        CancellationToken cancellationToken)
    {
        CurrentContextState newState = new()
        {
            CurrentContextId = target.Id,
            PreviousContextId = previousContextId,
            LastSwitchStartedAt = startedAt,
            LastSwitchCompletedAt = this.clock.UtcNow,
            LastSwitchStatus = overallStatus.ToString(),
            LastErrors = BuildStateErrors(stepResults)
        };

        try
        {
            await this.jsonStore.WriteAsync(this.statePath, newState, cancellationToken).ConfigureAwait(false);
            await this.analyticsService.StartSessionAsync(target.Id, cancellationToken).ConfigureAwait(false);
            return overallStatus;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await LogAsync(LogLevel.Error, "SwitchFailed", $"Failed to persist state: {ex.Message}", target.Id, correlationId, CancellationToken.None)
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
        return stepResults
            .Where(result => result.Status is AutomationResultStatus.Warning or AutomationResultStatus.Failed or AutomationResultStatus.TimedOut)
            .Select(result => new StateError
            {
                StepId = result.StepId,
                Message = result.Message,
                OccurredAt = result.CompletedAt
            })
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
        CancellationToken cancellationToken)
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
                CorrelationId = correlationId
            },
            cancellationToken).ConfigureAwait(false);
    }
}
