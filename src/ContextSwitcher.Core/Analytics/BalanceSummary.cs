namespace ContextSwitcher.Core.Analytics;

/// <summary>
/// Total time spent in each context on a given local calendar day.
/// </summary>
public sealed record BalanceSummary(DateOnly Date, IReadOnlyDictionary<string, long> SecondsByContextId);
