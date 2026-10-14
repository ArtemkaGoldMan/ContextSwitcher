using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Applications;

namespace ContextSwitcher.Tests.TestDoubles;

public sealed class FakeRunningApplications : IRunningApplications
{
    public List<RunningApp> Apps { get; } = [];

    public IReadOnlyList<RunningApp> List() => this.Apps.ToList();
}
