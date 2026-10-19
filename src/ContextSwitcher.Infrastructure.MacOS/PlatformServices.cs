using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Infrastructure.Browser;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.MacOS;
using ContextSwitcher.Infrastructure.MacOS.AppleScript;
using ContextSwitcher.Infrastructure.MacOS.Applications;
using ContextSwitcher.Infrastructure.MacOS.Browser;
using ContextSwitcher.Infrastructure.MacOS.Catalog;
using ContextSwitcher.Infrastructure.MacOS.Updates;
using Microsoft.Extensions.DependencyInjection;

// The same namespace as the Windows project's PlatformServices, so the app calls whichever its build
// brings in without naming a platform.
namespace ContextSwitcher.Infrastructure.Platform;

/// <summary>
/// What the app has on a Mac: where everything is kept, how a file is replaced, what a Mac can do and
/// may run, the macOS versions of what the shared code asks of the system, and the updater.
/// </summary>
public static class PlatformServices
{
    /// <summary>Registers the Mac's services and data.</summary>
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(SettingsRoot.DotConfig);
        services.AddSingleton<IFileReplacer>(FileReplacer.Once);

        // What a Mac can do: everything the app does today; and what it may run.
        services.AddSingleton(MacPlatform.Capabilities);
        services.AddSingleton(MacPlatform.AllowedCommands);

        services.AddSingleton<IShell, MacShell>();
        services.AddSingleton<IDefaultBrowser, MacDefaultBrowser>();
        services.AddSingleton<ILoginItem, MacLoginItem>();
        services.AddSingleton<IScriptRunner, AppleScriptRunner>();
        services.AddSingleton<IBrowserRuns, ProcessBrowserRuns>();
        services.AddSingleton<IBrowserControl, MacBrowserControl>();
        services.AddSingleton<IApplicationHider, MacApplicationHider>();
        services.AddSingleton<IAppProcesses, MacAppProcesses>();
        services.AddSingleton<IAppLauncher, MacAppLauncher>();
        services.AddSingleton<IAppQuitter, MacAppQuitter>();
        services.AddSingleton<IMediaPlayer, MacMediaPlayer>();
        services.AddSingleton<IShortcutRunner, MacShortcutRunner>();
        services.AddSingleton<IRunningApplications, MacRunningApplications>();
        services.AddSingleton<IImageConverter>(provider => new SipsImageConverter(provider.GetRequiredService<IProcessRunner>(), SipsImageConverter.DefaultTimeout));
        services.AddSingleton<IPermissionsChecker, MacPermissionsChecker>();
        services.AddSingleton<IInstalledAppsService, InstalledAppsService>();
        services.AddSingleton<ISystemCatalog, SystemCatalog>();
        services.AddSingleton<IFocusShortcutInstaller, FocusShortcutInstaller>();
        services.AddSingleton<ISystemNotifications, MacNotifications>();
        services.AddSingleton<IAwayEvents, AwayEvents>();
        services.AddSingleton<IDockIcon, DockIcon>();
        services.AddSingleton<IPreferredLanguages, PreferredLanguages>();
        services.AddSingleton<IDisplayAvailability, DisplayAvailability>();
        services.AddSingleton<ISystemVersion, MacSystemVersion>();
        services.AddSingleton<IPopoverWindow, MacPopoverWindow>();

        // The updater, and which releases are the Mac's: the zip and the disk image.
        services.AddSingleton<IAppUpdater>(provider => new AppBundleUpdater(
            provider.GetRequiredService<HttpClient>(), provider.GetRequiredService<IProcessRunner>()));
        services.AddSingleton(AppBundleUpdater.Assets);
    }
}
