using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Logging;
using SharpHook;
using SharpHook.Data;
using SharpHook.Providers;
using LogLevel = ContextSwitcher.Core.Logging.LogLevel;

namespace ContextSwitcher.Infrastructure.Hotkeys;

/// <summary>
/// Registers global keyboard shortcuts using <c>SharpHook</c>'s global hook. Requires macOS
/// Accessibility permission (<see cref="UioHookProvider.IsAxApiEnabled"/>); the hook still starts
/// without it, it just never receives events, so permission is checked explicitly.
/// </summary>
public sealed class SharpHookHotkeyService : IHotkeyService, IDisposable
{
    private const EventMask ModifierMask = EventMask.Ctrl | EventMask.Alt | EventMask.Meta | EventMask.Shift;

    private readonly ILogger logger;
    private readonly IClock clock;
    private readonly Lock gate = new();
    private readonly List<HotkeyBinding> bindings = [];
    private IGlobalHook? hook;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharpHookHotkeyService"/> class.
    /// </summary>
    public SharpHookHotkeyService(ILogger logger, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);

        this.logger = logger;
        this.clock = clock;
    }

    /// <inheritdoc />
    public event EventHandler<string>? HotkeyPressed;

    /// <inheritdoc />
    public async Task RegisterAsync(IReadOnlyList<HotkeyConfig> hotkeys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hotkeys);

        await this.UnregisterAllAsync(cancellationToken).ConfigureAwait(false);

        if (!UioHookProvider.Instance.IsAxApiEnabled(promptUserIfDisabled: false))
        {
            await this.LogAsync(
                LogLevel.Warning,
                "HotkeyPermissionMissing",
                "macOS Accessibility permission is required for global hotkeys. Grant it in " +
                "System Settings -> Privacy & Security -> Accessibility, then restart ContextSwitcher.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        List<HotkeyBinding> parsed = [];
        HashSet<(EventMask Modifiers, KeyCode Key)> seen = [];

        foreach (HotkeyConfig hotkey in hotkeys.Where(hotkey => hotkey.Enabled))
        {
            if (!AcceleratorParser.TryParse(hotkey.Accelerator, out EventMask modifiers, out KeyCode key))
            {
                await this.LogAsync(
                    LogLevel.Warning, "HotkeyParseFailed",
                    $"Could not parse accelerator '{hotkey.Accelerator}' for hotkey '{hotkey.Id}'.",
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!seen.Add((modifiers, key)))
            {
                await this.LogAsync(
                    LogLevel.Warning, "HotkeyConflict",
                    $"Hotkey '{hotkey.Id}' ('{hotkey.Accelerator}') conflicts with another registered hotkey and was skipped.",
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            parsed.Add(new HotkeyBinding(modifiers, key, hotkey.ContextId));
        }

        lock (this.gate)
        {
            this.bindings.Clear();
            this.bindings.AddRange(parsed);
        }

        SimpleGlobalHook newHook = new();
        newHook.KeyPressed += this.OnKeyPressed;
        this.hook = newHook;

        _ = this.ObserveRunTaskAsync(newHook.RunAsync());
    }

    /// <inheritdoc />
    public Task UnregisterAllAsync(CancellationToken cancellationToken)
    {
        if (this.hook is { } activeHook)
        {
            activeHook.KeyPressed -= this.OnKeyPressed;
            activeHook.Stop();
            activeHook.Dispose();
            this.hook = null;
        }

        lock (this.gate)
        {
            this.bindings.Clear();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        UnregisterAllAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        EventMask activeModifiers = e.RawEvent.Mask & ModifierMask;
        KeyCode key = e.Data.KeyCode;

        string? contextId;
        lock (this.gate)
        {
            contextId = this.bindings
                .FirstOrDefault(binding => binding.Modifiers == activeModifiers && binding.Key == key)
                ?.ContextId;
        }

        if (contextId is not null)
        {
            this.HotkeyPressed?.Invoke(this, contextId);
        }
    }

    private async Task ObserveRunTaskAsync(Task runTask)
    {
        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (HookException ex)
        {
            await this.LogAsync(
                LogLevel.Error, "HotkeyHookFailed", $"Global hotkey hook stopped unexpectedly: {ex.Message}",
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task LogAsync(LogLevel level, string eventId, string message, CancellationToken cancellationToken)
    {
        await this.logger.LogAsync(
            new LogEntry
            {
                Timestamp = this.clock.UtcNow,
                Level = level,
                Category = "Hotkeys",
                EventId = eventId,
                Message = message
            },
            cancellationToken).ConfigureAwait(false);
    }

    private sealed record HotkeyBinding(EventMask Modifiers, KeyCode Key, string ContextId);
}
