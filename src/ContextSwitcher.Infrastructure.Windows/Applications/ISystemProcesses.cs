namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>The processes Windows is running, and ending one. Behind an interface so that what is done with them is tested without ending any.</summary>
public interface ISystemProcesses
{
    /// <summary>The id of this app's own process, which is never one of the apps it closes.</summary>
    int CurrentProcessId { get; }

    /// <summary>Every process running now; null when Windows can't be asked.</summary>
    IReadOnlyList<ProcessInfo>? List();

    /// <summary>Ends process <paramref name="pid"/> at once. Returns whether Windows did.</summary>
    bool Terminate(int pid);
}
