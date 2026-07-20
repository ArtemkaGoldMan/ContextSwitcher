using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ContextSwitcher.App.Views.Pages;

public sealed partial class ProfilesPage : UserControl
{
    public ProfilesPage()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
