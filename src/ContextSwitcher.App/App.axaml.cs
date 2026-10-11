using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContextSwitcher.App;

public sealed partial class App : Application
{
    private static readonly Uri TrayIconUri = new("avares://ContextSwitcher/Assets/Icons/menu-neutral.png");

    private TrayIcon? trayIcon;
    private DashboardWindow? dashboardWindow;
    private MainAppWindow? mainAppWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // The theme follows the setting from the first window on, and changes the moment the setting
        // does: Settings saves the choice, the save publishes the configuration, and this applies it.
        // The theme is a UI-thread property; a configuration published from any other thread is
        // applied there rather than throwing.
        this.ApplyAppearance();
        AppHost.ConfigurationChanged += (_, _) =>
        {
            if (this.CheckAccess())
            {
                this.ApplyAppearance();
            }
            else
            {
                this.Dispatcher.Post(this.ApplyAppearance);
            }
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Anything needing the dispatcher starts here rather than in AppHost.Initialize, which
            // runs before the platform exists.
            AppHost.StartRuntimeServices();
            desktop.Exit += (_, _) =>
            {
                AppHost.ShutdownAsync().GetAwaiter().GetResult();
                DisposeTrayIcon();
            };
            this.trayIcon = CreateTrayIcon(desktop);

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

            // A fresh install otherwise lands on one empty "Default" profile - a context switcher
            // with nothing to switch between. Run the wizard before anything else in that case.
            if (!AppHost.Configuration.OnboardingCompleted)
            {
                this.ShowOnboarding();
            }
            else if (desktop.Args?.Contains("open-dashboard") == true)
            {
                this.ShowDashboard();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// "Match macOS" is Avalonia's default variant, which tracks the system's appearance live; light
    /// and dark pin it. The menu bar icon is a template image, so macOS draws that either way.
    /// </summary>
    public static ThemeVariant ThemeFor(AppearanceMode appearance) => appearance switch
    {
        AppearanceMode.Light => ThemeVariant.Light,
        AppearanceMode.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default
    };

    private void ApplyAppearance()
    {
        // Null only before AppHost has loaded a configuration, as in the headless test host.
        if (AppHost.Configuration is { } configuration)
        {
            this.RequestedThemeVariant = ThemeFor(configuration.Appearance);
        }
    }

    private TrayIcon CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        TrayMenu trayMenu = new(
            contextId => _ = SwitchFromMenuAsync(contextId),
            this.ShowDashboard,
            this.ShowMainApp,
            this.ShowUpdates,
            () => desktop.Shutdown());
        trayMenu.Update(AppHost.Configuration.Contexts, AppHost.State.CurrentContextId);

        // A waiting update shows in the menu; installing one quits this copy so the new one can take
        // its place - the updater has already arranged for it to open.
        UpdatesViewModel updates = AppHost.Services.GetRequiredService<UpdatesViewModel>();
        updates.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UpdatesViewModel.IsUpdateAvailable))
            {
                trayMenu.SetAvailableUpdate(updates.IsUpdateAvailable ? updates.AvailableVersionText : null);
            }
        };
        updates.RestartRequested += (_, _) => desktop.Shutdown();

        TrayIcon trayIcon = new()
        {
            Icon = new WindowIcon(AssetLoader.Open(TrayIconUri)),
            ToolTipText = "Context Switcher",
            Menu = trayMenu.Menu,
            IsVisible = true
        };

        // The profile list and its tick follow the configuration and the active profile. Both events
        // are raised on the UI thread, which a NativeMenu - an Avalonia object - requires. The menu is
        // updated in place, never replaced; see TrayMenu for why.
        // Opening the menu bar menu puts the popover away, the way opening one menu closes another.
        trayMenu.Menu.Opening += (_, _) => this.dashboardWindow?.Hide();

        AppHost.ConfigurationChanged += (_, _) => trayMenu.Update(AppHost.Configuration.Contexts, AppHost.State.CurrentContextId);
        AppHost.StateChanged += (_, _) => trayMenu.Update(AppHost.Configuration.Contexts, AppHost.State.CurrentContextId);

        // Belt-and-suspenders: on platforms/cases where Clicked still fires despite Menu being
        // set, this gives instant popover behavior; the "Open Dashboard" menu item above is the
        // guaranteed-reliable fallback either way.
        trayIcon.Clicked += (_, _) => this.ShowDashboard();

        MacOSProperties.SetIsTemplateIcon(trayIcon, true);

        return trayIcon;
    }

    /// <summary>
    /// A switch picked straight from the menu bar. Fire-and-forget from the menu's point of view, so
    /// this is a hard boundary: ContextSwitchService already logs its own failure paths, and anything
    /// unexpected is logged here rather than left as an unobserved task exception.
    /// </summary>
    private static async Task SwitchFromMenuAsync(string contextId)
    {
        try
        {
            IContextSwitchService switchService = AppHost.Services.GetRequiredService<IContextSwitchService>();
            await switchService
                .SwitchAsync(new ContextSwitchRequest(contextId, ContextSwitchSource.MenuBar), CancellationToken.None)
                .ConfigureAwait(true);

            IJsonStore jsonStore = AppHost.Services.GetRequiredService<IJsonStore>();
            ConfigPaths configPaths = AppHost.Services.GetRequiredService<ConfigPaths>();
            CurrentContextState? state = await jsonStore.ReadAsync<CurrentContextState>(configPaths.StatePath)
                .ConfigureAwait(true);
            AppHost.UpdateState(state ?? new CurrentContextState());
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
        if (this.dashboardWindow is null)
        {
            DashboardViewModel viewModel = AppHost.Services.GetRequiredService<DashboardViewModel>();
            this.dashboardWindow = new DashboardWindow(viewModel, this.ShowMainApp);
            this.dashboardWindow.Closed += (_, _) => this.dashboardWindow = null;
        }

        PositionNearMenuBar(this.dashboardWindow);
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
            this.mainAppWindow = new MainAppWindow(viewModel);
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

    private static void PositionNearMenuBar(Window window)
    {
        Screen? screen = window.Screens?.Primary;
        if (screen is null)
        {
            return;
        }

        // The card, not the window, goes 12 from the right edge and 4 under the menu bar: the window
        // is wider and taller by the transparent room its shadow is drawn into. Same units as this
        // has always used, which on macOS place the popover correctly.
        PixelRect area = screen.WorkingArea;
        Thickness room = DashboardWindow.ShadowRoom;
        int x = area.Right - (int)(window.Width - room.Right) - 12;
        int y = area.Y + 4 - (int)room.Top;
        window.Position = new PixelPoint(x, y);
    }

    private void DisposeTrayIcon()
    {
        this.trayIcon?.Dispose();
        this.trayIcon = null;
    }
}
