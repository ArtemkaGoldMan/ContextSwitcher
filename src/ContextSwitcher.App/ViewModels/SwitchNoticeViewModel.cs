using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Contexts;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// The card at the bottom of the main window that says what went wrong in the last switch - the
/// same warnings the dashboard lists, for whoever switches from the window instead.
///
/// It appears only when a switch had problems, and stays until it is closed or the next switch
/// replaces it: a card that faded on a timer could be gone before anyone read it. Only switches made
/// while the app is running count, so problems from before a restart don't greet you on launch -
/// the dashboard still has them.
/// </summary>
public sealed class SwitchNoticeViewModel : ViewModelBase, IDisposable
{
    private readonly DateTimeOffset since;

    private DateTimeOffset? noticeFor;
    private bool isShown;
    private bool isFailure;
    private string title = string.Empty;
    private string timeText = string.Empty;
    private IReadOnlyList<string> messages = [];

    /// <param name="since">When the app started: switches that finished earlier are not news.</param>
    public SwitchNoticeViewModel(DateTimeOffset since)
    {
        this.since = since;
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

    public RelayCommand DismissCommand { get; }

    /// <summary>
    /// Reports a switch that stopped before it recorded anything - rejected because another was
    /// running, say - so there is nothing in the state for the card to read.
    /// </summary>
    public void ShowRejected(string title, string message, DateTimeOffset at)
    {
        this.noticeFor = AppHost.State?.LastSwitchCompletedAt;
        this.Show(title, [message], at, isFailure: true);
    }

    public void Dispose() => AppHost.StateChanged -= this.OnAppStateChanged;

    private void OnAppStateChanged(object? sender, EventArgs e) => this.OnStateChanged(AppHost.State);

    private void OnStateChanged(CurrentContextState? state)
    {
        // The state is published again whenever it is re-read; only a new switch changes the card.
        // Comparing by switch also keeps a closed card closed.
        if (state is null || state.LastSwitchCompletedAt == this.noticeFor)
        {
            return;
        }

        this.noticeFor = state.LastSwitchCompletedAt;

        bool failed = state.LastSwitchStatus == nameof(ContextSwitchStatus.Failed);
        bool warned = state.LastSwitchStatus == nameof(ContextSwitchStatus.SucceededWithWarnings);
        if (state.LastSwitchCompletedAt is not { } completedAt || completedAt < this.since || !(failed || warned))
        {
            this.IsShown = false;
            return;
        }

        string name = AppHost.Configuration?.Contexts.FirstOrDefault(c => c.Id == state.CurrentContextId)?.DisplayName
            ?? state.CurrentContextId;
        this.Show(
            failed ? $"Couldn't switch to {name}" : $"Switched to {name}, with warnings",
            state.LastErrors.Select(error => error.Message).Where(message => !string.IsNullOrWhiteSpace(message)).ToList(),
            completedAt,
            failed);
    }

    private void Show(string title, IReadOnlyList<string> messages, DateTimeOffset at, bool isFailure)
    {
        this.Title = title;
        this.Messages = messages;
        this.TimeText = $"at {at.ToLocalTime():HH:mm}";
        this.IsFailure = isFailure;
        this.IsShown = true;
    }
}
