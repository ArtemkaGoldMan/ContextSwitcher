using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// macOS keeps to one copy itself: opening the app again brings the running one forward and hands it
/// a Reopen, which the app answers by showing its window. Nothing is left to do here.
/// </summary>
public sealed class MacSingleInstance : ISingleInstance
{
    /// <inheritdoc />
    public event EventHandler<IReadOnlyList<string>>? Reopened
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public bool TryClaim(IReadOnlyList<string> arguments) => true;

    /// <inheritdoc />
    public void Listen()
    {
    }
}
