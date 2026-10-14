using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ContextSwitcher.App.ViewModels;

namespace ContextSwitcher.App.Views;

/// <summary>
/// Asks, as a switch starts, what to do with the apps and tabs neither profile has. Closing it -
/// its close button or Escape - is a skip: the switch goes on and touches nothing else.
/// </summary>
public sealed partial class AskOnSwitchWindow : Window
{
    public AskOnSwitchWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public AskOnSwitchWindow(AskOnSwitchViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        this.Closed += (_, _) => viewModel.Skip();
        this.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                viewModel.Skip();
            }
        };
        _ = viewModel.Completion.ContinueWith(
            _ => Avalonia.Threading.Dispatcher.UIThread.Post(this.Close),
            TaskScheduler.Default);
    }
}
