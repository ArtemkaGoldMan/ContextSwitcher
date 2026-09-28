using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ContextSwitcher.App;

public sealed partial class App : Application
{
    private static readonly Uri TrayIconUri = new("avares://ContextSwitcher/Assets/Icons/menu-neutral.png");

    private TrayIcon? trayIcon;
    private DashboardWindow? dashboardWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
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

    private TrayIcon CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        TrayMenu trayMenu = new(
            contextId => _ = SwitchFromMenuAsync(contextId),
            this.ShowDashboard,
            () => desktop.Shutdown());
        trayMenu.Update(AppHost.Configuration.Contexts, AppHost.State.CurrentContextId);

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
            this.dashboardWindow = new DashboardWindow(viewModel);
            this.dashboardWindow.Closed += (_, _) => this.dashboardWindow = null;
        }

        PositionNearMenuBar(this.dashboardWindow);
        this.dashboardWindow.ShowAsPopover();
    }

    private static void PositionNearMenuBar(Window window)
    {
        Screen? screen = window.Screens?.Primary;
        if (screen is null)
        {
            return;
        }

        PixelRect area = screen.WorkingArea;
        int x = area.Right - (int)window.Width - 12;
        int y = area.Y + 4;
        window.Position = new PixelPoint(x, y);
    }

    private void DisposeTrayIcon()
    {
        this.trayIcon?.Dispose();
        this.trayIcon = null;
    }
}
