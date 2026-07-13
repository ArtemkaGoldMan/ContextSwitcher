using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Registers global keyboard shortcuts that trigger context switches. Implemented in Phase 5
/// on top of <c>SharpHook</c>, which requires macOS Input Monitoring permission.
/// </summary>
public interface IHotkeyService
{
    /// <summary>
    /// Registers the enabled hotkeys, replacing any previously registered set.
    /// </summary>
    /// <param name="hotkeys">The hotkeys to register.</param>
    /// <param name="cancellationToken">A token that can cancel the registration.</param>
    Task RegisterAsync(IReadOnlyList<HotkeyConfig> hotkeys, CancellationToken cancellationToken);

    /// <summary>
    /// Unregisters all currently active hotkeys.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    Task UnregisterAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Raised when a registered hotkey combination is pressed, carrying the target context id.
    /// </summary>
    event EventHandler<string>? HotkeyPressed;
}
