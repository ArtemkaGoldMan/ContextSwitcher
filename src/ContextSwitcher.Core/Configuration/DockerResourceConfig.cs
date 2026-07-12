namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Declares Docker containers that should be started or stopped for a context.
/// </summary>
public sealed record DockerResourceConfig
{
    /// <summary>
    /// Gets or sets the containers that should be started when entering the context.
    /// </summary>
    public IReadOnlyList<string> Start { get; init; } = [];

    /// <summary>
    /// Gets or sets the containers that should be stopped when leaving the context.
    /// </summary>
    public IReadOnlyList<string> Stop { get; init; } = [];
}
