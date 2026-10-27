using ContextSwitcher.Infrastructure.Windows.Applications;

namespace ContextSwitcher.Tests.Windows.Applications;

/// <summary>What an app a profile names is, from the Start menu's three ways of naming one.</summary>
public sealed class AppIdentitiesTests
{
    private static readonly Guid ProgramFilesX86 = Guid.Parse("7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E");

    [Fact]
    public async Task AStoreAppIsItsPackage()
    {
        StartMenuRig rig = new(new StartApp("Microsoft Teams", "MSTeams_8wekyb3d8bbwe!MSTeams"));

        AppIdentity? identity = await rig.Identities.FindAsync("Microsoft Teams", CancellationToken.None);

        Assert.Equal(new AppIdentity(null, "MSTeams_8wekyb3d8bbwe"), identity);
    }

    [Fact]
    public async Task APathIsTheExecutable()
    {
        StartMenuRig rig = new(new StartApp("Riot Client", @"C:\Riot Games\Riot Client\RiotClientServices.exe"));

        Assert.Equal(
            new AppIdentity(@"C:\Riot Games\Riot Client\RiotClientServices.exe", null),
            await rig.Identities.FindAsync("Riot Client", CancellationToken.None));
    }

    /// <summary>The Start menu writes Program Files as a GUID; Steam's id starts with one.</summary>
    [Fact]
    public async Task AKnownFoldersGuidIsWrittenOut()
    {
        StartMenuRig rig = new(new StartApp("Steam", @"{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}\Steam\Steam.exe"));
        rig.Folders.Folders[ProgramFilesX86] = @"C:\Program Files (x86)";

        Assert.Equal(
            new AppIdentity(@"C:\Program Files (x86)\Steam\Steam.exe", null),
            await rig.Identities.FindAsync("Steam", CancellationToken.None));
    }

    [Fact]
    public async Task AGuidWindowsDoesNotKnowTellsNothing()
    {
        StartMenuRig rig = new(new StartApp("Steam", @"{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}\Steam\Steam.exe"));

        Assert.Null(await rig.Identities.FindAsync("Steam", CancellationToken.None));
    }

    [Fact]
    public async Task AnIdThatSaysNothingOfTheExecutableUsesTheShortcutsTarget()
    {
        StartMenuRig rig = new(new StartApp("Visual Studio Code", "Microsoft.VisualStudioCode") { Target = @"C:\Users\someone\AppData\Local\Programs\Microsoft VS Code\Code.exe" });

        Assert.Equal(
            new AppIdentity(@"C:\Users\someone\AppData\Local\Programs\Microsoft VS Code\Code.exe", null),
            await rig.Identities.FindAsync("Visual Studio Code", CancellationToken.None));
    }

    [Fact]
    public async Task AnIdWithNoShortcutTellsNothing()
    {
        StartMenuRig rig = new(new StartApp("Edge", "MSEdge"));

        Assert.Null(await rig.Identities.FindAsync("Edge", CancellationToken.None));
    }

    /// <summary>A shortcut to a document, a folder or a relative path isn't how an app is told.</summary>
    [Theory]
    [InlineData(@"C:\Docs\readme.txt")]
    [InlineData(@"C:\Windows\explorer")]
    [InlineData(@"app.exe")]
    [InlineData(@"..\app.exe")]
    public async Task ATargetThatIsNotAnExecutableTellsNothing(string target)
    {
        StartMenuRig rig = new(new StartApp("Thing", "Thing.Id") { Target = target });

        Assert.Null(await rig.Identities.FindAsync("Thing", CancellationToken.None));
    }

    [Fact]
    public async Task TheNameIsMatchedWithoutCaseAndSpaces()
    {
        StartMenuRig rig = new(new StartApp("Visual Studio Code", "x!y"));

        Assert.NotNull(await rig.Identities.FindAsync("  visual studio CODE ", CancellationToken.None));
    }

    [Fact]
    public async Task AnAppNotInTheStartMenuIsNobody()
    {
        StartMenuRig rig = new(new StartApp("Slack", "a!b"));

        Assert.Null(await rig.Identities.FindAsync("Slak", CancellationToken.None));
    }

    /// <summary>Asking before anything has listed the Start menu can't be answered; asking after, can.</summary>
    [Fact]
    public async Task NothingIsKnownBeforeTheStartMenuHasBeenListed()
    {
        StartMenuRig rig = new(new StartApp("Slack", "a!b"));

        Assert.Null(rig.Identities.Find("Slack"));

        await rig.ListAsync();

        Assert.NotNull(rig.Identities.Find("Slack"));
    }

    [Fact]
    public void ABlankNameIsRefused()
    {
        StartMenuRig rig = new();

        Assert.Throws<ArgumentException>(() => rig.Identities.Find(" "));
    }
}
