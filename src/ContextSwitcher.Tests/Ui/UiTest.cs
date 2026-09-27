using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ContextSwitcher.Tests.Ui;

/// <summary>
/// Drives the real windows with real input. Everything below the view models - templates, styles,
/// bindings, command wiring, hit testing, and the render pass that follows a click - went untested
/// until a thread-affinity crash surfaced in exactly that gap, so these press the buttons instead
/// of calling the commands behind them.
///
/// Avalonia.Headless.XUnit would normally supply [AvaloniaFact]; it pulls xunit v3 and this suite is
/// on v2, so the session it wraps is used directly instead.
/// </summary>
public abstract class UiTest
{
    private static readonly Lazy<HeadlessUnitTestSession> SharedSession =
        new(() => HeadlessUnitTestSession.StartNew(typeof(UiTestApplication)), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Runs <paramref name="body"/> on the headless UI thread.
    ///
    /// The body is synchronous on purpose. The session only pumps the dispatcher for the duration
    /// of the dispatch, so an async body's continuations have nothing to run them once it returns -
    /// and its Func&lt;TResult&gt; overload binds an async lambda as TResult = Task, so awaiting the
    /// result once waits only for the body to be *started*. Both together meant tests passed while
    /// executing nothing past their first await. Use <see cref="PumpUntil"/> for async work instead.
    /// </summary>
    protected static Task OnUiThreadAsync(Action body) =>
        SharedSession.Value.Dispatch(body, CancellationToken.None);

    /// <summary>
    /// Drives the dispatcher until <paramref name="task"/> finishes, so work that hops threads and
    /// posts its continuation back to the UI thread actually gets to run. Rethrows what the task
    /// faulted with.
    /// </summary>
    protected static void PumpUntil(Task task, TimeSpan? timeout = null)
    {
        DateTime deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(2);
        }

        Assert.True(task.IsCompleted, "the awaited work never completed");
        task.GetAwaiter().GetResult();
    }

    /// <summary>Shows a window sized like the real one and lets layout settle.</summary>
    protected static Window ShowWindow(Control content, int width = 880, int height = 900)
    {
        Window window = new() { Width = width, Height = height, Content = content };
        window.Show();
        Settle(window);
        return window;
    }

    /// <summary>Clicks the centre of a control the way a person would, then drains the dispatcher.</summary>
    protected static void Click(Window window, Control control)
    {
        Point? topLeft = control.TranslatePoint(new Point(0, 0), window);
        Assert.True(topLeft.HasValue, "control is not connected to the window");
        Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0, "control has no hit area to click");

        Point centre = topLeft.Value + new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        window.MouseMove(centre, RawInputModifiers.None);
        window.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(centre, MouseButton.Left, RawInputModifiers.None);
        Settle(window);
    }

    /// <summary>Types into a control after focusing it.</summary>
    protected static void Type(Window window, TextBox target, string text)
    {
        target.Focus();
        window.KeyTextInput(text);
        Settle(window);
    }

    /// <summary>Runs pending dispatcher work and forces a render, which is where thread and
    /// binding faults actually surface.</summary>
    protected static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
    }

    protected static T FindControl<T>(Visual root, Func<T, bool> predicate)
        where T : Visual
    {
        T? match = root.GetVisualDescendants().OfType<T>().FirstOrDefault(predicate);
        Assert.NotNull(match);
        return match!;
    }

    /// <summary>Finds a button by the text a person would read on it.</summary>
    protected static Button FindButton(Visual root, string content) =>
        FindControl<Button>(root, b => b.Content as string == content);

    /// <summary>
    /// Finds a button a person could actually click. Several screens keep two variants of a row in
    /// the same grid cell and swap them with IsVisible - the profile rows' Delete and its
    /// "Delete? Yes / No" confirmation, for one - so searching by text alone happily returns a
    /// button that is laid out at zero size and cannot be clicked.
    /// </summary>
    protected static Button FindVisibleButton(Visual root, string content) =>
        FindControl<Button>(root, b => b.Content as string == content && IsClickable(b));

    protected static bool IsClickable(Control control) =>
        control.IsEffectivelyVisible && control.Bounds.Width > 0 && control.Bounds.Height > 0;

    /// <summary>
    /// Opens Profile Setup's collapsible tiers. Their contents are not in the visual tree while
    /// collapsed, so anything inside them is unfindable until this runs.
    /// </summary>
    protected static void ExpandSections(Window window)
    {
        foreach (Expander expander in FindAll<Expander>(window))
        {
            expander.IsExpanded = true;
        }

        Settle(window);
    }

    protected static IReadOnlyList<T> FindAll<T>(Visual root)
        where T : Visual =>
        root.GetVisualDescendants().OfType<T>().ToList();
}
