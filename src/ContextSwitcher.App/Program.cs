using System.Diagnostics;
using System.Globalization;
using Avalonia;
using ContextSwitcher.App.Resources;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.Cli;
using ContextSwitcher.Infrastructure.MacOS;
using Microsoft.Extensions.DependencyInjection;

namespace ContextSwitcher.App;

internal static class Program
{
    /// <summary>How many times a launch that could not start drawing starts itself over before giving up.</summary>
    private const int MaxStartAttempts = 3;

    private const string StartAttemptVariable = "CONTEXTSWITCHER_START_ATTEMPT";

    [STAThread]
    public static void Main(string[] args)
    {
        // With every display asleep Avalonia cannot start drawing, and the app used to abort here with a
        // crash report. A window needs a screen anyway, so wait for one - before AppHost.Initialize,
        // which starts counting time: nothing has begun until there is somewhere to show it. The
        // command line never draws, so it never waits.
        bool interactive = args.Length == 0 || !CliCommandRouter.IsHeadlessCommand(args[0]);
        TimeSpan waitedForDisplay = interactive ? DisplayAvailability.WaitUntilAvailable() : TimeSpan.Zero;

        AppHost.Initialize(args);
        if (waitedForDisplay > TimeSpan.Zero)
        {
            LogWaitedForDisplay(waitedForDisplay);
        }

        if (args.Length > 0 && CliCommandRouter.IsHeadlessCommand(args[0]))
        {
            CliCommandRouter router = AppHost.Services.GetRequiredService<CliCommandRouter>();
            int exitCode = router.RunAsync(args, AppHost.Configuration, AppHost.ConfigurationValidation, AppHost.State, Console.Out, CancellationToken.None, AppHost.ConfigurationWasQuarantined)
                .GetAwaiter()
                .GetResult();
            Environment.Exit(exitCode);
            return;
        }

        // Before any window, so every text is read in it. The command line above stays English: scripts
        // read it.
        AppLanguages.Apply(AppLanguages.Resolve(AppHost.Configuration?.Language, PreferredLanguages.Get()));

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (InvalidOperationException ex) when (DisplayAvailability.IsRenderTimerFailure(ex) && StartAttempt() < MaxStartAttempts)
        {
            // The screen went dark between the look above and Avalonia's own: Avalonia cannot be set up
            // a second time in this process, so a fresh one takes over once a display is awake.
            StartOver(args);
        }
    }

    private static int StartAttempt() =>
        int.TryParse(Environment.GetEnvironmentVariable(StartAttemptVariable), NumberStyles.None, CultureInfo.InvariantCulture, out int attempt) ? attempt : 0;

    private static void StartOver(string[] args)
    {
        if (Environment.ProcessPath is not { } self)
        {
            return;
        }

        // Ends the time session this run began, rather than leaving it to be repaired at the next start.
        AppHost.ShutdownAsync().GetAwaiter().GetResult();
        DisplayAvailability.WaitUntilAvailable();

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
            // A line in a log is not worth failing the launch for.
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions
            {
                // Read once at startup, not live - MacOSPlatformOptions is AppBuilder configuration,
                // not a runtime-toggleable property. AppHost.Initialize (called before this) has
                // already loaded config by the time BuildAvaloniaApp runs.
                ShowInDock = AppHost.Configuration?.ShowDockIcon ?? true,
                DisableDefaultApplicationMenuItems = true
            })
            .LogToTrace();
    }
}
