using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;

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
    private readonly IPopoverWindow? popoverWindow;
    private readonly ILogger? log;
    private DashboardViewModel? viewModel;
    private DispatcherTimer? frontmostWatch;
    private int? frontmostBaseline;
    private bool wasMouseDown;

    public DashboardWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>The popover for <paramref name="viewModel"/>, as the app shows it.</summary>
    /// <param name="openMainApp">
    /// Opens the main window. The app owns that window, since the menu bar's own "Open App" opens it
    /// too, and two owners meant two windows.
    /// </param>
    /// <param name="popover">How the window shows as a popover, and what is asked to tell a click elsewhere.</param>
    public DashboardWindow(DashboardViewModel viewModel, Action openMainApp, IPopoverWindow popover, ILogger logger)
        : this()
    {
        this.openMainApp = openMainApp;
        this.popoverWindow = popover;
        this.log = logger;
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

    // The elapsed time ticks only while the popover is showing: hidden, it stays alive for the next
    // click, and a clock running inside it woke the app for text nobody could read.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && this.viewModel is { } model)
        {
            if (this.IsVisible)
            {
                model.StartClock();
            }
            else
            {
                model.StopClock();
            }
        }
    }

    /// <summary>
    /// Shows the popover and starts watching which app is frontmost while it is open, so it closes
    /// when the user clicks somewhere else even if it never had focus to lose.
    /// </summary>
    public void ShowAsPopover()
    {
        // Only the designer makes one without these, and never shows it as a popover.
        if (this.popoverWindow is not { } popover || this.log is not { } logger)
        {
            this.Show();
            return;
        }

        // On whichever desktop the user is on, and over a full-screen app: by default a window goes
        // back to the Space it was first shown on, so the popover opened out of sight. And without
        // bringing the app forward, as a menu bar popover should: an app with a Dock icon coming
        // forward made macOS leave a full-screen app for a desktop. So it opens without focus -
        // taking focus activated the app, whatever the window. A click in it gives it focus. Should
        // the window not be one that can open without focus, it is shown and activated as an
        // ordinary window, as before.
        bool panel = popover.Prepare(this.NativeWindow());
        this.ShowActivated = !panel;
        this.Show();
        if (!panel)
        {
            this.Activate();
        }

        // Once as it opens, and again once whatever macOS does in response has happened.
        if (DiagnosingWindows)
        {
            this.LogSpace(popover, logger);
            DispatcherTimer.RunOnce(() => this.LogSpace(popover, logger), TimeSpan.FromSeconds(1));
        }

        this.frontmostBaseline = popover.FrontmostProcessId();

        // A button still held from the click that opened the popover is not a new press; unknown
        // counts as held for the same reason.
        this.wasMouseDown = popover.IsMouseDown() ?? true;
        this.frontmostWatch ??= this.CreateFrontmostWatch(popover);
        this.frontmostWatch.Start();
    }

    /// <summary>
    /// <c>CONTEXTSWITCHER_DIAGNOSE=windows</c> logs, as the popover opens, which Spaces it may appear
    /// on, whether it opened on the one in front, and whether the app came forward - what a report of
    /// it "not opening", or of being thrown onto another desktop, comes down to.
    /// </summary>
    private static bool DiagnosingWindows => Environment.GetEnvironmentVariable("CONTEXTSWITCHER_DIAGNOSE") == "windows";

    private void LogSpace(IPopoverWindow popover, ILogger logger)
    {
        if (popover.Describe(this.NativeWindow()) is not { } description)
        {
            return;
        }

        _ = logger.LogAsync(new LogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = LogLevel.Information,
            Category = "Diagnostics",
            EventId = "DashboardShown",
            Message = $"Dashboard shown: {description}."
        });
    }

    private IntPtr NativeWindow() =>
        this.TryGetPlatformHandle() is { HandleDescriptor: "NSWindow" } handle ? handle.Handle : IntPtr.Zero;

    /// <summary>
    /// Checks, every 50ms while the popover is open, for a fresh mouse press outside its card, and
    /// for another app coming to the front. 50ms because a quick click holds the button for not much
    /// more than that; each check is two calls into AppKit, nothing that costs anything.
    /// </summary>
    private DispatcherTimer CreateFrontmostWatch(IPopoverWindow popover)
    {
        DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            if (!this.IsVisible)
            {
                timer.Stop();
                return;
            }

            (bool dismiss, this.frontmostBaseline) = PopoverDismissal.AfterFrontmostLook(
                this.frontmostBaseline, popover.FrontmostProcessId(), Environment.ProcessId);

            if (dismiss || this.WasPressedOutside(popover))
            {
                timer.Stop();
                this.Hide();
            }
        };
        return timer;
    }

    /// <summary>Whether the mouse has just gone down outside the card.</summary>
    private bool WasPressedOutside(IPopoverWindow popover)
    {
        bool? isDown = popover.IsMouseDown();
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

        // The card, not the transparent room around it that its shadow is drawn into.
        bool? outside = popover.IsPointerOutside(this.NativeWindow(), ShadowRoom.Left, ShadowRoom.Top, ShadowRoom.Right, ShadowRoom.Bottom);
        return outside is { } pointerOutside && PopoverDismissal.IsPressOutside(wasDown, isDown.Value, pointerOutside);
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
