namespace ContextSwitcher.Core.ProcessExecution;

/// <summary>
/// The structured outcome of running an external process through <see cref="Abstractions.IProcessRunner"/>.
/// A non-zero <paramref name="ExitCode"/> is a normal, expected outcome and never throws.
/// </summary>
public sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut);
