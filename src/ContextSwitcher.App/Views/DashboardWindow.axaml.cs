using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ContextSwitcher.App.ViewModels;

namespace ContextSwitcher.App.Views;

public sealed partial class DashboardWindow : Window
{
    private DashboardViewModel? viewModel;
    private SettingsWindow? settingsWindow;

    public DashboardWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public DashboardWindow(DashboardViewModel viewModel)
        : this()
    {
        this.viewModel = viewModel;
        DataContext = viewModel;
        viewModel.OpenSettingsRequested += this.OnOpenSettingsRequested;
        this.Closed += this.OnClosed;
    }

    private void OnOpenSettingsRequested(object? sender, EventArgs e)
    {
        if (this.settingsWindow is null)
        {
            this.settingsWindow = new SettingsWindow();
            this.settingsWindow.Closed += (_, _) => this.settingsWindow = null;
        }

        this.settingsWindow.Show();
        this.settingsWindow.Activate();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (this.viewModel is not null)
        {
            this.viewModel.OpenSettingsRequested -= this.OnOpenSettingsRequested;
            this.viewModel.Dispose();
            this.viewModel = null;
        }
    }
}
