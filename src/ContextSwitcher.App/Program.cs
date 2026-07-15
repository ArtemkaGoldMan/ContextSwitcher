using Avalonia;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Infrastructure.Cli;
using Microsoft.Extensions.DependencyInjection;

namespace ContextSwitcher.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppHost.Initialize(args);

        if (args.Length > 0 && CliCommandRouter.IsHeadlessCommand(args[0]))
        {
            CliCommandRouter router = AppHost.Services.GetRequiredService<CliCommandRouter>();
            int exitCode = router.RunAsync(args, AppHost.Configuration, AppHost.ConfigurationValidation, AppHost.State, Console.Out, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Environment.Exit(exitCode);
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions
            {
                ShowInDock = false,
                DisableDefaultApplicationMenuItems = true
            })
            .LogToTrace();
    }
}
