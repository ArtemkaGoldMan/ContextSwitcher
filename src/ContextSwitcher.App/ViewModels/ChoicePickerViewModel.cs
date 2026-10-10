namespace ContextSwitcher.App.ViewModels;

/// <summary>Something a picker can offer: the value stored, and how it is shown.</summary>
/// <param name="Value">What gets added when it is picked - a URL, a container name.</param>
/// <param name="Title">The main line, such as a page title.</param>
/// <param name="Detail">A quieter second line, such as the site; empty for none.</param>
public sealed record PickerChoice(string Value, string Title, string Detail = "");

/// <summary>One clickable entry in a <see cref="ChoicePickerViewModel"/>'s list.</summary>
public sealed class PickerItemViewModel(PickerChoice choice, string glyphKey, Action<PickerChoice> pick)
{
    public string Title { get; } = choice.Title;

    /// <summary>The icon resource drawn in the entry's leading slot.</summary>
    public string GlyphKey { get; } = glyphKey;

    public string Detail { get; } = choice.Detail;

    public bool HasDetail => this.Detail.Length > 0;

    public RelayCommand PickCommand { get; } = new(() => pick(choice));
}

/// <summary>
/// A list to choose from that is read when it is opened rather than when the page is - reading it
/// can mean scripting a browser or asking Docker, which should happen because the user asked, not
/// every time Profile Setup opens. Entries already added are left out, and drop out as they are
/// picked.
/// </summary>
public sealed class ChoicePickerViewModel : ViewModelBase
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<PickerChoice>?>> load;
    private readonly Func<IEnumerable<string>> alreadyAdded;
    private readonly Action<PickerChoice> pick;

    private IReadOnlyList<PickerChoice>? loaded;
    private IReadOnlyList<PickerItemViewModel> items = [];
    private string searchText = string.Empty;
    private bool isLoading;
    private bool hasLoaded;

    /// <param name="load">Reads the choices, or returns <see langword="null"/> when their source is unavailable.</param>
    /// <param name="alreadyAdded">Values already chosen, re-read on every refresh.</param>
    /// <param name="pick">Adds a chosen entry.</param>
    /// <param name="emptyText">Shown when there is nothing (left) to pick.</param>
    /// <param name="unavailableText">Shown when the source could not be read.</param>
    /// <param name="glyphKey">The icon resource drawn beside every entry - a globe for pages, a box for containers.</param>
    /// <param name="searchPlaceholder">The search box's placeholder, such as "Search open tabs…".</param>
    public ChoicePickerViewModel(
        Func<CancellationToken, Task<IReadOnlyList<PickerChoice>?>> load,
        Func<IEnumerable<string>> alreadyAdded,
        Action<PickerChoice> pick,
        string emptyText,
        string unavailableText,
        string glyphKey,
        string searchPlaceholder)
    {
        this.GlyphKey = glyphKey;
        this.SearchPlaceholder = searchPlaceholder;
        this.load = load;
        this.alreadyAdded = alreadyAdded;
        this.pick = pick;
        this.EmptyText = emptyText;
        this.UnavailableText = unavailableText;
        this.LoadCommand = new RelayCommand(() => _ = this.LoadAsync());
    }

    public string EmptyText { get; }

    public string GlyphKey { get; }

    public string SearchPlaceholder { get; }

    /// <summary>Narrows the list by title or detail, the same search every picker has.</summary>
    public string SearchText
    {
        get => this.searchText;
        set
        {
            if (this.SetProperty(ref this.searchText, value ?? string.Empty))
            {
                this.Refresh();
            }
        }
    }

    public string UnavailableText { get; }

    /// <summary>Bound to the button that opens the picker, so every opening reads a fresh list.</summary>
    public RelayCommand LoadCommand { get; }

    public IReadOnlyList<PickerItemViewModel> Items
    {
        get => this.items;
        private set
        {
            if (this.SetProperty(ref this.items, value))
            {
                this.OnPropertyChanged(nameof(this.IsEmpty));
            }
        }
    }

    public bool IsLoading
    {
        get => this.isLoading;
        private set
        {
            if (this.SetProperty(ref this.isLoading, value))
            {
                this.OnPropertyChanged(nameof(this.IsUnavailable));
                this.OnPropertyChanged(nameof(this.IsEmpty));
            }
        }
    }

    /// <summary>The source could not be read at all - as opposed to having nothing in it.</summary>
    public bool IsUnavailable => this.hasLoaded && !this.IsLoading && this.loaded is null;

    /// <summary>The source was read and has nothing left to offer, or nothing matching the search.</summary>
    public bool IsEmpty => this.hasLoaded && !this.IsLoading && this.loaded is not null && this.Items.Count == 0;

    /// <summary>Reads the choices. Never throws: a failure reads as unavailable.</summary>
    public async Task LoadAsync()
    {
        this.IsLoading = true;
        try
        {
            this.loaded = await this.load(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception)
        {
            this.loaded = null;
        }
        finally
        {
            this.hasLoaded = true;
            this.IsLoading = false;
            this.Refresh();
        }
    }

    /// <summary>Re-applies the "already added" filter. Call after the caller's list changes.</summary>
    public void Refresh()
    {
        HashSet<string> added = this.alreadyAdded().ToHashSet(StringComparer.Ordinal);
        string search = this.SearchText.Trim();
        this.Items = (this.loaded ?? [])
            .Where(choice => !added.Contains(choice.Value))
            .Where(choice => search.Length == 0
                || choice.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || choice.Detail.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Select(choice => new PickerItemViewModel(choice, this.GlyphKey, this.Pick))
            .ToList();
        this.OnPropertyChanged(nameof(this.IsUnavailable));
        this.OnPropertyChanged(nameof(this.IsEmpty));
    }

    private void Pick(PickerChoice choice)
    {
        this.pick(choice);
        this.Refresh();
    }
}
