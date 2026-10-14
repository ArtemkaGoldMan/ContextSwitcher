using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ContextSwitcher.App.Controls;

/// <summary>
/// Lets the cards of a list be dragged into a new order. A card is a button - a click on it turns its
/// profile on - so a press only becomes a drag once the pointer has moved a few pixels; before that it
/// is still a click, and after it the click is not made.
///
/// While one is held it follows the pointer, lifted over the rest, and the cards it passes make room
/// for it. Letting go puts it where the others have made room; Escape puts it back. Alt with the up or
/// down arrow does the same from the keyboard, one place at a time.
/// </summary>
public sealed class RowReorder : IDisposable
{
    /// <summary>How far the pointer goes before a press is a drag, not a click that wavered.</summary>
    private const double DragStartsAfter = 6;

    private readonly ItemsControl list;
    private readonly Func<object?, int, Task> move;
    private readonly Func<object?, string?> idOf;

    private Control? pressed;
    private Point pressedAt;
    private int from = -1;
    private int target = -1;
    private bool dragging;
    private List<Slot> slots = [];

    /// <param name="list">The list whose cards are dragged; each item's card is a <c>profileCard</c> button.</param>
    /// <param name="move">Called with an item's data and the place it now goes to.</param>
    /// <param name="idOf">An item's identity, for putting the keyboard's focus back on it after it has moved.</param>
    public RowReorder(ItemsControl list, Func<object?, int, Task> move, Func<object?, string?> idOf)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(idOf);

        this.list = list;
        this.move = move;
        this.idOf = idOf;

        // Tunnelling, so this sees the press before the button does, and handled events too: a button
        // marks its own press handled.
        list.AddHandler(InputElement.PointerPressedEvent, this.OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        list.AddHandler(InputElement.PointerMovedEvent, this.OnMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        list.AddHandler(InputElement.PointerReleasedEvent, this.OnReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        list.AddHandler(InputElement.PointerCaptureLostEvent, this.OnCaptureLost, RoutingStrategies.Bubble, handledEventsToo: true);
        list.AddHandler(InputElement.KeyDownEvent, this.OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>Whether a card is being held and moved right now.</summary>
    public bool IsDragging => this.dragging;

    public void Dispose()
    {
        this.list.RemoveHandler(InputElement.PointerPressedEvent, this.OnPressed);
        this.list.RemoveHandler(InputElement.PointerMovedEvent, this.OnMoved);
        this.list.RemoveHandler(InputElement.PointerReleasedEvent, this.OnReleased);
        this.list.RemoveHandler(InputElement.PointerCaptureLostEvent, this.OnCaptureLost);
        this.list.RemoveHandler(InputElement.KeyDownEvent, this.OnKeyDown);
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        this.Forget();
        if (!e.GetCurrentPoint(this.list).Properties.IsLeftButtonPressed || e.Source is not Visual source)
        {
            return;
        }

        // A row's own buttons - Edit, Duplicate, Delete - take their clicks; they are not handles.
        if (source.GetSelfAndVisualAncestors().OfType<Button>().Any(button => button.Classes.Contains("rowAction")))
        {
            return;
        }

        if (source.GetSelfAndVisualAncestors().OfType<Button>().FirstOrDefault(button => button.Classes.Contains("profileCard")) is not { } card
            || ContainerOf(card) is not { } container)
        {
            return;
        }

        this.from = this.list.IndexFromContainer(container);
        if (this.from < 0)
        {
            return;
        }

        this.pressed = container;
        this.pressedAt = e.GetPosition(this.list);
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (this.pressed is null)
        {
            return;
        }

        Point now = e.GetPosition(this.list);
        if (!this.dragging)
        {
            if (!e.GetCurrentPoint(this.list).Properties.IsLeftButtonPressed)
            {
                this.Forget();
                return;
            }

            if (Math.Abs(now.Y - this.pressedAt.Y) + Math.Abs(now.X - this.pressedAt.X) < DragStartsAfter)
            {
                return;
            }

            this.StartDragging(e.Pointer);
        }

        this.Follow(now.Y - this.pressedAt.Y);
        e.Handled = true;
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!this.dragging)
        {
            this.Forget();
            return;
        }

        // The click that would have turned the profile on is not made: the card was not clicked, it was moved.
        e.Handled = true;
        this.Finish(commit: true, e.Pointer);
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (this.dragging)
        {
            this.Finish(commit: false, pointer: null);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (this.dragging && e.Key == Key.Escape)
        {
            e.Handled = true;
            this.Finish(commit: false, pointer: null);
            return;
        }

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Alt) || e.Key is not (Key.Up or Key.Down)
            || TopLevel.GetTopLevel(this.list)?.FocusManager?.GetFocusedElement() is not Visual focused
            || ContainerOf(focused) is not { } container)
        {
            return;
        }

        int index = this.list.IndexFromContainer(container);
        int to = index + (e.Key == Key.Up ? -1 : 1);
        if (index < 0 || to < 0 || to >= this.list.ItemCount)
        {
            return;
        }

        e.Handled = true;
        object? item = this.list.Items.Cast<object?>().ElementAt(index);
        string? id = this.idOf(item);
        _ = this.move(item, to);

        // The cards are made again in their new order, which takes the keyboard's focus with the old
        // ones; it goes back to the card that moved, as soon as the new ones are laid out.
        this.FocusCardWhenLaidOut(id, to, attemptsLeft: 5);
    }

    private void StartDragging(IPointer pointer)
    {
        this.dragging = true;
        pointer.Capture(this.list);

        this.slots = Enumerable.Range(0, this.list.ItemCount)
            .Select(index => this.list.ContainerFromIndex(index))
            .OfType<Control>()
            .Select((container, index) => new Slot(container, index, container.Bounds.Y, container.Bounds.Height))
            .ToList();

        for (int i = 0; i < this.slots.Count; i++)
        {
            this.slots[i].Container.Classes.Add(i == this.from ? "lifted" : "reorder");
        }

        this.target = this.from;
    }

    /// <summary>Moves the held card by <paramref name="dy"/> from where it started and the others out of its way.</summary>
    private void Follow(double dy)
    {
        if (this.slots.Count == 0 || this.from < 0 || this.from >= this.slots.Count)
        {
            return;
        }

        Slot held = this.slots[this.from];
        double firstTop = this.slots[0].Top;
        double lastBottom = this.slots[^1].Top + this.slots[^1].Height;
        double top = Math.Clamp(held.Top + dy, firstTop, lastBottom - held.Height);
        held.Container.RenderTransform = new TranslateTransform(0, top - held.Top);

        // Where it would land: after every other card whose middle is above its own.
        double middle = top + (held.Height / 2);
        this.target = this.slots.Count(slot => slot.Index != this.from && slot.Top + (slot.Height / 2) < middle);

        // Room is made by the distance between one card's top and the next's: its height and the gap.
        double step = this.slots.Count > 1 ? this.slots[1].Top - this.slots[0].Top : held.Height;
        foreach (Slot slot in this.slots.Where(slot => slot.Index != this.from))
        {
            double shift = 0;
            if (slot.Index < this.from && slot.Index >= this.target)
            {
                shift = step;
            }
            else if (slot.Index > this.from && slot.Index <= this.target)
            {
                shift = -step;
            }

            slot.Container.RenderTransform = shift == 0 ? null : TransformOperations.Parse(FormattableString.Invariant($"translateY({shift}px)"));
        }
    }

    private void Finish(bool commit, IPointer? pointer)
    {
        object? item = this.from >= 0 && this.from < this.list.ItemCount ? this.list.Items.Cast<object?>().ElementAt(this.from) : null;
        int to = this.target;
        bool moved = commit && to >= 0 && to != this.from && item is not null;

        this.dragging = false;
        pointer?.Capture(null);
        this.Reset();
        this.Forget();

        if (moved)
        {
            _ = this.move(item, to);
        }
    }

    private void Reset()
    {
        foreach (Slot slot in this.slots)
        {
            slot.Container.RenderTransform = null;
            slot.Container.Classes.Remove("lifted");
            slot.Container.Classes.Remove("reorder");
        }

        this.slots = [];
    }

    private void Forget()
    {
        this.pressed = null;
        this.from = -1;
        this.target = -1;
    }

    /// <summary>
    /// Focuses the card of <paramref name="id"/> once it exists and has a place: the ones made after a
    /// move are laid out a moment later, and a card without a place cannot take the focus. Tried again
    /// after each layout, a few times, rather than guessed at.
    /// </summary>
    private void FocusCardWhenLaidOut(string? id, int place, int attemptsLeft)
    {
        Dispatcher.UIThread.Post(
            () =>
            {
                if (this.FocusCardOf(id, place) || attemptsLeft <= 0)
                {
                    return;
                }

                void Retry(object? sender, EventArgs args)
                {
                    this.list.LayoutUpdated -= Retry;
                    this.FocusCardWhenLaidOut(id, place, attemptsLeft - 1);
                }

                this.list.LayoutUpdated += Retry;
                this.list.InvalidateMeasure();
            },
            DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Focuses the card of <paramref name="id"/> if it is at <paramref name="place"/>. The old cards
    /// are still there for a moment after a move, the moved one at its old place; focusing one of
    /// those would be undone as they are replaced.
    /// </summary>
    private bool FocusCardOf(string? id, int place)
    {
        foreach (Control container in Enumerable.Range(0, this.list.ItemCount).Select(index => this.list.ContainerFromIndex(index)).OfType<Control>())
        {
            if (this.idOf(container.DataContext) == id
                && this.list.IndexFromContainer(container) == place
                && container.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Classes.Contains("profileCard")) is { } card)
            {
                return card.Focus();
            }
        }

        return false;
    }

    /// <summary>The list's own container for whatever is inside one, if it is inside one.</summary>
    private Control? ContainerOf(Visual inside) =>
        inside.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(control => this.list.IndexFromContainer(control) >= 0);

    private sealed record Slot(Control Container, int Index, double Top, double Height);
}
