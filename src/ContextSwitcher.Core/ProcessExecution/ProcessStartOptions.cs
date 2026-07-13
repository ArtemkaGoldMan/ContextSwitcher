namespace ContextSwitcher.Core.ProcessExecution;

/// <summary>
/// Describes an external process to run through <see cref="Abstractions.IProcessRunner"/>.
/// </summary>
public sealed record ProcessStartOptions(
    string FileName,
    IReadOnlyList<string> Arguments,
    TimeSpan Timeout,
    IReadOnlyDictionary<string, string>? Environment = null,
    string? WorkingDirectory = null,
    bool CaptureOutput = true);
