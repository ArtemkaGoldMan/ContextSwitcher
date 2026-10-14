using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;

namespace ContextSwitcher.Tests.App.ViewModels;

/// <summary>
/// The window asking about everything else open: everything starts kept, each row offers the
/// profiles it can be added to, and more than 13 tabs are one line - keep or close them all.
/// </summary>
public sealed class AskOnSwitchViewModelTests
{
    private static readonly ContextDefinition Work = new() { Id = "work", DisplayName = "Work" };

    private static readonly ContextDefinition Study = new()
    {
        Id = "study",
        DisplayName = "Study",
        BrowserManagement = new BrowserManagementConfig { Mode = BrowserManagementMode.Profiles }
    };

    [Fact]
    public void EverythingStartsKeptAndEachRowOffersWhereItCanGo()
    {
        Unclaimed unclaimed = new([new RunningApp("Figma")], [new OpenBrowserTab(BrowserKind.Chrome, "Lo-fi", "https://www.youtube.com/watch?v=1")]);

        AskOnSwitchViewModel viewModel = new(unclaimed, Work, Study);

        Assert.Equal("Switching from Work to Study", viewModel.Title);
        UnclaimedRowViewModel figma = Assert.Single(viewModel.Apps);
        Assert.Equal(UnclaimedChoice.Keep, figma.Choice);
        Assert.Equal(["Keep", "Close", "+ Work", "+ Study"], figma.Options.Select(option => option.Label));

        // Study opens browser profiles, so it takes no pages.
        UnclaimedRowViewModel tab = Assert.Single(viewModel.Tabs);
        Assert.Equal(["Keep", "Close", "+ Work"], tab.Options.Select(option => option.Label));
        Assert.Equal("youtube.com", tab.Subtitle);
    }

    [Fact]
    public void TurningAProfileOnOrOffOffersOnlyThatProfile()
    {
        Unclaimed unclaimed = new([new RunningApp("Figma")], []);

        AskOnSwitchViewModel off = new(unclaimed, Work, null);
        AskOnSwitchViewModel on = new(unclaimed, null, Work);

        Assert.Equal("Turning Work off", off.Title);
        Assert.Equal(["Keep", "Close", "+ Work"], off.Apps[0].Options.Select(option => option.Label));
        Assert.Equal("Turning Work on", on.Title);
        Assert.Equal(UnclaimedChoice.AddToEntering, on.Apps[0].Options[2].Value);
        Assert.False(on.HasTabs);
    }

    [Fact]
    public async Task MoreThanThirteenTabsAreOneLineToKeepOrCloseThemAll()
    {
        List<OpenBrowserTab> tabs = Enumerable.Range(1, 14).Select(i => new OpenBrowserTab(BrowserKind.Chrome, $"Tab {i}", $"https://example.com/{i}")).ToList();
        AskOnSwitchViewModel viewModel = new(new Unclaimed([], tabs), Work, null);

        Assert.True(viewModel.HasManyTabs);
        Assert.Empty(viewModel.Tabs);
        Assert.Equal("All tabs: 14", viewModel.AllTabs!.Title);
        Assert.Equal(["Keep", "Close"], viewModel.AllTabs.Options.Select(option => option.Label));

        viewModel.AllTabs.Options[1].SelectCommand.Execute(null);
        viewModel.ContinueCommand.Execute(null);

        AskOnSwitchResult result = await viewModel.Completion;
        Assert.All(result.Answers.Tabs, answer => Assert.Equal(UnclaimedChoice.Close, answer.Choice));
        Assert.Equal(14, result.Answers.ToExtras().CloseTabs.Count);

        Assert.False(new AskOnSwitchViewModel(new Unclaimed([], tabs.Take(13).ToList()), Work, null).HasManyTabs);
    }

    [Fact]
    public void CloseAllAndKeepAllChooseForEveryRow()
    {
        Unclaimed unclaimed = new([new RunningApp("Figma"), new RunningApp("Notes")], [new OpenBrowserTab(BrowserKind.Safari, "News", "https://news.example/")]);
        AskOnSwitchViewModel viewModel = new(unclaimed, Work, null);
        viewModel.Apps[1].Options[2].SelectCommand.Execute(null);

        viewModel.CloseAllCommand.Execute(null);
        Assert.All(viewModel.Apps.Concat(viewModel.Tabs), row => Assert.Equal(UnclaimedChoice.Close, row.Choice));
        Assert.True(viewModel.Apps[0].Options[1].IsSelected);

        viewModel.KeepAllCommand.Execute(null);
        Assert.All(viewModel.Apps.Concat(viewModel.Tabs), row => Assert.Equal(UnclaimedChoice.Keep, row.Choice));
    }

    [Fact]
    public async Task ContinueGivesTheChoicesAndSkipGivesNone()
    {
        Unclaimed unclaimed = new([new RunningApp("Figma"), new RunningApp("Notes")], []);
        AskOnSwitchViewModel answered = new(unclaimed, Work, null);
        answered.Apps[0].Options[1].SelectCommand.Execute(null);
        answered.Apps[1].Options[2].SelectCommand.Execute(null);
        answered.DontAskAgain = true;

        answered.ContinueCommand.Execute(null);

        AskOnSwitchResult result = await answered.Completion;
        Assert.False(result.Skipped);
        Assert.True(result.DontAskAgain);
        Assert.Equal([UnclaimedChoice.Close, UnclaimedChoice.AddToLeaving], result.Answers.Apps.Select(answer => answer.Choice));

        AskOnSwitchViewModel skipped = new(unclaimed, Work, null);
        skipped.Apps[0].Options[1].SelectCommand.Execute(null);
        skipped.SkipCommand.Execute(null);
        skipped.ContinueCommand.Execute(null);
        AskOnSwitchResult skip = await skipped.Completion;
        Assert.True(skip.Skipped);
        Assert.Empty(skip.Answers.Apps);
    }
}
