using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace ContextSwitcher.App.Controls;

/// <summary>
/// <c>PopupActions.ClosesPopup="True"</c> on a button inside a flyout closes the flyout once the
/// button has done its work. Used by every picker's "Type a name…": it adds a row to type into
/// under the flyout, and leaving the flyout open on top of that row hid what it had just added.
/// </summary>
public static class PopupActions
{
    public static readonly AttachedProperty<bool> ClosesPopupProperty =
        AvaloniaProperty.RegisterAttached<Button, bool>("ClosesPopup", typeof(PopupActions));

    static PopupActions()
    {
        ClosesPopupProperty.Changed.AddClassHandler<Button>((button, e) =>
        {
            if (e.NewValue is true)
            {
                button.Click += OnClick;
            }
            else
            {
                button.Click -= OnClick;
            }
        });
    }

    public static bool GetClosesPopup(Button button) => button.GetValue(ClosesPopupProperty);

    public static void SetClosesPopup(Button button, bool value) => button.SetValue(ClosesPopupProperty, value);

    private static void OnClick(object? sender, RoutedEventArgs e)
    {
        // Click is raised before the button runs its command, so close afterwards: closing first
        // would detach the button and its command would run against a torn-down tree.
        if (sender is Button button && button.FindLogicalAncestorOfType<Popup>() is { } popup)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => popup.Close());
        }
    }
}
