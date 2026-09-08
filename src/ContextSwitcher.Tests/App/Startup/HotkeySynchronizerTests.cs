using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.Startup;

/// <summary>
/// Hotkeys used to be registered exactly once during startup, so editing an accelerator in Profile
/// Setup updated the file and the UI while the live hook kept firing the old key until the next
/// launch.
/// </summary>
public sealed class HotkeySynchronizerTests
{
    [Fact]
    public async Task ApplyAsyncRegistersTheConfiguredAccelerators()
    {
        FakeHotkeyService hotkeys = new();
        HotkeySynchronizer synchronizer = Create(hotkeys, out _);

        await synchronizer.ApplyAsync(ConfigurationWith("Cmd+Alt+Ctrl+W"), configurationIsValid: true, CancellationToken.None);

        Assert.Equal(["Cmd+Alt+Ctrl+W"], hotkeys.CurrentAccelerators);
    }

    [Fact]
    public async Task ApplyAsyncReRegistersWithTheNewAcceleratorWhenConfigurationChanges()
    {
        FakeHotkeyService hotkeys = new();
        HotkeySynchronizer synchronizer = Create(hotkeys, out _);

        await synchronizer.ApplyAsync(ConfigurationWith("Cmd+Alt+Ctrl+W"), configurationIsValid: true, CancellationToken.None);
        await synchronizer.ApplyAsync(ConfigurationWith("Cmd+Shift+J"), configurationIsValid: true, CancellationToken.None);

        Assert.Equal(2, hotkeys.Registrations.Count);
        Assert.Equal(["Cmd+Shift+J"], hotkeys.CurrentAccelerators);
    }

    [Fact]
    public async Task ApplyAsyncUnregistersEverythingWhenConfigurationIsInvalid()
    {
        FakeHotkeyService hotkeys = new();
        HotkeySynchronizer synchronizer = Create(hotkeys, out _);

        await synchronizer.ApplyAsync(ConfigurationWith("Cmd+Alt+Ctrl+W"), configurationIsValid: false, CancellationToken.None);

        Assert.Empty(hotkeys.Registrations);
        Assert.Equal(1, hotkeys.UnregisterCallCount);
    }

    /// <summary>
    /// This runs fire-and-forget from a configuration-changed notification, so a throw would become
    /// an unobserved task exception rather than a visible failure.
    /// </summary>
    [Fact]
    public async Task ApplyAsyncLogsInsteadOfThrowingWhenRegistrationFails()
    {
        FakeHotkeyService hotkeys = new() { RegisterThrows = new InvalidOperationException("hook is dead") };
        HotkeySynchronizer synchronizer = Create(hotkeys, out TestLogger logger);

        await synchronizer.ApplyAsync(ConfigurationWith("Cmd+Alt+Ctrl+W"), configurationIsValid: true, CancellationToken.None);

        LogEntry entry = Assert.Single(logger.Entries, e => e.EventId == "HotkeyRegistrationFailed");
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("hook is dead", entry.Message, StringComparison.Ordinal);
    }

    private static HotkeySynchronizer Create(FakeHotkeyService hotkeys, out TestLogger logger)
    {
        logger = new TestLogger();
        return new HotkeySynchronizer(hotkeys, logger, new FakeClock());
    }

    private static AppConfiguration ConfigurationWith(string accelerator)
    {
        return new AppConfiguration
        {
            ActiveContextId = "work",
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }],
            Hotkeys = [new HotkeyConfig { Id = "switch-work", ContextId = "work", Accelerator = accelerator, Enabled = true }]
        };
    }
}
