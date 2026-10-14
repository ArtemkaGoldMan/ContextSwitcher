using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.App.Resources;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Backs the Profiles page (agent.md section 11.1.2): lists every configured context, activates
/// one on click through the same switch pipeline the Dashboard uses, and raises
/// <see cref="EditRequested"/> for Add/Edit so <see cref="MainAppViewModel"/> can open Profile Setup.
/// </summary>
public sealed class ProfilesViewModel : ViewModelBase, IDisposable
{
    private readonly IContextSwitchService switchService;
    private readonly ConfigurationStore configurationStore;
    private readonly IJsonStore jsonStore;
    private readonly ConfigPaths configPaths;
    private readonly bool ownsNotice;

    private IReadOnlyList<ProfileRowViewModel> rows = [];
    private string? errorMessage;

    public ProfilesViewModel(
        IContextSwitchService switchService,
        ConfigurationStore configurationStore,
        IJsonStore jsonStore,
        ConfigPaths configPaths,
        SwitchNoticeViewModel? notice = null)
    {
        this.switchService = switchService;
        this.Notice = notice ?? new SwitchNoticeViewModel(DateTimeOffset.UtcNow);
        this.ownsNotice = notice is null;
        this.configurationStore = configurationStore;
        this.jsonStore = jsonStore;
        this.configPaths = configPaths;

        this.AddCommand = new RelayCommand(() => this.EditRequested?.Invoke(this, null));

        this.Refresh();
        AppHost.ConfigurationChanged += this.OnConfigurationOrStateChanged;
        AppHost.StateChanged += this.OnConfigurationOrStateChanged;
    }

    /// <summary>
    /// Raised for both "Add new profile" (<c>context</c> is <see langword="null"/>) and a row's
    /// Edit action (<c>context</c> is the profile to edit).
    /// </summary>
    public event EventHandler<ContextDefinition?>? EditRequested;

    public IReadOnlyList<ProfileRowViewModel> Rows
    {
        get => this.rows;
        private set
        {
            if (this.SetProperty(ref this.rows, value))
            {
                this.OnPropertyChanged(nameof(this.CanReorder));
            }
        }
    }

    public string? ErrorMessage
    {
        get => this.errorMessage;
        private set
        {
            if (this.SetProperty(ref this.errorMessage, value))
            {
                this.OnPropertyChanged(nameof(this.HasErrorMessage));
            }
        }
    }

    public bool HasErrorMessage => !string.IsNullOrEmpty(this.ErrorMessage);

    /// <summary>Whether there is an order to change: more than one profile.</summary>
    public bool CanReorder => this.Rows.Count > 1;

    /// <summary>What went wrong in the last switch, shown at the bottom of the window.</summary>
    public SwitchNoticeViewModel Notice { get; }

    public RelayCommand AddCommand { get; }

    public void Dispose()
    {
        AppHost.ConfigurationChanged -= this.OnConfigurationOrStateChanged;
        AppHost.StateChanged -= this.OnConfigurationOrStateChanged;
        if (this.ownsNotice)
        {
            this.Notice.Dispose();
        }
    }

    private void OnConfigurationOrStateChanged(object? sender, EventArgs e) => this.Refresh();

    private void Refresh()
    {
        string activeContextId = AppHost.State.CurrentContextId;

        // Cards that already show exactly these profiles are left as they are. A move puts them in
        // their new order at once and the save publishes the same order a moment later: making them
        // all again then would take the keyboard's focus with the old ones for the second time.
        IReadOnlyList<ContextDefinition> current = AppHost.Configuration.Contexts;
        if (this.rows.Count == current.Count
            && this.rows.Select((row, index) => ReferenceEquals(row.Context, current[index]) && row.IsActive == (current[index].Id == activeContextId)).All(same => same))
        {
            return;
        }

        this.Rows = AppHost.Configuration.Contexts
            .Select(context => new ProfileRowViewModel(
                context,
                context.Id == activeContextId,
                this.ToggleAsync,
                context => this.EditRequested?.Invoke(this, context),
                this.DuplicateAsync,
                this.DeleteAsync))
            .ToList();
    }

    /// <summary>The profile clicked comes on - or, if it is the one on, goes off - as in the menu bar popover.</summary>
    private async Task ToggleAsync(ContextDefinition context)
    {
        this.ErrorMessage = null;
        DateTimeOffset? lastSwitchBefore = AppHost.State.LastSwitchCompletedAt;
        ContextSwitchRequest request = ContextSwitchRequest.ForClickOn(context.Id, AppHost.State.CurrentContextId, ContextSwitchSource.Dashboard);
        string couldNot = request.IsTurnOff
            ? Localized.Format(Strings.Notice_CouldNotTurnOff, context.DisplayName)
            : Localized.Format(Strings.Notice_CouldNotSwitchTo, context.DisplayName);

        ContextSwitchResult result = await this.switchService
            .SwitchAsync(request, CancellationToken.None)
            .ConfigureAwait(true);

        CurrentContextState? state = await this.jsonStore
            .ReadAsync<CurrentContextState>(this.configPaths.StatePath)
            .ConfigureAwait(true);
        AppHost.UpdateState(state ?? new CurrentContextState());

        // A switch that ran shows its own warnings in the notice, from the state it wrote. These two
        // never got that far, so the notice is told directly.
        if (result.Status is ContextSwitchStatus.Cancelled)
        {
            this.Notice.ShowRejected(couldNot, Strings.Notice_AnotherSwitchRunning, result.CompletedAt);
        }
        else if (result.Status is ContextSwitchStatus.Failed && AppHost.State.LastSwitchCompletedAt == lastSwitchBefore)
        {
            this.Notice.ShowRejected(couldNot, Strings.Notice_SwitchStopped, result.CompletedAt);
        }
    }

    /// <summary>
    /// Puts a profile at <paramref name="newIndex"/> in the list, and so everywhere the list is shown:
    /// the menu bar menu and the dashboard both read the profiles in this order. Done in the app only
    /// - nothing else offers it.
    ///
    /// The cards change places at once, before the order is saved: a card let go of at its new place
    /// would otherwise spring back for the moment the save takes, and then jump.
    /// </summary>
    public async Task MoveAsync(string contextId, int newIndex)
    {
        List<ContextDefinition> contexts = [.. AppHost.Configuration.Contexts];
        int from = contexts.FindIndex(context => context.Id == contextId);
        int to = Math.Clamp(newIndex, 0, Math.Max(contexts.Count - 1, 0));
        if (from < 0 || from == to)
        {
            return;
        }

        this.ErrorMessage = null;
        ContextDefinition moved = contexts[from];
        contexts.RemoveAt(from);
        contexts.Insert(to, moved);

        List<ProfileRowViewModel> reordered = [.. this.rows];
        ProfileRowViewModel row = reordered[from];
        reordered.RemoveAt(from);
        reordered.Insert(to, row);
        this.Rows = reordered;

        await this.SaveAsync(AppHost.Configuration with { Contexts = contexts }).ConfigureAwait(true);

        // A save that did not happen leaves the cards in an order that is not the profiles'.
        if (this.HasErrorMessage)
        {
            this.Refresh();
        }
    }

    /// <summary>One place up (<paramref name="delta"/> -1) or down (+1): the keyboard's way of what dragging does.</summary>
    public Task MoveByAsync(string contextId, int delta)
    {
        int from = AppHost.Configuration.Contexts.ToList().FindIndex(context => context.Id == contextId);
        return from < 0 ? Task.CompletedTask : this.MoveAsync(contextId, from + delta);
    }

    private async Task DuplicateAsync(ContextDefinition context)
    {
        this.ErrorMessage = null;

        string newId = GenerateUniqueId(context.Id, AppHost.Configuration.Contexts);
        ContextDefinition duplicate = context with { Id = newId, DisplayName = Localized.Format(Strings.Profiles_CopyName, context.DisplayName) };

        AppConfiguration updated = AppHost.Configuration with
        {
            Contexts = [.. AppHost.Configuration.Contexts, duplicate]
        };

        await this.SaveAsync(updated).ConfigureAwait(true);
    }

    private async Task DeleteAsync(ContextDefinition context)
    {
        this.ErrorMessage = null;

        if (context.Id == AppHost.State.CurrentContextId)
        {
            this.ErrorMessage = Localized.Format(Strings.Profiles_CannotDeleteActive, context.DisplayName);
            return;
        }

        if (AppHost.Configuration.Contexts.Count <= 1)
        {
            this.ErrorMessage = Strings.Profiles_AtLeastOne;
            return;
        }

        AppConfiguration updated = AppHost.Configuration with
        {
            Contexts = AppHost.Configuration.Contexts.Where(c => c.Id != context.Id).ToList()
        };

        await this.SaveAsync(updated).ConfigureAwait(true);
    }

    private async Task SaveAsync(AppConfiguration updated)
    {
        ConfigurationSaveResult result = await this.configurationStore.SaveAsync(updated, CancellationToken.None).ConfigureAwait(true);
        if (!result.Succeeded)
        {
            this.ErrorMessage = string.Join(" ", result.Errors);
        }
    }

    private static string GenerateUniqueId(string baseId, IReadOnlyList<ContextDefinition> existing)
    {
        HashSet<string> existingIds = existing.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        string candidate = $"{baseId}-copy";
        int suffix = 2;
        while (existingIds.Contains(candidate))
        {
            candidate = $"{baseId}-copy-{suffix}";
            suffix++;
        }

        return candidate;
    }
}
