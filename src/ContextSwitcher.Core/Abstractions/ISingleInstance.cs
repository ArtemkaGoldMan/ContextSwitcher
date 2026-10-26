namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Keeps the app to one running copy. Opening it again while it runs shows the running copy instead,
/// as macOS does by itself - it brings the running app forward with a Reopen.
/// </summary>
public interface ISingleInstance
{
    /// <summary>
    /// Makes this the copy that runs, and returns true. With another copy already running, hands it
    /// <paramref name="arguments"/> - so it shows its window - and returns false, for this one to end.
    /// </summary>
    bool TryClaim(IReadOnlyList<string> arguments);

    /// <summary>Starts hearing from copies opened later, each raising <see cref="Reopened"/>. Called once.</summary>
    void Listen();

    /// <summary>Another copy was opened, with these arguments. Raised on a thread of its own.</summary>
    event EventHandler<IReadOnlyList<string>>? Reopened;
}
