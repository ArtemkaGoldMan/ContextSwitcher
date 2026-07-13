namespace ContextSwitcher.Infrastructure.Cli;

/// <summary>
/// Process exit codes for the CLI, per agent.md section 10.
/// </summary>
public enum CliExitCode
{
    Success = 0,
    GeneralFailure = 1,
    InvalidArguments = 2,
    UnknownContext = 3,
    ConfigurationInvalid = 4,
    SucceededWithWarnings = 5,
    AnotherSwitchRunning = 6
}
