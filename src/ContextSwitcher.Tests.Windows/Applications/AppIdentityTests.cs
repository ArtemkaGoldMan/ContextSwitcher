using ContextSwitcher.Infrastructure.Windows.Applications;

namespace ContextSwitcher.Tests.Windows.Applications;

/// <summary>Which processes are an app's, from what the Start menu says of it.</summary>
public sealed class AppIdentityTests
{
    private static ProcessInfo Running(string? image, string? package = null) => new(10, 1, image, package, null);

    [Fact]
    public void TheSameExecutableIsTheAppsWhateverItsCase()
    {
        AppIdentity identity = new(@"C:\Program Files\Google\Chrome\Application\chrome.exe", null);

        Assert.True(identity.Matches(Running(@"c:\program files\google\chrome\application\CHROME.EXE")));
    }

    /// <summary>Apps that update themselves keep each version in a folder of its own.</summary>
    [Fact]
    public void TheSameFileNameBelowTheShortcutsFolderIsTheAppsToo()
    {
        AppIdentity identity = new(@"C:\Users\someone\AppData\Local\Discord\Discord.exe", null);

        Assert.True(identity.Matches(Running(@"C:\Users\someone\AppData\Local\Discord\app-1.0.9\Discord.exe")));
    }

    [Fact]
    public void AnotherProgramInTheSameFolderIsNot()
    {
        AppIdentity identity = new(@"C:\Users\someone\AppData\Local\Discord\Discord.exe", null);

        Assert.False(identity.Matches(Running(@"C:\Users\someone\AppData\Local\Discord\Update.exe")));
    }

    /// <summary>A shortcut into a folder every program shares must not take their processes.</summary>
    [Fact]
    public void ASharedFoldersOtherProgramsAreNot()
    {
        AppIdentity identity = new(@"C:\Windows\System32\cmd.exe", null);

        Assert.False(identity.Matches(Running(@"C:\Windows\System32\svchost.exe")));
        Assert.False(identity.Matches(Running(@"C:\Windows\System32\wbem\wmiprvse.exe")));
    }

    [Fact]
    public void AFolderThatOnlyStartsWithTheSameLettersIsNot()
    {
        AppIdentity identity = new(@"C:\App\app.exe", null);

        Assert.False(identity.Matches(Running(@"C:\AppEvil\app.exe")));
    }

    [Fact]
    public void TheSameFileNameInAnotherFolderIsNot()
    {
        AppIdentity identity = new(@"C:\Program Files\Slack\slack.exe", null);

        Assert.False(identity.Matches(Running(@"D:\Downloads\slack.exe")));
    }

    [Fact]
    public void AProcessThatWillNotSayWhereItIsIsNobodys()
    {
        Assert.False(new AppIdentity(@"C:\App\app.exe", null).Matches(Running(null)));
    }

    [Fact]
    public void AStoreAppsProcessesCarryItsPackage()
    {
        AppIdentity identity = new(null, "MSTeams_8wekyb3d8bbwe");

        Assert.True(identity.Matches(Running(@"C:\Program Files\WindowsApps\MSTeams_1\ms-teams.exe", "msteams_8wekyb3d8bbwe")));
        Assert.False(identity.Matches(Running(@"C:\Program Files\WindowsApps\MSTeams_1\ms-teams.exe", "Microsoft.WindowsNotepad_8wekyb3d8bbwe")));
        Assert.False(identity.Matches(Running(@"C:\Program Files\WindowsApps\MSTeams_1\ms-teams.exe")));
    }

    /// <summary>An identity with neither says nothing about any process.</summary>
    [Fact]
    public void NothingKnownMatchesNothing()
    {
        Assert.False(new AppIdentity(null, null).Matches(Running(@"C:\App\app.exe", "Family")));
    }
}
