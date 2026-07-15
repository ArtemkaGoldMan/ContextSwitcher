using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ContextSwitcher.App.Views;

public sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
