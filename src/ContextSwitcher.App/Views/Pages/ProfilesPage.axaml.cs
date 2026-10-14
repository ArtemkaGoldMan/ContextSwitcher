using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ContextSwitcher.App.Controls;
using ContextSwitcher.App.ViewModels;

namespace ContextSwitcher.App.Views.Pages;

public sealed partial class ProfilesPage : UserControl
{
    private RowReorder? reorder;

    /// <summary>How long a card is held before it is picked up to be dragged.</summary>
    public TimeSpan HoldToDrag { get; set; } = RowReorder.DefaultHold;

    public ProfilesPage()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// The cards can be dragged into another order, which is the order in the menu bar and the
    /// dashboard too. The drag needs the cards on screen, so it starts once the page is.
    /// </summary>
    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (this.reorder is null && this.FindControl<ItemsControl>("ProfileList") is { } list)
        {
            this.reorder = new RowReorder(
                list,
                (row, index) => this.DataContext is ProfilesViewModel profiles && row is ProfileRowViewModel profile
                    ? profiles.MoveAsync(profile.Context.Id, index)
                    : Task.CompletedTask,
                row => (row as ProfileRowViewModel)?.Context.Id,
                this.HoldToDrag);
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        this.reorder?.Dispose();
        this.reorder = null;
        base.OnDetachedFromVisualTree(e);
    }
}
