using System.Diagnostics;
using System.IO.Pipes;
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
    private static readonly TimeSpan HandedOverWithin = TimeSpan.FromSeconds(3);

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

    /// <summary>
    /// A copy opened the moment after Listen returns must find the pipe: it is made before Listen
    /// returns, not later on a thread pool that may be busy.
    /// </summary>
    [Fact]
    public void TheFirstCopyIsListeningAsSoonAsListenReturns()
    {
        string folder = Folder();
        using WindowsSingleInstance first = new(folder, new TestLogger());
        Assert.True(first.TryClaim([]));

        first.Listen();

        using Process current = Process.GetCurrentProcess();
        string name = WindowsSingleInstance.NameFor(folder, Environment.UserName, current.SessionId);
        using NamedPipeClientStream probe = new(".", name, PipeDirection.Out, PipeOptions.CurrentUserOnly);
        probe.Connect(timeout: 1);
    }

    /// <summary>
    /// A copy that connects and ends without a word can end before the first one is waiting for it,
    /// when Windows reports the pipe as closing. Each is still a copy opened, and each is heard.
    /// </summary>
    [Fact]
    public async Task EveryCopyThatComesAndGoesWithoutAWordIsHeard()
    {
        const int copies = 30;
        string folder = Folder();
        using WindowsSingleInstance first = new(folder, new TestLogger());
        int heard = 0;
        TaskCompletionSource allHeard = new(TaskCreationOptions.RunContinuationsAsynchronously);
        first.Reopened += (_, _) =>
        {
            if (Interlocked.Increment(ref heard) == copies)
            {
                allHeard.TrySetResult();
            }
        };
        Assert.True(first.TryClaim([]));
        first.Listen();

        using Process current = Process.GetCurrentProcess();
        string name = WindowsSingleInstance.NameFor(folder, Environment.UserName, current.SessionId);
        for (int copy = 0; copy < copies; copy++)
        {
            using NamedPipeClientStream client = new(".", name, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(HandedOverWithin);
        }

        await allHeard.Task.WaitAsync(HeardWithin);
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
