using ContextSwitcher.Infrastructure.Windows.Applications;
using static ContextSwitcher.Tests.Windows.Applications.StartMenuRig;

namespace ContextSwitcher.Tests.Windows.Applications;

/// <summary>Asking an app to quit as its close button does, and looking whether it went.</summary>
public sealed class WindowsAppQuitterTests
{
    private const string Code = @"C:\Users\someone\AppData\Local\Programs\Microsoft VS Code\Code.exe";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static StartMenuRig Rig(params ProcessInfo[] running)
    {
        StartMenuRig rig = new(new StartApp("Visual Studio Code", "Microsoft.VisualStudioCode") { Target = Code });
        rig.Pc.Running = [.. running];
        return rig;
    }

    [Fact]
    public async Task EachMainWindowIsAskedToClose()
    {
        StartMenuRig rig = Rig(Process(100, 1, Code), Process(101, 100, Code));
        rig.Windows.Open = [Window(1, 100), Window(2, 101)];

        string said = await rig.Quitter.QuitAsync("Visual Studio Code", Timeout, CancellationToken.None);

        Assert.Equal(string.Empty, said);
        Assert.Equal<nint>([1, 2], rig.Windows.AskedToClose.Order());
    }

    /// <summary>A dialog is the app's to answer; a palette or a hidden window isn't one a close button closes.</summary>
    [Fact]
    public async Task OnlyMainWindowsAreAskedNotDialogsPalettesOrHiddenOnes()
    {
        StartMenuRig rig = Rig(Process(100, 1, Code));
        rig.Windows.Open = [Window(1, 100), Window(2, 100, owned: true), Window(3, 100, tool: true), Window(4, 100, visible: false)];

        await rig.Quitter.QuitAsync("Visual Studio Code", Timeout, CancellationToken.None);

        Assert.Equal<nint>([1], rig.Windows.AskedToClose);
    }

    [Fact]
    public async Task AMinimisedMainWindowIsAskedToo()
    {
        StartMenuRig rig = Rig(Process(100, 1, Code));
        rig.Windows.Open = [Window(1, 100, minimized: true)];

        await rig.Quitter.QuitAsync("Visual Studio Code", Timeout, CancellationToken.None);

        Assert.Equal<nint>([1], rig.Windows.AskedToClose);
    }

    [Fact]
    public async Task AnotherAppsWindowsAreLeftAlone()
    {
        StartMenuRig rig = Rig(Process(100, 1, Code), Process(200, 1, @"C:\Windows\notepad.exe"));
        rig.Windows.Open = [Window(1, 100), Window(2, 200)];

        await rig.Quitter.QuitAsync("Visual Studio Code", Timeout, CancellationToken.None);

        Assert.Equal<nint>([1], rig.Windows.AskedToClose);
    }

    [Fact]
    public async Task AnAppThatIsNotRunningIsNotAsked()
    {
        StartMenuRig rig = Rig(Process(200, 1, @"C:\Windows\notepad.exe"));
        rig.Windows.Open = [Window(2, 200)];

        string said = await rig.Quitter.QuitAsync("Visual Studio Code", Timeout, CancellationToken.None);

        Assert.Equal(string.Empty, said);
        Assert.Empty(rig.Windows.AskedToClose);
    }

    [Fact]
    public async Task AnAppRunningInTheBackgroundSaysSoInsteadOfPretending()
    {
        StartMenuRig rig = Rig(Process(100, 1, Code));
        rig.Windows.Open = [Window(1, 100, visible: false)];

        string said = await rig.Quitter.QuitAsync("Visual Studio Code", Timeout, CancellationToken.None);

        Assert.Contains("no window to close", said);
        Assert.Empty(rig.Windows.AskedToClose);
    }

    [Fact]
    public async Task AWindowWindowsWillNotPassTheRequestToIsCounted()
    {
        StartMenuRig rig = Rig(Process(100, 1, Code));
        rig.Windows.Open = [Window(1, 100), Window(2, 100)];
        rig.Windows.Unreachable.Add(2);

        string said = await rig.Quitter.QuitAsync("Visual Studio Code", Timeout, CancellationToken.None);

        Assert.Contains("1 of", said);
        Assert.Equal<nint>([1], rig.Windows.AskedToClose);
    }

    [Fact]
    public async Task AnAppNotInTheStartMenuIsSaidSoAndNothingIsClosed()
    {
        StartMenuRig rig = Rig(Process(100, 1, Code));
        rig.Windows.Open = [Window(1, 100)];

        string said = await rig.Quitter.QuitAsync("Nope", Timeout, CancellationToken.None);

        Assert.Contains("isn't in the Start menu", said);
        Assert.Empty(rig.Windows.AskedToClose);
    }

    [Fact]
    public async Task WhenWindowsWillNotListProcessesNothingIsClosed()
    {
        StartMenuRig rig = Rig();
        rig.Pc.Running = null;
        rig.Windows.Open = [Window(1, 100)];

        string said = await rig.Quitter.QuitAsync("Visual Studio Code", Timeout, CancellationToken.None);

        Assert.Contains("wouldn't list the processes", said);
        Assert.Empty(rig.Windows.AskedToClose);
    }

    [Fact]
    public async Task WhenWindowsWillNotListWindowsNothingIsClosed()
    {
        StartMenuRig rig = Rig(Process(100, 1, Code));
        rig.Windows.Open = null;

        string said = await rig.Quitter.QuitAsync("Visual Studio Code", Timeout, CancellationToken.None);

        Assert.Contains("wouldn't list the windows", said);
    }

    /// <summary>Quitting may be the first thing to ask about the app, before anything has listed the Start menu.</summary>
    [Fact]
    public async Task QuittingListsTheStartMenuFirst()
    {
        StartMenuRig rig = Rig(Process(100, 1, Code));
        rig.Windows.Open = [Window(1, 100)];

        await rig.Quitter.QuitAsync("Visual Studio Code", Timeout, CancellationToken.None);

        Assert.Single(rig.Windows.AskedToClose);
    }

    [Fact]
    public async Task LookingWhetherItWentAnswersFromWhatIsRunningNow()
    {
        StartMenuRig rig = Rig(Process(100, 1, Code));

        Assert.True(await rig.Quitter.IsRunningAsync("Visual Studio Code", Timeout, CancellationToken.None));

        rig.Pc.Running = [];

        Assert.False(await rig.Quitter.IsRunningAsync("Visual Studio Code", Timeout, CancellationToken.None));
    }

    [Fact]
    public async Task LookingAtAnAppTheStartMenuDoesNotHaveCannotBeTold()
    {
        StartMenuRig rig = Rig(Process(100, 1, Code));

        Assert.Null(await rig.Quitter.IsRunningAsync("Nope", Timeout, CancellationToken.None));
    }

    [Fact]
    public async Task ABlankNameIsRefused()
    {
        StartMenuRig rig = Rig();

        await Assert.ThrowsAsync<ArgumentException>(() => rig.Quitter.QuitAsync(" ", Timeout, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => rig.Quitter.IsRunningAsync(string.Empty, Timeout, CancellationToken.None));
    }
}
