using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Windows.NotYet;

/// <summary>Opening at sign-in on Windows comes later in stage 1; until then Settings doesn't offer it.</summary>
public sealed class NotYetLoginItem : ILoginItem
{
    /// <inheritdoc />
    public LoginItemStatus Status => LoginItemStatus.Unavailable;

    /// <inheritdoc />
    public string? SetEnabled(bool enabled) => NotYetReason.LoginItem;

    /// <inheritdoc />
    public void OpenSystemSettings()
    {
    }
}
