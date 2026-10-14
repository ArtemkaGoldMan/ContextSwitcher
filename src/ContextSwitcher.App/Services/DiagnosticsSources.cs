using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Logging;

namespace ContextSwitcher.App.Services;

/// <summary>Where Settings gets what a diagnostic report is made of: the log file, what is only in memory, which app on which Mac.</summary>
public sealed record DiagnosticsSources(
    ConfigPaths Paths,
    Func<RunInfo> Run,
    Func<IReadOnlyList<LogEntry>> Pending,
    Func<DateTimeOffset> Now);
