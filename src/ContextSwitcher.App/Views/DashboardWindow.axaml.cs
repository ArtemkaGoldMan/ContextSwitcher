using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ContextSwitcher.App.Views;

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
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (this.viewModel is not null)
        {
            this.viewModel.OpenAppRequested -= this.OnOpenAppRequested;
            this.viewModel.Dispose();
            this.viewModel = null;
        }
    }
}
