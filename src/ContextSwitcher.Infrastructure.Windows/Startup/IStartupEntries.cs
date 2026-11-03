namespace ContextSwitcher.Infrastructure.Windows.Startup;

/// <summary>What opens at sign-in, as Windows keeps it. Behind an interface so that opening at sign-in is tested without touching the registry.</summary>
public interface IStartupEntries
{
    /// <summary>The command Windows runs at sign-in for <paramref name="name"/>; null when there is none.</summary>
    string? Command(string name);

    /// <summary>Makes Windows run <paramref name="command"/> at sign-in, under <paramref name="name"/>.</summary>
    void SetCommand(string name, string command);

    /// <summary>Stops Windows running <paramref name="name"/> at sign-in. Nothing to stop is not a failure.</summary>
    void RemoveCommand(string name);

    /// <summary>The bytes Startup apps keeps for <paramref name="name"/>; null when it keeps none.</summary>
    byte[]? Approval(string name);

    /// <summary>Forgets what Startup apps kept for <paramref name="name"/>, so that it is no longer turned off there.</summary>
    void RemoveApproval(string name);
}
