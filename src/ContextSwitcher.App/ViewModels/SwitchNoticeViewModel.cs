using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.App.Resources;
using ContextSwitcher.Core.Messages;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// The card at the bottom of the main window that says what went wrong in the last switch - the
/// same warnings the dashboard lists, for whoever switches from the window instead.
///
/// It appears only when a switch had problems, and stays until it is closed or the next switch
/// replaces it: a card that faded on a timer could be gone before anyone read it. Only switches made
/// while the app is running count, so problems from before a restart don't greet you on launch -
/// the dashboard still has them.
///
/// A switch's lines can change after it - an app that was still closing quits, or stays on in the
/// background (<see cref="ClosingAppsFollowUp"/>) - and the card follows them while it is open; with
/// none left it goes. An app still running with no window on screen has a "Force quit" next to it.
/// </summary>
public sealed class SwitchNoticeViewModel : ViewModelBase, IDisposable
{
    private readonly DateTimeOffset since;
    private readonly Func<DateTimeOffset, string, Task>? forceQuit;

    private DateTimeOffset? noticeFor;
    private bool isShown;
    private bool isFailure;
    private string title = string.Empty;
    private string timeText = string.Empty;
    private IReadOnlyList<string> messages = [];
    private IReadOnlyList<NoticeLine> lines = [];
    private bool showsRejection;

    /// <param name="since">When the app started: switches that finished earlier are not news.</param>
    /// <param name="forceQuit">Ends an app by force, as asked from a line: the switch's completion time and the app.</param>
    public SwitchNoticeViewModel(DateTimeOffset since, Func<DateTimeOffset, string, Task>? forceQuit = null)
    {
        this.since = since;
        this.forceQuit = forceQuit;
        this.DismissCommand = new RelayCommand(() => this.IsShown = false);

        this.OnStateChanged(AppHost.State);
        AppHost.StateChanged += this.OnAppStateChanged;
    }

    public bool IsShown
    {
        get => this.isShown;
        private set => this.SetProperty(ref this.isShown, value);
    }

    /// <summary>The switch did not happen, as opposed to happening with some steps going wrong.</summary>
    public bool IsFailure
    {
        get => this.isFailure;
        private set => this.SetProperty(ref this.isFailure, value);
    }

    public string Title
    {
        get => this.title;
        private set => this.SetProperty(ref this.title, value);
    }

    /// <summary>When it happened, e.g. "at 14:32".</summary>
    public string TimeText
    {
        get => this.timeText;
        private set => this.SetProperty(ref this.timeText, value);
    }

    public IReadOnlyList<string> Messages
    {
        get => this.messages;
        private set => this.SetProperty(ref this.messages, value);
    }

    /// <summary>The same as <see cref="Messages"/>, each with what can be done about it.</summary>
    public IReadOnlyList<NoticeLine> Lines
    {
        get => this.lines;
        private set => this.SetProperty(ref this.lines, value);
    }

    public RelayCommand DismissCommand { get; }

    /// <summary>
    /// Reports a switch that stopped before it recorded anything - rejected because another was
    /// running, say - so there is nothing in the state for the card to read.
    /// </summary>
    public void ShowRejected(string title, string message, DateTimeOffset at)
    {
        this.noticeFor = AppHost.State?.LastSwitchCompletedAt;
        this.Show(title, [new NoticeLine(message)], at, isFailure: true);
        this.showsRejection = true;
    }

    public void Dispose() => AppHost.StateChanged -= this.OnAppStateChanged;

    private void OnAppStateChanged(object? sender, EventArgs e) => this.OnStateChanged(AppHost.State);

    private void OnStateChanged(CurrentContextState? state)
    {
        if (state is null)
        {
            return;
        }

        // The state is published again whenever it is re-read; only a new switch makes a new card,
        // which also keeps a closed card closed. The same switch can come back with its lines
        // changed, which an open card shows - unless it is saying a switch was turned away.
        if (state.LastSwitchCompletedAt == this.noticeFor)
        {
            if (this.IsShown && !this.showsRejection)
            {
                this.Follow(state);
            }

            return;
        }

        this.noticeFor = state.LastSwitchCompletedAt;
        this.showsRejection = false;

        bool failed = state.LastSwitchStatus == nameof(ContextSwitchStatus.Failed);
        bool warned = state.LastSwitchStatus == nameof(ContextSwitchStatus.SucceededWithWarnings);
        if (state.LastSwitchCompletedAt is not { } completedAt || completedAt < this.since || !(failed || warned))
        {
            this.IsShown = false;
            return;
        }

        // With no profile current, the active one was turned off: the problems are from leaving it.
        bool turnedOff = string.IsNullOrEmpty(state.CurrentContextId);
        string subjectId = (turnedOff ? state.PreviousContextId : state.CurrentContextId) ?? string.Empty;
        string name = AppHost.Configuration?.Contexts.FirstOrDefault(c => c.Id == subjectId)?.DisplayName ?? subjectId;
        string title = (turnedOff, failed) switch
        {
            (true, true) => Localized.Format(Strings.Notice_CouldNotTurnOff, name),
            (true, false) => Localized.Format(Strings.Notice_TurnedOffWithWarnings, name),
            (false, true) => Localized.Format(Strings.Notice_CouldNotSwitchTo, name),
            (false, false) => Localized.Format(Strings.Notice_SwitchedWithWarnings, name)
        };
        this.Show(title, this.LinesOf(state.LastErrors, completedAt), completedAt, failed);
    }

    /// <summary>The open card's switch, with its lines as they are now; with no problem left, it goes.</summary>
    private void Follow(CurrentContextState state)
    {
        bool stillWrong = state.LastSwitchStatus is nameof(ContextSwitchStatus.Failed) or nameof(ContextSwitchStatus.SucceededWithWarnings);
        IReadOnlyList<NoticeLine> now = state.LastSwitchCompletedAt is { } completedAt ? this.LinesOf(state.LastErrors, completedAt) : [];
        if (!stillWrong || now.Count == 0)
        {
            this.IsShown = false;
            return;
        }

        if (!now.Select(line => line.Text).SequenceEqual(this.Messages))
        {
            this.Lines = now;
            this.Messages = now.Select(line => line.Text).ToList();
        }
    }

    private IReadOnlyList<NoticeLine> LinesOf(IReadOnlyList<StateError> errors, DateTimeOffset completedAt) =>
        errors
            .Select(error => (Error: error, Text: MessageText.Of(error)))
            .Where(line => !string.IsNullOrWhiteSpace(line.Text))
            .Select(line => ClosingApps.ForceQuittable(line.Error) is { } app && this.forceQuit is { } quit
                ? new NoticeLine(line.Text, app, () => quit(completedAt, app))
                : new NoticeLine(line.Text))
            .ToList();

    private void Show(string title, IReadOnlyList<NoticeLine> lines, DateTimeOffset at, bool isFailure)
    {
        this.Title = title;
        this.Lines = lines;
        this.Messages = lines.Select(line => line.Text).ToList();
        this.TimeText = Localized.Format(Strings.Notice_At, at.ToLocalTime());
        this.IsFailure = isFailure;
        this.IsShown = true;
    }
}

/// <summary>
/// A line on the card: what went wrong, and, for an app still running minutes after a switch with no
/// window on screen, a way to end it - which it only ever is when this is clicked.
/// </summary>
public sealed class NoticeLine
{
    public NoticeLine(string text, string? app = null, Func<Task>? forceQuit = null)
    {
        this.Text = text;
        if (app is not null && forceQuit is not null)
        {
            this.ForceQuitName = Localized.Format(Strings.Notice_ForceQuitApp, app);
            this.ForceQuitCommand = new AsyncRelayCommand(forceQuit);
        }
    }

    public string Text { get; }

    public bool CanForceQuit => this.ForceQuitCommand is not null;

    public AsyncRelayCommand? ForceQuitCommand { get; }

    /// <summary>"Force quit Steam", for VoiceOver: every such button looks the same.</summary>
    public string? ForceQuitName { get; }
}
