using ContextSwitcher.Infrastructure.Windows;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Windows;

/// <summary>
/// One copy for each folder of settings: a second copy hands its arguments to the first and ends, and
/// a copy with a folder of its own - a development build - runs beside it.
/// </summary>
public sealed class WindowsSingleInstanceTests
{
    private static readonly TimeSpan HeardWithin = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ASecondCopyHandsItsArgumentsToTheFirstAndEnds()
    {
        string folder = Folder();
        using WindowsSingleInstance first = new(folder, new TestLogger());
        using WindowsSingleInstance second = new(folder, new TestLogger());
        TaskCompletionSource<IReadOnlyList<string>> heard = new(TaskCreationOptions.RunContinuationsAsynchronously);
        first.Reopened += (_, arguments) => heard.TrySetResult(arguments);

        Assert.True(first.TryClaim([]));
        first.Listen();

        Assert.False(second.TryClaim(["open-dashboard"]));
        Assert.Equal(["open-dashboard"], await heard.Task.WaitAsync(HeardWithin));
    }

    /// <summary>A copy opened with nothing to say still reaches the first, which shows its window.</summary>
    [Fact]
    public async Task ASecondCopyWithNoArgumentsIsHeardToo()
    {
        string folder = Folder();
        using WindowsSingleInstance first = new(folder, new TestLogger());
        using WindowsSingleInstance second = new(folder, new TestLogger());
        TaskCompletionSource<IReadOnlyList<string>> heard = new(TaskCreationOptions.RunContinuationsAsynchronously);
        first.Reopened += (_, arguments) => heard.TrySetResult(arguments);

        Assert.True(first.TryClaim([]));
        first.Listen();

        Assert.False(second.TryClaim([]));
        Assert.Empty(await heard.Task.WaitAsync(HeardWithin));
    }

    [Fact]
    public void ACopyWithAFolderOfItsOwnRunsBeside()
    {
        using WindowsSingleInstance installed = new(Folder(), new TestLogger());
        using WindowsSingleInstance development = new(Folder(), new TestLogger());

        Assert.True(installed.TryClaim([]));
        Assert.True(development.TryClaim([]));
    }

    /// <summary>Once the first copy has ended, the next one runs.</summary>
    [Fact]
    public void TheNextCopyRunsOnceTheFirstHasEnded()
    {
        string folder = Folder();
        WindowsSingleInstance first = new(folder, new TestLogger());
        Assert.True(first.TryClaim([]));
        first.Dispose();

        using WindowsSingleInstance next = new(folder, new TestLogger());
        Assert.True(next.TryClaim([]));
    }

    /// <summary>The same folder, however it is written, is the same copy; Windows' paths ignore case.</summary>
    [Fact]
    public void TheSameFolderHasTheSameName()
    {
        string name = WindowsSingleInstance.NameFor(@"C:\Users\someone\AppData\Roaming\ContextSwitcher", "someone", 1);

        Assert.Equal(name, WindowsSingleInstance.NameFor(@"c:\users\someone\appdata\roaming\contextswitcher\", "someone", 1));
        Assert.NotEqual(name, WindowsSingleInstance.NameFor(@"C:\Users\someone\AppData\Roaming\ContextSwitcher-dev", "someone", 1));
        Assert.NotEqual(name, WindowsSingleInstance.NameFor(@"C:\Users\someone\AppData\Roaming\ContextSwitcher", "someone", 2));
        Assert.DoesNotContain('\\', name);
    }

    /// <summary>A folder of its own for each test, so tests running at once never meet.</summary>
    private static string Folder() => Path.Combine(Path.GetTempPath(), $"ContextSwitcherTests-{Guid.NewGuid():N}");
}
