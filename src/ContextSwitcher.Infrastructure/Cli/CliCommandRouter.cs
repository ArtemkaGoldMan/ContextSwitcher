using System.Text.Json;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Serialization;

namespace ContextSwitcher.Infrastructure.Cli;

/// <summary>
/// Routes CLI commands (<c>switch</c>, <c>status</c>, <c>list-contexts</c>, <c>validate-config</c>)
/// to the same services the app uses internally, per agent.md section 10. Human-readable output by
/// default; <c>--json</c> produces pure JSON with no extra text.
/// </summary>
public sealed class CliCommandRouter
{
    /// <summary>
    /// The one command that needs the running UI rather than running headlessly. Every other
    /// non-empty first argument - including unrecognized commands - routes through
    /// <see cref="RunAsync"/>, which reports invalid commands with exit code 2 instead of falling
    /// through to a full app launch.
    /// </summary>
    public const string OpenDashboardCommand = "open-dashboard";

    private readonly IContextSwitchService switchService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliCommandRouter"/> class.
    /// </summary>
    public CliCommandRouter(IContextSwitchService switchService)
    {
        ArgumentNullException.ThrowIfNull(switchService);
        this.switchService = switchService;
    }

    /// <summary>
    /// Returns whether <paramref name="command"/> should run headlessly and exit, per the startup
    /// sequence in section 5 ("route the command and exit without starting full UI"). Any command
    /// other than <see cref="OpenDashboardCommand"/> is headless, including unrecognized ones -
    /// those are rejected by <see cref="RunAsync"/> rather than silently launching the GUI.
    /// </summary>
    public static bool IsHeadlessCommand(string command) => command != OpenDashboardCommand;

    /// <summary>
    /// Runs the given command-line arguments against already-loaded startup state and returns the
    /// process exit code.
    /// </summary>
    public async Task<int> RunAsync(
        string[] args,
        AppConfiguration configuration,
        ConfigurationValidationResult configurationValidation,
        CurrentContextState state,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configurationValidation);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(output);

        if (args.Length == 0)
        {
            WriteUsage(output);
            return (int)CliExitCode.InvalidArguments;
        }

        bool json = args.Contains("--json");

        return args[0] switch
        {
            "switch" => await this.RunSwitchAsync(GetOptionValue(args, "--context"), configuration, configurationValidation, json, output, cancellationToken)
                .ConfigureAwait(false),
            "status" => RunStatus(state, json, output),
            "list-contexts" => RunListContexts(configuration, json, output),
            "validate-config" => RunValidateConfig(configurationValidation, json, output),
            _ => RunUnknownCommand(args[0], output)
        };
    }

    private async Task<int> RunSwitchAsync(
        string? contextId,
        AppConfiguration configuration,
        ConfigurationValidationResult configurationValidation,
        bool json,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contextId))
        {
            WriteError(output, json, "Missing required --context <id> argument.");
            return (int)CliExitCode.InvalidArguments;
        }

        if (!configurationValidation.IsValid)
        {
            WriteError(output, json, "Configuration is invalid. Run 'validate-config' for details.");
            return (int)CliExitCode.ConfigurationInvalid;
        }

        if (!configuration.Contexts.Any(context => context.Id == contextId))
        {
            WriteError(output, json, $"Unknown context '{contextId}'.");
            return (int)CliExitCode.UnknownContext;
        }

        ContextSwitchResult result = await this.switchService
            .SwitchAsync(new ContextSwitchRequest(contextId, ContextSwitchSource.Cli), cancellationToken)
            .ConfigureAwait(false);

        List<string> warnings = result.StepResults
            .Where(step => step.Status is AutomationResultStatus.Warning or AutomationResultStatus.Failed or AutomationResultStatus.TimedOut)
            .Select(step => step.Message)
            .ToList();

        if (json)
        {
            WriteJson(output, new { status = result.Status.ToString(), contextId = result.TargetContextId, warnings });
        }
        else
        {
            output.WriteLine($"Switch to '{result.TargetContextId}' finished with status: {result.Status}");
            foreach (string warning in warnings)
            {
                output.WriteLine($"  - {warning}");
            }
        }

        return result.Status switch
        {
            ContextSwitchStatus.Succeeded or ContextSwitchStatus.NoOp => (int)CliExitCode.Success,
            ContextSwitchStatus.SucceededWithWarnings => (int)CliExitCode.SucceededWithWarnings,
            ContextSwitchStatus.Cancelled => (int)CliExitCode.AnotherSwitchRunning,
            _ => (int)CliExitCode.GeneralFailure
        };
    }

    private static int RunStatus(CurrentContextState state, bool json, TextWriter output)
    {
        if (json)
        {
            WriteJson(
                output,
                new
                {
                    currentContextId = state.CurrentContextId,
                    previousContextId = state.PreviousContextId,
                    lastSwitchStatus = state.LastSwitchStatus,
                    lastSwitchCompletedAt = state.LastSwitchCompletedAt
                });
        }
        else
        {
            output.WriteLine($"Current context: {(string.IsNullOrEmpty(state.CurrentContextId) ? "(none)" : state.CurrentContextId)}");
            output.WriteLine($"Last switch status: {state.LastSwitchStatus}");
        }

        return (int)CliExitCode.Success;
    }

    private static int RunListContexts(AppConfiguration configuration, bool json, TextWriter output)
    {
        if (json)
        {
            WriteJson(
                output,
                new
                {
                    contexts = configuration.Contexts
                        .Select(context => new { id = context.Id, displayName = context.DisplayName })
                        .ToList()
                });
        }
        else
        {
            foreach (ContextDefinition context in configuration.Contexts)
            {
                output.WriteLine($"{context.Id} - {context.DisplayName}");
            }
        }

        return (int)CliExitCode.Success;
    }

    private static int RunValidateConfig(ConfigurationValidationResult validation, bool json, TextWriter output)
    {
        if (json)
        {
            WriteJson(
                output,
                new
                {
                    isValid = validation.IsValid,
                    errors = validation.Errors.Select(error => new { path = error.Path, message = error.Message }).ToList()
                });
        }
        else if (validation.IsValid)
        {
            output.WriteLine("Configuration is valid.");
        }
        else
        {
            output.WriteLine("Configuration is invalid:");
            foreach (ConfigurationValidationError error in validation.Errors)
            {
                output.WriteLine($"  - {error.Path}: {error.Message}");
            }
        }

        return validation.IsValid ? (int)CliExitCode.Success : (int)CliExitCode.ConfigurationInvalid;
    }

    private static int RunUnknownCommand(string command, TextWriter output)
    {
        WriteUsage(output);
        output.WriteLine();
        output.WriteLine($"Unknown command: '{command}'");
        return (int)CliExitCode.InvalidArguments;
    }

    private static void WriteUsage(TextWriter output)
    {
        output.WriteLine("Usage: ContextSwitcher <command> [options]");
        output.WriteLine();
        output.WriteLine("Commands:");
        output.WriteLine("  switch --context <id>   Switch to the given context");
        output.WriteLine("  status                  Show the current context and last switch outcome");
        output.WriteLine("  list-contexts           List configured contexts");
        output.WriteLine("  validate-config         Validate the local configuration");
        output.WriteLine("  open-dashboard          Open the dashboard window");
        output.WriteLine();
        output.WriteLine("Options:");
        output.WriteLine("  --json                  Emit machine-readable JSON instead of text");
    }

    private static void WriteError(TextWriter output, bool json, string message)
    {
        if (json)
        {
            WriteJson(output, new { error = message });
        }
        else
        {
            output.WriteLine($"Error: {message}");
        }
    }

    private static void WriteJson<T>(TextWriter output, T value)
    {
        output.WriteLine(JsonSerializer.Serialize(value, ContextSwitcherJson.Options));
    }

    private static string? GetOptionValue(string[] args, string optionName)
    {
        int index = Array.IndexOf(args, optionName);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
