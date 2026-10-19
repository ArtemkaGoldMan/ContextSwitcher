using System.Diagnostics;
using System.Globalization;
using Avalonia;
using ContextSwitcher.App.Resources;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.Cli;
using Microsoft.Extensions.DependencyInjection;

namespace ContextSwitcher.App;

internal static class Program
{
    /// <summary>How many times a launch that could not start drawing starts itself over before giving up.</summary>
    private const int MaxStartAttempts = 3;

    private const string StartAttemptVariable = "CONTEXTSWITCHER_START_ATTEMPT";

    /// <summary>
    /// How much GPU memory Skia may keep cached for drawing. Its default is tens of megabytes, for an
    /// app whose whole interface is a few small, mostly still windows; opening the dashboard once left
    /// 28 MB of it behind for good, and 2 MB leaves 13 MB less with nothing to redraw any slower.
    /// </summary>
    private const long GpuCacheBytes = 2 * 1024 * 1024;

    /// <summary>
    /// Set to <c>software</c> to draw on the CPU rather than the GPU. That leaves no GPU memory behind
    /// at all - the dashboard then costs about 27 MB less than with Metal - and is off by default only
    /// because how it looks has not been judged on a real screen yet.
    /// </summary>
    internal const string RenderingVariable = "CONTEXTSWITCHER_RENDERING";

    [STAThread]
    public static void Main(string[] args)
    {
        // With every display asleep Avalonia cannot start drawing, and the app used to abort here with a
        // crash report. A window needs a screen anyway, so wait for one - before AppHost.Initialize,
        // which starts counting time: nothing has begun until there is somewhere to show it. The
        // command line never draws, so it never waits.
        AppHost.Build();
        IDisplayAvailability displays = AppHost.Services.GetRequiredService<IDisplayAvailability>();
        bool interactive = !CliCommandRouter.IsHeadless(args);
        TimeSpan waitedForDisplay = interactive ? displays.WaitUntilAvailable() : TimeSpan.Zero;

        StartupOutcome startup = AppHost.Initialize(args);
        ReportCrashes();
        if (waitedForDisplay > TimeSpan.Zero)
        {
            LogWaitedForDisplay(waitedForDisplay);
        }

        IAppState appState = AppHost.Services.GetRequiredService<IAppState>();
        if (CliCommandRouter.IsHeadless(args))
        {
            CliCommandRouter router = AppHost.Services.GetRequiredService<CliCommandRouter>();
            int exitCode = router.RunAsync(args, appState.Configuration, appState.ConfigurationValidation, appState.State, Console.Out, CancellationToken.None, startup.ConfigurationWasQuarantined)
                .GetAwaiter()
                .GetResult();
            Environment.Exit(exitCode);
            return;
        }

        // Before any window, so every text is read in it. The command line above stays English: scripts
        // read it.
        AppLanguages.Apply(AppLanguages.Resolve(appState.Configuration.Language, AppHost.Services.GetRequiredService<IPreferredLanguages>().Get()));

        try
        {
            BuildAvaloniaApp(appState.Configuration.ShowDockIcon)
                .StartWithClassicDesktopLifetime(args);
        }
        catch (InvalidOperationException ex) when (displays.IsRenderTimerFailure(ex) && StartAttempt() < MaxStartAttempts)
        {
            // The screen went dark between the look above and Avalonia's own: Avalonia cannot be set up
            // a second time in this process, so a fresh one takes over once a display is awake.
            StartOver(args, displays);
        }
    }

    /// <summary>
    /// A crash leaves only macOS's own report, which says where the app stopped and nothing of what it
    /// was doing. These write the trail that led to it first - the exception on any thread, and a
    /// background task that failed with nobody waiting - before the process goes.
    /// </summary>
    private static void ReportCrashes()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLog.Critical(
            "Crash",
            "UnhandledException",
            e.IsTerminating ? "The app crashed." : "An exception nothing handled.",
            e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) => AppLog.Error(
            "Crash",
            "UnobservedTaskException",
            "A background task failed and nothing was waiting for its result.",
            e.Exception);
    }

    private static int StartAttempt() =>
        int.TryParse(Environment.GetEnvironmentVariable(StartAttemptVariable), NumberStyles.None, CultureInfo.InvariantCulture, out int attempt) ? attempt : 0;

    private static void StartOver(string[] args, IDisplayAvailability displays)
    {
        if (Environment.ProcessPath is not { } self)
        {
            return;
        }

        // Ends the time session this run began, rather than leaving it to be repaired at the next start.
        AppHost.ShutdownAsync().GetAwaiter().GetResult();
        displays.WaitUntilAvailable();

        ProcessStartInfo start = new(self) { UseShellExecute = false };
        foreach (string argument in args)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment[StartAttemptVariable] = (StartAttempt() + 1).ToString(CultureInfo.InvariantCulture);
        using Process? next = Process.Start(start);
    }

    private static void LogWaitedForDisplay(TimeSpan waited)
    {
        try
        {
            AppHost.Services.GetRequiredService<ILogger>().LogAsync(new LogEntry
            {
                Timestamp = AppHost.Services.GetRequiredService<IClock>().UtcNow,
                Level = LogLevel.Information,
                Category = "Startup",
                EventId = "WaitedForDisplay",
                Message = $"Every display was asleep at launch; started {waited.TotalSeconds:0} seconds later, when one woke."
            }).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not logged: a line in a log is not worth failing the launch for.
        }
    }

    /// <summary>The app as Avalonia's previewer builds it: with the Dock icon, as the setting starts out.</summary>
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(showDockIcon: true);

    private static AppBuilder BuildAvaloniaApp(bool showDockIcon)
    {
        AppBuilder builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions
            {
                // Read once at startup, not live - MacOSPlatformOptions is AppBuilder configuration,
                // not a runtime-toggleable property. Main passes the setting AppHost.Initialize loaded.
                ShowInDock = showDockIcon,
                DisableDefaultApplicationMenuItems = true
            })
            .With(new SkiaOptions { MaxGpuResourceSizeBytes = GpuCacheBytes });

        if (string.Equals(Environment.GetEnvironmentVariable(RenderingVariable), "software", StringComparison.OrdinalIgnoreCase))
        {
            builder = builder.With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.Software] });
        }

        return builder.LogToTrace();
    }
}
