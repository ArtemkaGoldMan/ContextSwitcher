namespace ContextSwitcher.Core.Security;

/// <summary>
/// The executables ContextSwitcher may run, which differ by platform: each platform's registrations
/// choose its own. <see cref="Abstractions.IProcessRunner"/> implementations must reject anything
/// outside it; a request to run something else indicates a bug, not an expected automation failure.
/// </summary>
public sealed class CommandAllowlist
{
    private readonly HashSet<string> allowed;

    /// <summary>An allowlist of exactly <paramref name="executables"/>, matched by exact name.</summary>
    public CommandAllowlist(IEnumerable<string> executables)
    {
        ArgumentNullException.ThrowIfNull(executables);

        this.allowed = new HashSet<string>(executables, StringComparer.Ordinal);
    }

    /// <summary>Whether <paramref name="fileName"/> may run.</summary>
    public bool IsAllowed(string fileName) => this.allowed.Contains(fileName);
}
