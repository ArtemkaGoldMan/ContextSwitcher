using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Tests.TestDoubles;

public sealed class FakeHotkeyService : IHotkeyService
{
    /// <summary>One entry per <see cref="RegisterAsync"/> call, holding the set it was given.</summary>
    public List<IReadOnlyList<HotkeyConfig>> Registrations { get; } = [];

    public int UnregisterCallCount { get; private set; }

    /// <summary>When set, <see cref="RegisterAsync"/> throws it - the unobserved-exception path.</summary>
    public Exception? RegisterThrows { get; set; }

    /// <summary>The accelerators currently registered, in order.</summary>
    public IReadOnlyList<string> CurrentAccelerators =>
        this.Registrations.Count == 0 ? [] : this.Registrations[^1].Select(hotkey => hotkey.Accelerator).ToList();

    public event EventHandler<string>? HotkeyPressed;

    public Task RegisterAsync(IReadOnlyList<HotkeyConfig> hotkeys, CancellationToken cancellationToken)
    {
        if (this.RegisterThrows is not null)
        {
            throw this.RegisterThrows;
        }

        this.Registrations.Add(hotkeys);
        return Task.CompletedTask;
    }

    public Task UnregisterAllAsync(CancellationToken cancellationToken)
    {
        this.UnregisterCallCount++;
        return Task.CompletedTask;
    }

    public void RaiseHotkeyPressed(string contextId) => this.HotkeyPressed?.Invoke(this, contextId);
}
