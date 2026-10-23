using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Applications;

namespace ContextSwitcher.Infrastructure.Windows.NotYet;

/// <summary>
/// Listing the apps open on Windows comes with the apps part of stage 1; until then there are none to
/// ask about on a switch.
/// </summary>
public sealed class NotYetRunningApplications : IRunningApplications
{
    /// <inheritdoc />
    public IReadOnlyList<RunningApp> List() => [];
}
