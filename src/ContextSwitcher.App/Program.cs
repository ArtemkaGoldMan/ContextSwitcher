using Avalonia;
using ContextSwitcher.App.Resources;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Infrastructure.Cli;
using ContextSwitcher.Infrastructure.MacOS;
using Microsoft.Extensions.DependencyInjection;

namespace ContextSwitcher.App;

internal static class Program
{
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
        AppHost.Initialize(args);

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

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        AppBuilder builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions
            {
                // Read once at startup, not live - MacOSPlatformOptions is AppBuilder configuration,
                // not a runtime-toggleable property. AppHost.Initialize (called before this) has
                // already loaded config by the time BuildAvaloniaApp runs.
                ShowInDock = AppHost.Configuration?.ShowDockIcon ?? true,
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
