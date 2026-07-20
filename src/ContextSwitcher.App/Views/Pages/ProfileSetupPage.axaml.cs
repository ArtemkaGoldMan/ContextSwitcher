using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ContextSwitcher.App.Views.Pages;

public sealed partial class ProfileSetupPage : UserControl
{
    public ProfileSetupPage()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
