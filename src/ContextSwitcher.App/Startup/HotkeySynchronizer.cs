using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Logging;

namespace ContextSwitcher.App.Startup;

/// <summary>
/// Keeps the live global hook in step with <c>settings.json</c>. Accelerators are ordinary
/// configuration, editable at any time from Profile Setup, but registration used to happen exactly
/// once during startup - so a saved binding and the key that actually fired diverged until the next
/// launch, with the UI confidently showing the new one.
/// </summary>
public sealed class HotkeySynchronizer
{
    private readonly IHotkeyService hotkeyService;
    private readonly ILogger logger;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="HotkeySynchronizer"/> class.
    /// </summary>
    public HotkeySynchronizer(IHotkeyService hotkeyService, ILogger logger, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(hotkeyService);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);

        this.hotkeyService = hotkeyService;
        this.logger = logger;
        this.clock = clock;
    }

    /// <summary>
    /// Applies <paramref name="configuration"/>'s accelerators to the global hook, replacing whatever
    /// was registered before. An invalid configuration unregisters everything instead of registering
    /// bindings that reference contexts which may not exist.
    /// </summary>
    /// <remarks>
    /// Never throws. This runs fire-and-forget from a configuration-changed notification, so an
    /// unhandled exception here would surface as an unobserved task exception rather than a normal
    /// failure; the hotkey service already logs its own expected problems, such as missing
    /// Accessibility permission.
    /// </remarks>
    public async Task ApplyAsync(AppConfiguration configuration, bool configurationIsValid, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            if (configurationIsValid)
            {
                await this.hotkeyService.RegisterAsync(configuration.Hotkeys, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await this.hotkeyService.UnregisterAllAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await this.logger.LogAsync(
                new LogEntry
                {
                    Timestamp = this.clock.UtcNow,
                    Level = LogLevel.Error,
                    Category = "Hotkeys",
                    EventId = "HotkeyRegistrationFailed",
                    Message = $"Unhandled error applying hotkeys: {ex.Message}"
                },
                CancellationToken.None).ConfigureAwait(false);
        }
    }
}
