namespace ContextSwitcher.Core.Abstractions;

/// <summary>The version of the system the app runs on - for the first line of a log and of a report.</summary>
public interface ISystemVersion
{
    /// <summary>The version as the system's own settings show it: "26.6" on a Mac.</summary>
    string Read();
}
