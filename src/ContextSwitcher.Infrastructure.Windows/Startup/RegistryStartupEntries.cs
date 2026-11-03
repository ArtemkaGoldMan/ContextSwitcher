using Microsoft.Win32;

namespace ContextSwitcher.Infrastructure.Windows.Startup;

/// <summary>The current user's <c>Run</c> and <c>StartupApproved\Run</c> keys, which need no administrator.</summary>
public sealed class RegistryStartupEntries : IStartupEntries
{
    private readonly string runKey;
    private readonly string approvedKey;

    public RegistryStartupEntries(LoginItemOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.runKey = options.RunKey;
        this.approvedKey = options.ApprovedKey;
    }

    /// <inheritdoc />
    public string? Command(string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(this.runKey);
        return key?.GetValue(name) as string;
    }

    /// <inheritdoc />
    public void SetCommand(string name, string command)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(this.runKey, writable: true);
        key.SetValue(name, command, RegistryValueKind.String);
    }

    /// <inheritdoc />
    public void RemoveCommand(string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(this.runKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    /// <inheritdoc />
    public byte[]? Approval(string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(this.approvedKey);
        return key?.GetValue(name) as byte[];
    }

    /// <inheritdoc />
    public void RemoveApproval(string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(this.approvedKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}
