using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ContextSwitcher.App.ViewModels;

namespace ContextSwitcher.App.Views;

public sealed partial class MainAppWindow : Window
{
    private MainAppViewModel? viewModel;

    public MainAppWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public MainAppWindow(MainAppViewModel viewModel)
        : this()
    {
        this.viewModel = viewModel;
        DataContext = viewModel;
        this.Closed += this.OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        this.viewModel?.Dispose();
        this.viewModel = null;
    }
}
