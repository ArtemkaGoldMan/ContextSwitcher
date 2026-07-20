using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ContextSwitcher.App.Views.Pages;

public sealed partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
