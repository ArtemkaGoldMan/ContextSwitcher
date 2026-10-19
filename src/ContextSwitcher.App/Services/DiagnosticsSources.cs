using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Logging;

namespace ContextSwitcher.App.Services;

/// <summary>
/// Where Settings gets what a diagnostic report is made of: where the log is, its last lines, what is
/// only in memory, which app on which Mac, and the clock the report is dated by.
/// </summary>
public sealed record DiagnosticsSources(
    ConfigPaths Paths,
    Func<RunInfo> Run,
    Func<IReadOnlyList<string>> Log,
    Func<IReadOnlyList<LogEntry>> Pending,
    IClock Clock);
