namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Configures analytics collection and retention for local context sessions.
/// </summary>
public sealed record AnalyticsConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether analytics are enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets or sets the number of days to retain analytics data.
    /// </summary>
    public int RetentionDays { get; init; } = 365;
}
