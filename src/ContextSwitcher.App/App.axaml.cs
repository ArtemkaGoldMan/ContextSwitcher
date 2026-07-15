using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views;
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
            desktop.Exit += (_, _) =>
            {
                AppHost.ShutdownAsync().GetAwaiter().GetResult();
                DisposeTrayIcon();
            };
            this.trayIcon = CreateTrayIcon(desktop);

            if (desktop.Args?.Contains("open-dashboard") == true)
            {
                this.ShowDashboard();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private TrayIcon CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        NativeMenuItem dashboardItem = new("Open Dashboard");
        dashboardItem.Click += (_, _) => ShowDashboard();

        NativeMenuItem quitItem = new("Quit");
        quitItem.Click += (_, _) => desktop.Shutdown();

        NativeMenu menu = new();
        menu.Items.Add(new NativeMenuItem("Context Switcher")
        {
            IsEnabled = false
        });
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(dashboardItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quitItem);

        TrayIcon trayIcon = new()
        {
            Icon = new WindowIcon(AssetLoader.Open(TrayIconUri)),
            ToolTipText = "Context Switcher",
            Menu = menu,
            IsVisible = true
        };

        // Belt-and-suspenders: on platforms/cases where Clicked still fires despite Menu being
        // set, this gives instant popover behavior; the "Open Dashboard" menu item above is the
        // guaranteed-reliable fallback either way.
        trayIcon.Clicked += (_, _) => this.ShowDashboard();

        MacOSProperties.SetIsTemplateIcon(trayIcon, true);

        return trayIcon;
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
        this.dashboardWindow.Show();
        this.dashboardWindow.Activate();
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
