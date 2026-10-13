using ContextSwitcher.Core.Updates;

namespace ContextSwitcher.Tests.Updates;

public sealed class ReleaseNotesTests
{
    /// <summary>The description as the release workflow publishes it: our notes, then GitHub's own.</summary>
    private const string Description = """
        ## What's new

        - **Update notifications.** When a new version is out, macOS lets you know.
        - **Turn a profile off.** Click it again. `ContextSwitcher off` does the same from
          [Shortcuts](https://support.apple.com/guide/shortcuts-mac/) or Siri.

        ## Install

        - Paste this into Terminal.

        ## What's Changed
        * Release 0.4.0 by @ArtemkaGoldMan in https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/18
        """;

    [Fact]
    public void TheWhatsNewItemsAreReadAsPlainText()
    {
        Assert.Equal(
            [
                "Update notifications. When a new version is out, macOS lets you know.",
                "Turn a profile off. Click it again. ContextSwitcher off does the same from Shortcuts or Siri."
            ],
            ReleaseNotes.WhatsNew(Description));
    }

    /// <summary>The repository's own notes file - what the next release will say - reads as a list too.</summary>
    [Fact]
    public void TheRepositorysReleaseNotesHaveAWhatsNewList()
    {
        string path = Path.Combine(RepositoryRoot(), ".github", "release-notes.md");

        Assert.NotEmpty(ReleaseNotes.WhatsNew(File.ReadAllText(path)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("## Install\n\n- Paste this into Terminal.")]
    public void WithoutAWhatsNewSectionThereIsNothing(string? description)
    {
        Assert.Empty(ReleaseNotes.WhatsNew(description));
    }

    [Fact]
    public void ATypographicApostropheCountsToo()
    {
        Assert.Equal(["Faster."], ReleaseNotes.WhatsNew("### What’s New\n- Faster.\n"));
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".github")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("No repository above the test output.");
    }
}
