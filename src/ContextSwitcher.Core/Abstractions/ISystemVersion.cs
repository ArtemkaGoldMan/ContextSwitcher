namespace ContextSwitcher.Core.Abstractions;

/// <summary>Which system, at which version, the app runs on - for the first line of a log and of a report.</summary>
public interface ISystemVersion
{
    /// <summary>The system's name as people know it: "macOS".</summary>
    string Name { get; }

    /// <summary>The version as the system's own settings show it: "26.6" on a Mac.</summary>
    string Read();
}
