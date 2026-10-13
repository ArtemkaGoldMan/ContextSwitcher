using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;

namespace ContextSwitcher.App.Views.Pages;

public sealed partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Export and import open the Save and Open panels, which belong to this page's window - the one
    /// thing the view model can't reach for itself. A picker set already, as by a test, is kept.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (this.DataContext is SettingsViewModel settings && TopLevel.GetTopLevel(this) is { } topLevel)
        {
            settings.Transfer.FilePicker ??= new StorageProviderSettingsFilePicker(topLevel);
        }
    }
}
