using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using System.Globalization;
using ContextSwitcher.App.Resources;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Platform;
using ContextSwitcher.Core.Updates;
using ContextSwitcher.Infrastructure.Cli;
using Microsoft.Extensions.DependencyInjection;

namespace ContextSwitcher.App;

public sealed partial class App : Application
{
    private TrayIcon? trayIcon;
    private ThemeFollower? themeFollower;
    private DashboardWindow? dashboardWindow;
    private MainAppWindow? mainAppWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // The theme's own texts - the few its controls carry - in the app's language too.
        if (this.Styles.OfType<Semi.Avalonia.SemiTheme>().FirstOrDefault() is { } semi && AppLanguages.Current == "uk")
        {
            semi.Locale = CultureInfo.GetCultureInfo("uk-UA");
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            IAppState appState = AppHost.Services.GetRequiredService<IAppState>();
            this.themeFollower = new ThemeFollower(appState, this);

            // The Dock icon too changes the moment the setting does, on the UI thread like the theme.
            IDockIcon dockIcon = AppHost.Services.GetRequiredService<IDockIcon>();
            appState.ConfigurationChanged += (_, _) =>
            {
                if (this.CheckAccess())
                {
                    this.ApplyDockIcon(appState, dockIcon);
                }
                else
                {
                    this.Dispatcher.Post(() => this.ApplyDockIcon(appState, dockIcon));
                }
            };

            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Anything needing the dispatcher starts here rather than in AppHost.Initialize, which
            // runs before the platform exists.
            AppHost.StartRuntimeServices();
            desktop.Exit += (_, _) =>
            {
                AppHost.ShutdownAsync().GetAwaiter().GetResult();
                DisposeTrayIcon();
                this.themeFollower?.Dispose();
            };
            this.trayIcon = CreateTrayIcon(desktop, appState);
            ShowInMenuBar(this, this.trayIcon);

            // Clicking the desktop or another app sends this app to the background, and the popover
            // goes with it. The popover's own Deactivated only covers the case where it was the key
            // window; this also covers the one where it never became key at all.
            if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
            {
                activatable.Deactivated += (_, _) => this.dashboardWindow?.Hide();

                // Opening the app again while it runs - from Applications, Spotlight or its Dock
                // icon - brings up its window. The menu bar icon used to be the only way in, and
                // macOS hides menu bar icons that don't fit beside a MacBook's notch.
                activatable.Activated += (_, e) =>
                {
                    if (e.Kind == ActivationKind.Reopen)
                    {
                        this.ShowOnReopen(desktop);
                    }
                };
            }

            // Opening the app again where the system doesn't bring the running copy forward itself -
            // Windows - reaches it here instead: the same as a Reopen, or the dashboard if asked for.
            ISingleInstance singleInstance = AppHost.Services.GetRequiredService<ISingleInstance>();
            singleInstance.Reopened += (_, arguments) => this.Dispatcher.Post(() =>
            {
                if (arguments.Contains(CliCommandRouter.OpenDashboardCommand) && appState.Configuration.OnboardingCompleted)
                {
                    this.ShowDashboard();
                }
                else
                {
                    this.ShowOnReopen(desktop);
                }
            });
            singleInstance.Listen();

            // A fresh install otherwise lands on one empty "Default" profile - a context switcher
            // with nothing to switch between. Run the wizard before anything else in that case.
            if (!appState.Configuration.OnboardingCompleted)
            {
                this.ShowOnboarding();
            }
            else if (desktop.Args?.Contains(CliCommandRouter.OpenDashboardCommand) == true)
            {
                this.ShowDashboard();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Shows or hides the Dock icon the moment Settings saves the choice, rather than at the next
    /// start. Leaving the Dock makes an app give up being frontmost, so the main window - where the
    /// switch was flipped - is brought back.
    /// </summary>
    private void ApplyDockIcon(IAppState appState, IDockIcon dockIcon)
    {
        bool wanted = appState.Configuration.ShowDockIcon;
        if (dockIcon.IsShown() is not { } shown || shown == wanted)
        {
            return;
        }

        dockIcon.Show(wanted);
        this.mainAppWindow?.Activate();
    }

    /// <summary>
    /// Puts the icon in the menu bar. Since Avalonia 12.1.3 a tray icon only reaches the menu bar once
    /// it is one of the application's <see cref="TrayIcon.Icons"/>; one that is only constructed stays
    /// a plain object, and 0.6.4 to 0.6.6 shipped with no menu bar icon at all because of it.
    /// </summary>
    public static void ShowInMenuBar(Application app, TrayIcon icon) => TrayIcon.SetIcons(app, [icon]);

    private TrayIcon CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, IAppState appState)
    {
        TrayMenu trayMenu = new(
            contextId => _ = SwitchFromMenuAsync(contextId, appState),
            this.ShowDashboard,
            this.ShowMainApp,
            this.ShowUpdates,
            () => desktop.Shutdown());
        trayMenu.Update(appState.Configuration.Contexts, appState.State.CurrentContextId);

        // A waiting update shows in the menu; installing one quits this copy so the new one can take
        // its place - the updater has already arranged for it to open.
        UpdateChecker updates = AppHost.Services.GetRequiredService<UpdateChecker>();
        updates.Changed += (_, _) => trayMenu.SetAvailableUpdate(updates.Available is { } release ? ReleaseVersion.Format(release.Version) : null);
        updates.RestartRequested += (_, _) => desktop.Shutdown();

        // Clicking the update notification leads to the same place. It can be one shown before a
        // restart, while this run hasn't found the update yet, so it asks again.
        AppHost.Services.GetRequiredService<ISystemNotifications>().Clicked += (_, id) =>
        {
            if (id == UpdateChecker.NotificationId)
            {
                this.Dispatcher.Post(() =>
                {
                    this.ShowUpdates();
                    if (updates.Available is null)
                    {
                        _ = updates.CheckAsync(userAsked: true);
                    }
                });
            }
        };

        ITrayIconLook look = AppHost.Services.GetRequiredService<ITrayIconLook>();
        TrayIcon trayIcon = new()
        {
            Icon = TrayIconImage(look.Current),
            ToolTipText = AppHost.Services.GetRequiredService<IAppInfo>().DisplayName,
            Menu = trayMenu.Menu,
            IsVisible = true
        };

        // A template image is coloured by macOS; any other drawing is chosen for the taskbar, and
        // changed when the taskbar turns light or dark.
        MacOSProperties.SetIsTemplateIcon(trayIcon, look.Current == TrayIconLook.Template);
        look.Changed += (_, _) => this.Dispatcher.Post(() => trayIcon.Icon = TrayIconImage(look.Current));

        // The profile list and its tick follow the configuration and the active profile. Both events
        // are raised on the UI thread, which a NativeMenu - an Avalonia object - requires. The menu is
        // updated in place, never replaced; see TrayMenu for why.
        // Opening the menu bar menu puts the popover away, the way opening one menu closes another.
        trayMenu.Menu.Opening += (_, _) => this.dashboardWindow?.Hide();

        appState.ConfigurationChanged += (_, _) => trayMenu.Update(appState.Configuration.Contexts, appState.State.CurrentContextId);
        appState.StateChanged += (_, _) => trayMenu.Update(appState.Configuration.Contexts, appState.State.CurrentContextId);

        // Belt-and-suspenders: on platforms/cases where Clicked still fires despite Menu being
        // set, this gives instant popover behavior; the "Open Dashboard" menu item above is the
        // guaranteed-reliable fallback either way.
        trayIcon.Clicked += (_, _) => this.ShowDashboard();

        return trayIcon;
    }

    /// <summary>The drawing of the menu bar or tray icon for <paramref name="look"/>.</summary>
    private static WindowIcon TrayIconImage(TrayIconLook look) => new(AssetLoader.Open(new Uri(look switch
    {
        TrayIconLook.Dark => "avares://ContextSwitcher/Assets/Icons/tray-dark.png",
        TrayIconLook.Light => "avares://ContextSwitcher/Assets/Icons/tray-light.png",
        _ => "avares://ContextSwitcher/Assets/Icons/menu-neutral.png"
    })));

    /// <summary>
    /// A switch picked straight from the menu bar. Fire-and-forget from the menu's point of view, so
    /// this is a hard boundary: ContextSwitchService already logs its own failure paths, and anything
    /// unexpected is logged here rather than left as an unobserved task exception.
    /// </summary>
    private static async Task SwitchFromMenuAsync(string contextId, IAppState appState)
    {
        try
        {
            IContextSwitchService switchService = AppHost.Services.GetRequiredService<IContextSwitchService>();
            await switchService
                .SwitchAsync(ContextSwitchRequest.ForClickOn(contextId, appState.State.CurrentContextId, ContextSwitchSource.TrayMenu), CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ILogger logger = AppHost.Services.GetRequiredService<ILogger>();
            IClock clock = AppHost.Services.GetRequiredService<IClock>();
            await logger.LogAsync(
                new LogEntry
                {
                    Timestamp = clock.UtcNow,
                    Level = LogLevel.Error,
                    Category = "MenuBar",
                    EventId = "MenuSwitchFailed",
                    Message = $"Unhandled error switching to '{contextId}' from the menu bar: {ex.Message}",
                    ContextId = contextId
                },
                CancellationToken.None).ConfigureAwait(true);
        }
    }

    /// <summary>The setup wizard while it is still open - it comes first - and otherwise the main window.</summary>
    private void ShowOnReopen(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (desktop.Windows.OfType<OnboardingWindow>().FirstOrDefault() is { } onboarding)
        {
            onboarding.Activate();
            return;
        }

        this.ShowMainApp();
    }

    private void ShowOnboarding()
    {
        OnboardingViewModel viewModel = AppHost.Services.GetRequiredService<OnboardingViewModel>();
        OnboardingWindow window = new(viewModel);

        // Show the Dashboard once setup finishes so the user immediately sees the profiles they
        // just created, rather than being dropped into an app with no visible window.
        window.Closed += (_, _) => this.ShowDashboard();
        window.Show();
    }

    private void ShowDashboard()
    {
        IPopoverWindow popover = AppHost.Services.GetRequiredService<IPopoverWindow>();
        if (this.dashboardWindow is null)
        {
            DashboardViewModel viewModel = AppHost.Services.GetRequiredService<DashboardViewModel>();
            DashboardWindow window = new(viewModel, this.ShowMainApp, popover, AppHost.Services.GetRequiredService<ILogger>())
            {
                Title = AppHost.Services.GetRequiredService<IAppInfo>().DisplayName
            };
            window.Closed += (_, _) => this.dashboardWindow = null;

            // The popover is as tall as what it shows. Beside a taskbar along the bottom it grows
            // upwards, so it is placed again whenever its height changes.
            window.SizeChanged += (_, _) => PlacePopover(window, popover);
            this.dashboardWindow = window;
        }

        PlacePopover(this.dashboardWindow, popover);
        this.dashboardWindow.ShowAsPopover();
    }

    /// <summary>
    /// Opens the main window, or brings it forward if it is already open. Both the menu bar's
    /// "Open App" and the dashboard's go through here, so there is only ever one.
    /// </summary>
    private void ShowMainApp()
    {
        if (this.mainAppWindow is null)
        {
            MainAppViewModel viewModel = AppHost.Services.GetRequiredService<MainAppViewModel>();
            this.mainAppWindow = new MainAppWindow(viewModel) { Title = AppHost.Services.GetRequiredService<IAppInfo>().DisplayName };
            this.mainAppWindow.Closed += (_, _) => this.mainAppWindow = null;
        }

        this.mainAppWindow.Show();
        this.mainAppWindow.Activate();
    }

    /// <summary>The menu bar's "Update to …": the main window, on Settings, where the update is.</summary>
    private void ShowUpdates()
    {
        this.ShowMainApp();
        (this.mainAppWindow?.DataContext as MainAppViewModel)?.ShowSettings();
    }

    /// <summary>
    /// Puts the popover where its platform has it - under the menu bar, or beside the taskbar - on the
    /// main screen. The card, not the window, lines up with the screen's edges: the window is wider
    /// and taller by the transparent room its shadow is drawn into.
    /// </summary>
    private static void PlacePopover(Window window, IPopoverWindow popover)
    {
        if (window.Screens?.Primary is not { } screen)
        {
            return;
        }

        // Before it first shows, the window has no height yet: it is measured for what it will show.
        double height = window.Bounds.Height;
        if (height <= 0)
        {
            window.Measure(new Size(window.Width, double.PositiveInfinity));
            height = window.DesiredSize.Height;
        }

        Thickness room = DashboardWindow.ShadowRoom;
        WindowPosition place = popover.Place(
            new PopoverScreen(ScreenAreaOf(screen.Bounds), ScreenAreaOf(screen.WorkingArea), screen.Scaling),
            new PopoverSize(window.Width, height, room.Left, room.Top, room.Right, room.Bottom));
        PixelPoint position = new(place.X, place.Y);
        if (window.Position != position)
        {
            window.Position = position;
        }
    }

    private static ScreenArea ScreenAreaOf(PixelRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    private void DisposeTrayIcon()
    {
        this.trayIcon?.Dispose();
        this.trayIcon = null;
    }
}
