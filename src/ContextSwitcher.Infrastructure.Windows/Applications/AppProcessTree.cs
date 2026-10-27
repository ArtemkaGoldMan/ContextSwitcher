namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>Which of the running processes are an app's: those that are its own, and those it started.</summary>
public static class AppProcessTree
{
    /// <summary>The processes of <paramref name="identity"/>, other than this app's own.</summary>
    public static List<ProcessInfo> Of(AppIdentity identity, IReadOnlyList<ProcessInfo> all, int self) =>
        all.Where(process => process.Pid != self && identity.Matches(process)).ToList();

    /// <summary>
    /// <paramref name="roots"/> and every process they started, and those started, and so on. Not this
    /// app's own process, nor anything it started, even when an app it closes started it: ending the
    /// app that was asked to switch is never what a switch means. A process id that comes round again
    /// can't make it loop, as each is visited once.
    /// </summary>
    public static HashSet<int> WithChildren(IEnumerable<ProcessInfo> roots, IReadOnlyList<ProcessInfo> all, int self)
    {
        Dictionary<int, ProcessInfo> byPid = [];
        foreach (ProcessInfo process in all)
        {
            byPid[process.Pid] = process;
        }

        HashSet<int> found = [.. roots.Select(process => process.Pid)];
        Queue<int> parents = new(found);
        while (parents.Count > 0)
        {
            int parent = parents.Dequeue();
            // Each process is added once, so one whose id comes round again can't make this loop.
            IEnumerable<int> children = all
                .Where(child => child.ParentPid == parent && child.Pid != self && child.Pid != parent
                    && StartedAfter(child, byPid.GetValueOrDefault(parent)))
                .Select(child => child.Pid)
                .Where(pid => found.Add(pid));
            foreach (int child in children)
            {
                parents.Enqueue(child);
            }
        }

        return found;
    }

    /// <summary>
    /// Whether <paramref name="child"/> started after <paramref name="parent"/>, as one it started
    /// did. Where either time is unknown it is not taken for one: leaving a process out is safe, and
    /// ending an unrelated one is not.
    /// </summary>
    private static bool StartedAfter(ProcessInfo child, ProcessInfo? parent) =>
        child.StartedAt is { } childStart && parent?.StartedAt is { } parentStart && childStart >= parentStart;
}
