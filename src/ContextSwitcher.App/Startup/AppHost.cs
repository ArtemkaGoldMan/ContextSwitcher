using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.Cli;
using Microsoft.Extensions.DependencyInjection;

namespace ContextSwitcher.App.Startup;

/// <summary>
/// Composition root: builds the container from <see cref="AppServices"/> and starts the app - the
/// start-up sequence (<see cref="AppStartup"/>) before the Avalonia UI thread starts, what runs while
/// it is up (<see cref="IHousekeeping"/>) once it has, and the end of the time session when it quits.
/// Building starts nothing, so <see cref="Program"/> can ask the system what it needs - whether a
/// display is awake - before anything begins.
/// </summary>
public static class AppHost
{
    /// <summary>
    /// Gets the root service provider. Only <see cref="Program"/> and Avalonia-instantiated
    /// types (which cannot receive constructor injection from <c>AppBuilder</c>) should read this.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Builds the service container. Must be called once, first; it starts nothing.</summary>
    public static void Build()
    {
        ServiceCollection services = new();
        AppServices.Register(services);
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        // For the places that cannot be handed a logger: a callback from macOS, a catch in a static helper.
        AppLog.Use(Services.GetRequiredService<ILogger>());
    }

    /// <summary>
    /// Runs the synchronous startup sequence. Must be called once, after <see cref="Build"/> and
    /// before the Avalonia application starts.
    /// </summary>
    public static StartupOutcome Initialize(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // A headless CLI invocation is a one-shot process that exits immediately: it has no window to
        // show a switch's problems in, and starts none of what runs while the app is up.
        bool interactive = !CliCommandRouter.IsHeadless(args);
        StartupOutcome outcome = Services.GetRequiredService<AppStartup>().RunAsync(interactive).GetAwaiter().GetResult();
        if (interactive)
        {
            // Made now rather than when a window first asks: it counts switches from the moment the app started.
            Services.GetRequiredService<SwitchNoticeViewModel>();
        }

        return outcome;
    }

    /// <summary>
    /// Starts what runs while the app is up. It needs Avalonia's dispatcher, and so cannot run during
    /// <see cref="Initialize"/> - that happens before the platform is set up, and merely constructing
    /// a DispatcherTimer there claims the UI dispatcher for the calling thread, which made Avalonia's
    /// own initialization fail with "the calling thread cannot access this object". Called from
    /// OnFrameworkInitializationCompleted instead.
    /// </summary>
    public static void StartRuntimeServices()
    {
        if (Services is null)
        {
            return;
        }

        foreach (IHousekeeping housekeeping in Services.GetServices<IHousekeeping>())
        {
            housekeeping.Start();
        }
    }

    /// <summary>
    /// Ends the current analytics session on app shutdown. Safe to call even if none was ever
    /// started (headless CLI runs never call this).
    /// </summary>
    public static async Task ShutdownAsync()
    {
        Services.GetRequiredService<StateFileWatcher>().Dispose();

        IAnalyticsService analyticsService = Services.GetRequiredService<IAnalyticsService>();
        await analyticsService.EndCurrentSessionAsync(SessionEndReason.AppShutdown, CancellationToken.None)
            .ConfigureAwait(false);
    }
}
