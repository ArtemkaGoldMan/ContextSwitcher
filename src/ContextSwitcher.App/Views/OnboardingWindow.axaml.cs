using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ContextSwitcher.App.ViewModels;

namespace ContextSwitcher.App.Views;

public sealed partial class OnboardingWindow : Window
{
    private OnboardingViewModel? viewModel;

    public OnboardingWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public OnboardingWindow(OnboardingViewModel viewModel)
        : this()
    {
        this.viewModel = viewModel;
        DataContext = viewModel;
        viewModel.Completed += this.OnCompleted;
        this.Closed += this.OnClosed;
    }

    private void OnCompleted(object? sender, EventArgs e) => this.Close();

    private void OnClosed(object? sender, EventArgs e)
    {
        if (this.viewModel is not null)
        {
            this.viewModel.Completed -= this.OnCompleted;
            this.viewModel = null;
        }
    }
}
