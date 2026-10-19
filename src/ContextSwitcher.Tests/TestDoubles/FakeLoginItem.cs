using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>Opening at login as macOS would keep it, with a refusal or an approval wait to play.</summary>
public sealed class FakeLoginItem : ILoginItem
{
    public LoginItemStatus Status { get; set; } = LoginItemStatus.Off;

    /// <summary>A copy that isn't the installed app, so has no login item to offer - as when run from source.</summary>
    public static FakeLoginItem Unavailable() => new() { Status = LoginItemStatus.Unavailable };

    /// <summary>Returned instead of success, as macOS's reason, when set.</summary>
    public string? Refusal { get; set; }

    /// <summary>Turning on leaves it waiting for approval, as macOS sometimes does.</summary>
    public bool NeedsApproval { get; set; }

    public List<bool> Changes { get; } = [];

    public int SettingsOpened { get; private set; }

    public string? SetEnabled(bool enabled)
    {
        this.Changes.Add(enabled);
        if (this.Refusal is not null)
        {
            return this.Refusal;
        }

        this.Status = !enabled ? LoginItemStatus.Off : this.NeedsApproval ? LoginItemStatus.NeedsApproval : LoginItemStatus.On;
        return null;
    }

    public void OpenSystemSettings() => this.SettingsOpened++;
}
