using ContextSwitcher.Core.Applications;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>The apps running right now with a place in the Dock, other than ContextSwitcher and Finder.</summary>
public interface IRunningApplications
{
    /// <summary>Those apps, by name; empty where they can't be read.</summary>
    IReadOnlyList<RunningApp> List();
}
