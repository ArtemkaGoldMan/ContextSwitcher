namespace ContextSwitcher.Core.Contexts;

/// <summary>
/// Represents the current runtime state of the active context and the most recent switch outcome.
/// </summary>
public sealed record CurrentContextState
{
    /// <summary>
    /// Gets or sets the schema version for the state document.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Gets or sets the identifier of the currently active context.
    /// </summary>
    public string CurrentContextId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the previous context identifier, if any.
    /// </summary>
    public string? PreviousContextId { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when the most recent switch started.
    /// </summary>
    public DateTimeOffset? LastSwitchStartedAt { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when the most recent switch completed.
    /// </summary>
    public DateTimeOffset? LastSwitchCompletedAt { get; init; }

    /// <summary>
    /// Gets or sets the status of the most recent switch.
    /// </summary>
    public string LastSwitchStatus { get; init; } = "Unknown";

    /// <summary>
    /// Gets or sets the most recent state errors associated with the last switch.
    /// </summary>
    public IReadOnlyList<StateError> LastErrors { get; init; } = [];
}
