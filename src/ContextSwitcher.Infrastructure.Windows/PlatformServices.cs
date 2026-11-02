using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.Browser;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Windows;
using ContextSwitcher.Infrastructure.Windows.Applications;
using ContextSwitcher.Infrastructure.Windows.NotYet;
using Microsoft.Extensions.DependencyInjection;

// The same namespace as the macOS project's PlatformServices, so the app calls whichever its build
// brings in without naming a platform.
namespace ContextSwitcher.Infrastructure.Platform;

/// <summary>
/// What the app has on Windows: where everything is kept, how a file is replaced, what Windows can do
/// and may run, and the Windows versions of what the shared code asks of the system. What later stages
/// of <c>docs/windows-plan.md</c> bring is a stand-in until then, from <c>NotYet/</c>, and the
/// capabilities hide it.
/// </summary>
public static class PlatformServices
{
    /// <summary>Registers Windows' services and data.</summary>
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(SettingsRoot.AppData);
        services.AddSingleton<IFileReplacer>(FileReplacer.RetryingWhileHeldOpen);

        // What Windows can do so far, and what it may run.
        services.AddSingleton(WindowsPlatform.Capabilities);
        services.AddSingleton(WindowsPlatform.AllowedCommands);

        // The shell of the app: the tray, its popover, one copy, links and files, the language.
        services.AddSingleton<IShell, WindowsShell>();
        services.AddSingleton<IPopoverWindow, WindowsPopoverWindow>();
        services.AddSingleton<ITrayIconLook, WindowsTrayIconLook>();
        services.AddSingleton<ISingleInstance>(provider => new WindowsSingleInstance(
            provider.GetRequiredService<ConfigPaths>().BaseDirectory, provider.GetRequiredService<ILogger>()));
        services.AddSingleton<IPreferredLanguages, WindowsPreferredLanguages>();
        services.AddSingleton<ISystemVersion, WindowsSystemVersion>();
        services.AddSingleton<IDisplayAvailability, WindowsDisplayAvailability>();

        // What Windows doesn't have at all.
        services.AddSingleton<IDockIcon, NoDockIcon>();
        services.AddSingleton<IPermissionsChecker, WindowsPermissionsChecker>();
        services.AddSingleton<NoShortcuts>();
        services.AddSingleton<IShortcutRunner>(provider => provider.GetRequiredService<NoShortcuts>());
        services.AddSingleton<IFocusShortcutInstaller>(provider => provider.GetRequiredService<NoShortcuts>());

        // The Start menu's apps, and opening one.
        services.AddSingleton(StartAppsOptions.Default);
        services.AddSingleton<StartApps>();
        services.AddSingleton(InstalledAppsOptions.Default);
        services.AddSingleton<IAppIcons, ShellAppIcons>();
        services.AddSingleton<WindowsInstalledApps>();
        services.AddSingleton<IInstalledAppsService>(provider => provider.GetRequiredService<WindowsInstalledApps>());
        services.AddSingleton<IAppLauncher, WindowsAppLauncher>();

        // Telling an app's processes from the others, asking its windows to close, and ending it.
        services.AddSingleton(AppProcessesOptions.Default);
        services.AddSingleton<IKnownFolders, WindowsKnownFolders>();
        services.AddSingleton<ISystemProcesses, SystemProcesses>();
        services.AddSingleton<ISystemWindows, SystemWindows>();
        services.AddSingleton<AppIdentities>();
        services.AddSingleton<IAppIdentityCatalog, WindowsAppIdentityCatalog>();
        services.AddSingleton<WindowsAppProcesses>();
        services.AddSingleton<IAppProcesses>(provider => provider.GetRequiredService<WindowsAppProcesses>());
        services.AddSingleton<IAppQuitter, WindowsAppQuitter>();

        // Minimising apps, and the apps open now, which a switch asks about.
        services.AddSingleton<AppWindows>();
        services.AddSingleton<IApplicationHider, WindowsApplicationHider>();
        services.AddSingleton<IRunningApplications, WindowsRunningApplications>();

        // Both answer without waiting, from the Start menu as it was last listed: list it, and draw the icons the picker
        // shows, as the app starts.
        services.AddSingleton<IWarmUp>(provider => provider.GetRequiredService<WindowsInstalledApps>());

        // Opening at sign-in, sleep and lock: later in stage 1.
        services.AddSingleton<ILoginItem, NotYetLoginItem>();
        services.AddSingleton<IAwayEvents, NotYetAwayEvents>();

        // The browser and site icons: stage 2. Music, Docker's list and notifications: stage 3.
        services.AddSingleton<NotYetBrowserControl>();
        services.AddSingleton<IBrowserControl>(provider => provider.GetRequiredService<NotYetBrowserControl>());
        services.AddSingleton<IBrowserRuns>(provider => provider.GetRequiredService<NotYetBrowserControl>());
        services.AddSingleton<IDefaultBrowser, NotYetDefaultBrowser>();
        services.AddSingleton<IImageConverter, NotYetImageConverter>();
        services.AddSingleton<ISystemCatalog, NotYetSystemCatalog>();
        services.AddSingleton<IMediaPlayer, NotYetMediaPlayer>();
        services.AddSingleton<ISystemNotifications, NotYetNotifications>();

        // The updater comes with the installer, at the end of stage 1; the release files are Windows'
        // already, so a release with only the Mac's is never offered here.
        services.AddSingleton<IAppUpdater, NotYetUpdater>();
        services.AddSingleton(WindowsPlatform.Assets);
    }
}
