namespace ContextSwitcher.Core.Contexts;

/// <summary>
/// Represents a single recorded error that occurred during a context switch.
/// </summary>
public sealed record StateError
{
    /// <summary>
    /// Gets or sets the identifier of the automation step that failed.
    /// </summary>
    public string StepId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable error message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the message's key, for the app to show it translated; null for one recorded before
    /// messages had keys, which is shown as <see cref="Message"/>.
    /// </summary>
    public string? MessageKey { get; init; }

    /// <summary>
    /// Gets or sets the values filled into the message, in order.
    /// </summary>
    public IReadOnlyList<string> MessageArguments { get; init; } = [];

    /// <summary>
    /// Gets or sets the time at which the error occurred.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }
}
