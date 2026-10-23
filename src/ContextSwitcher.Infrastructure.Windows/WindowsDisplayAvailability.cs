using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Windows;

/// <summary>
/// Windows lets an app start drawing with every display asleep - it is macOS that refuses - so there
/// is never anything to wait for, and no failure of that kind to start over from.
/// </summary>
public sealed class WindowsDisplayAvailability : IDisplayAvailability
{
    /// <inheritdoc />
    public TimeSpan WaitUntilAvailable() => TimeSpan.Zero;

    /// <inheritdoc />
    public bool IsRenderTimerFailure(Exception exception) => false;
}
