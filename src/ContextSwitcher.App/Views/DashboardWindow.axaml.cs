using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ContextSwitcher.App.Views;

/// <summary>
/// The menu bar popover. It behaves like one: clicking anywhere outside it, opening the main app, or
/// making a clean switch dismisses it. Dismissing hides rather than closes, so reopening it is
/// instant and it keeps its state - the last switch's warnings among it.
/// </summary>
public sealed partial class DashboardWindow : Window
{
    private DashboardViewModel? viewModel;
    private MainAppWindow? mainAppWindow;

    public DashboardWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public DashboardWindow(DashboardViewModel viewModel)
        : this()
    {
        this.viewModel = viewModel;
        DataContext = viewModel;
        viewModel.OpenAppRequested += this.OnOpenAppRequested;
        viewModel.DismissRequested += this.OnDismissRequested;
        this.Deactivated += this.OnDeactivated;
        this.Closed += this.OnClosed;
    }

    private void OnOpenAppRequested(object? sender, EventArgs e)
    {
        if (this.mainAppWindow is null)
        {
            MainAppViewModel mainAppViewModel = AppHost.Services.GetRequiredService<MainAppViewModel>();
            this.mainAppWindow = new MainAppWindow(mainAppViewModel);
            this.mainAppWindow.Closed += (_, _) => this.mainAppWindow = null;
        }

        this.mainAppWindow.Show();
        this.mainAppWindow.Activate();

        // The main window taking focus would dismiss the popover anyway; saying so explicitly keeps
        // that from depending on activation order.
        this.Hide();
    }

    private void OnDismissRequested(object? sender, EventArgs e) => this.Hide();

    // Light dismiss: focus went somewhere else - the desktop, another app, the main window.
    private void OnDeactivated(object? sender, EventArgs e) => this.Hide();

    private void OnClosed(object? sender, EventArgs e)
    {
        this.Deactivated -= this.OnDeactivated;
        if (this.viewModel is not null)
        {
            this.viewModel.OpenAppRequested -= this.OnOpenAppRequested;
            this.viewModel.DismissRequested -= this.OnDismissRequested;
            this.viewModel.Dispose();
            this.viewModel = null;
        }
    }
}
