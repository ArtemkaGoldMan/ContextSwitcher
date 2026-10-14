using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;

namespace ContextSwitcher.Tests.Contexts;

/// <summary>
/// The answers to "what about everything else open?": closing goes to the switch, and adding makes
/// the app or page a full part of the profile - opened on the way in, closed on the way out.
/// </summary>
public sealed class UnclaimedAnswersTests
{
    private static readonly AppConfiguration Configuration = new()
    {
        ActiveContextId = "work",
        Contexts =
        [
            new ContextDefinition { Id = "work", DisplayName = "Work", LaunchApps = ["Slack"], CloseApps = ["Slack"] },
            new ContextDefinition
            {
                Id = "personal",
                DisplayName = "Personal",
                BrowserManagement = new BrowserManagementConfig { Mode = BrowserManagementMode.Urls, Browser = BrowserKind.Safari, Urls = ["https://news.example/"] }
            },
            new ContextDefinition
            {
                Id = "study",
                DisplayName = "Study",
                BrowserManagement = new BrowserManagementConfig { Mode = BrowserManagementMode.Profiles, Profiles = [new BrowserProfileConfig { ProfileDirectory = "Default" }] }
            }
        ]
    };

    [Fact]
    public void WhatWasClosedGoesToTheSwitch()
    {
        UnclaimedAnswers answers = new(
            [(new RunningApp("Figma"), UnclaimedChoice.Close), (new RunningApp("Notes"), UnclaimedChoice.Keep), (new RunningApp("Zoom"), UnclaimedChoice.AddToLeaving)],
            [(new OpenBrowserTab(BrowserKind.Chrome, "Lo-fi", "https://youtube.com/watch?v=1"), UnclaimedChoice.Close)]);

        SwitchExtras extras = answers.ToExtras();

        Assert.Equal(["Figma"], extras.CloseApps);
        Assert.Equal([new TabToClose(BrowserKind.Chrome, "https://youtube.com/watch?v=1")], extras.CloseTabs);
        Assert.True(answers.AddsAnything);
    }

    [Fact]
    public void AnAddedAppBecomesAFullPartOfItsProfile()
    {
        UnclaimedAnswers answers = new(
            [(new RunningApp("Figma"), UnclaimedChoice.AddToLeaving), (new RunningApp("slack"), UnclaimedChoice.AddToLeaving), (new RunningApp("Notes"), UnclaimedChoice.AddToEntering)],
            []);

        AppConfiguration updated = answers.ApplyTo(Configuration, "work", "personal");

        ContextDefinition work = updated.Contexts[0];
        Assert.Equal(["Slack", "Figma"], work.LaunchApps);
        Assert.Equal(["Slack", "Figma"], work.CloseApps);
        Assert.Equal(["Notes"], updated.Contexts[1].LaunchApps);
        Assert.Equal(["Notes"], updated.Contexts[1].CloseApps);
        Assert.Same(Configuration.Contexts[2], updated.Contexts[2]);
    }

    /// <summary>
    /// A page is added opened and closed; a profile that opened no pages starts to, in the tab's own
    /// browser; one that opens browser profiles takes none.
    /// </summary>
    [Fact]
    public void AnAddedTabBecomesOneOfItsProfilesPages()
    {
        OpenBrowserTab pulls = new(BrowserKind.Chrome, "Pull requests", "https://github.com/pulls");
        UnclaimedAnswers answers = new([], [(pulls, UnclaimedChoice.AddToLeaving)]);

        AppConfiguration toWork = answers.ApplyTo(Configuration, "work", "personal");
        BrowserManagementConfig work = toWork.Contexts[0].BrowserManagement;
        Assert.Equal((BrowserManagementMode.Urls, BrowserKind.Chrome), (work.Mode, work.Browser));
        Assert.Equal(["https://github.com/pulls"], work.Urls);
        Assert.Equal(["https://github.com/pulls"], work.CloseUrls);

        AppConfiguration toPersonal = new UnclaimedAnswers([], [(pulls, UnclaimedChoice.AddToEntering)]).ApplyTo(Configuration, "work", "personal");
        BrowserManagementConfig personal = toPersonal.Contexts[1].BrowserManagement;
        Assert.Equal(BrowserKind.Safari, personal.Browser);
        Assert.Equal(["https://news.example/", "https://github.com/pulls"], personal.Urls);

        Assert.False(UnclaimedAnswers.TakesPages(Configuration.Contexts[2]));
        AppConfiguration toStudy = new UnclaimedAnswers([], [(pulls, UnclaimedChoice.AddToEntering)]).ApplyTo(Configuration, "work", "study");
        Assert.Empty(toStudy.Contexts[2].BrowserManagement.Urls);
    }
}
