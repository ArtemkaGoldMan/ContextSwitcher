using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.App.Views;

/// <summary>
/// The menu bar popover. It behaves like one: a click anywhere outside it - another app, the desktop,
/// the menu bar icon - Escape, its close button, opening the main app, or a clean switch dismisses
/// it. Dismissing hides rather than closes, so reopening it is instant and it keeps its state - the
/// last switch's warnings among it.
/// </summary>
public sealed partial class DashboardWindow : Window
{
    /// <summary>
    /// The transparent margin around the card that its shadow is drawn into - its blur on every side,
    /// and its downward offset at the bottom. Positioning allows for it, so the card itself lands
    /// where it always did.
    /// </summary>
    public static readonly Thickness ShadowRoom = new(16, 10, 16, 22);

    private readonly Action openMainApp = () => { };
    private DashboardViewModel? viewModel;
    private DispatcherTimer? frontmostWatch;
    private int? frontmostBaseline;
    private bool wasMouseDown;

    public DashboardWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <param name="openMainApp">
    /// Opens the main window. The app owns that window, since the menu bar's own "Open App" opens it
    /// too, and two owners meant two windows.
    /// </param>
    public DashboardWindow(DashboardViewModel viewModel, Action openMainApp)
        : this()
    {
        this.openMainApp = openMainApp;
        this.viewModel = viewModel;
        DataContext = viewModel;
        viewModel.OpenAppRequested += this.OnOpenAppRequested;
        viewModel.DismissRequested += this.OnDismissRequested;
        this.Deactivated += this.OnDeactivated;
        this.Closed += this.OnClosed;
        this.KeyDown += this.OnKeyDown;
    }

    private void OnOpenAppRequested(object? sender, EventArgs e)
    {
        this.openMainApp();

        // The main window taking focus would dismiss the popover anyway; saying so explicitly keeps
        // that from depending on activation order.
        this.Hide();
    }

    /// <summary>
    /// Shows the popover and starts watching which app is frontmost while it is open, so it closes
    /// when the user clicks somewhere else even if it never had focus to lose.
    /// </summary>
    public void ShowAsPopover()
    {
        this.Show();
        this.Activate();

        this.frontmostBaseline = FrontmostApplication.ProcessId();

        // A button still held from the click that opened the popover is not a new press; unknown
        // counts as held for the same reason.
        this.wasMouseDown = OutsideClick.IsMouseDown() ?? true;
        this.frontmostWatch ??= CreateFrontmostWatch();
        this.frontmostWatch.Start();
    }

    /// <summary>
    /// Checks, every 50ms while the popover is open, for a fresh mouse press outside its card, and
    /// for another app coming to the front. 50ms because a quick click holds the button for not much
    /// more than that; each check is two calls into AppKit, nothing that costs anything.
    /// </summary>
    private DispatcherTimer CreateFrontmostWatch()
    {
        DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            if (!this.IsVisible)
            {
                timer.Stop();
                return;
            }

            (bool dismiss, this.frontmostBaseline) = FrontmostApplication.Evaluate(
                this.frontmostBaseline, FrontmostApplication.ProcessId(), Environment.ProcessId);

            if (dismiss || this.WasPressedOutside())
            {
                timer.Stop();
                this.Hide();
            }
        };
        return timer;
    }

    /// <summary>Whether the mouse has just gone down outside the card - in AppKit's own coordinates.</summary>
    private bool WasPressedOutside()
    {
        bool? isDown = OutsideClick.IsMouseDown();
        if (isDown is null)
        {
            return false;
        }

        bool wasDown = this.wasMouseDown;
        this.wasMouseDown = isDown.Value;
        if (!isDown.Value || wasDown)
        {
            return false;
        }

        IntPtr nsWindow = this.TryGetPlatformHandle() is { HandleDescriptor: "NSWindow" } handle ? handle.Handle : IntPtr.Zero;
        if (OutsideClick.WindowFrame(nsWindow) is not { } frame || OutsideClick.PointerLocation() is not { } pointer)
        {
            return false;
        }

        // The card, not the transparent room around it that its shadow is drawn into.
        ScreenRect card = frame.Inset(ShadowRoom.Left, ShadowRoom.Top, ShadowRoom.Right, ShadowRoom.Bottom);
        return OutsideClick.IsPressOutside(wasDown, isDown.Value, pointer, card);
    }

    private void OnKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Escape)
        {
            e.Handled = true;
            this.Hide();
        }
    }

    private void OnDismissRequested(object? sender, EventArgs e) => this.Hide();

    // Light dismiss: focus went somewhere else - the desktop, another app, the main window.
    private void OnDeactivated(object? sender, EventArgs e) => this.Hide();

    private void OnClosed(object? sender, EventArgs e)
    {
        this.frontmostWatch?.Stop();
        this.Deactivated -= this.OnDeactivated;
        this.KeyDown -= this.OnKeyDown;
        if (this.viewModel is not null)
        {
            this.viewModel.OpenAppRequested -= this.OnOpenAppRequested;
            this.viewModel.DismissRequested -= this.OnDismissRequested;
            this.viewModel.Dispose();
            this.viewModel = null;
        }
    }
}
