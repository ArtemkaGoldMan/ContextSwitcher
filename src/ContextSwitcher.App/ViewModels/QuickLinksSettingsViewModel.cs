using System.Collections.ObjectModel;
using ContextSwitcher.App.Resources;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App.ViewModels;

/// <summary>Profile Setup's quick links: the pages the dashboard offers while the profile is on.</summary>
public sealed class QuickLinksSettingsViewModel : ViewModelBase
{
    /// <param name="openTabs">The tabs open in the profile's browser, to pick a link from.</param>
    public QuickLinksSettingsViewModel(Func<CancellationToken, Task<IReadOnlyList<PickerChoice>?>> openTabs, IReadOnlyList<QuickLinkConfig> source)
    {
        this.QuickLinks = new ObservableCollection<QuickLinkRowViewModel>(
            source.Select(link => new QuickLinkRowViewModel(link.Title, link.Url, link.Icon, this.RemoveQuickLink)));
        this.QuickLinkTabPicker = new ChoicePickerViewModel(
            openTabs,
            () => this.QuickLinks.Select(row => row.Url.Trim()),
            choice => this.QuickLinks.Add(new QuickLinkRowViewModel(choice.Title, choice.Value, "link", this.RemoveQuickLink)),
            Strings.Picker_OpenTabsEmpty,
            Strings.Picker_OpenTabsUnavailable,
            "IconGlobe",
            Strings.Picker_SearchOpenTabs);
        this.AddQuickLinkCommand = new RelayCommand(() => this.QuickLinks.Add(new QuickLinkRowViewModel(string.Empty, string.Empty, "link", this.RemoveQuickLink)));
    }

    public ObservableCollection<QuickLinkRowViewModel> QuickLinks { get; }

    /// <summary>"From open tabs" under Quick links: picking a tab fills in both its title and URL.</summary>
    public ChoicePickerViewModel QuickLinkTabPicker { get; }

    public RelayCommand AddQuickLinkCommand { get; }

    /// <summary>The links as the profile keeps them: those with an address, each with a title and an icon.</summary>
    public IReadOnlyList<QuickLinkConfig> ToConfig() => this.QuickLinks
        .Where(q => !string.IsNullOrWhiteSpace(q.Url))
        .Select(q => new QuickLinkConfig
        {
            // The dashboard shows a link by its title alone, so an untitled one would be a blank
            // button; the site's name is the obvious stand-in.
            Title = string.IsNullOrWhiteSpace(q.Title) ? BrowserSettingsViewModel.HostOf(q.Url.Trim()) : q.Title.Trim(),
            Url = q.Url.Trim(),
            Icon = string.IsNullOrWhiteSpace(q.Icon) ? "link" : q.Icon.Trim()
        })
        .ToList();

    private void RemoveQuickLink(QuickLinkRowViewModel row)
    {
        this.QuickLinks.Remove(row);
        this.QuickLinkTabPicker.Refresh();
    }
}
