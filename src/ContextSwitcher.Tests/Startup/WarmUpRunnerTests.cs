using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Startup;

/// <summary>What the platform asks to have read as the app starts is read, whatever happens to the rest.</summary>
public sealed class WarmUpRunnerTests
{
    [Fact]
    public async Task EveryWarmUpRuns()
    {
        CountingWarmUp first = new();
        CountingWarmUp second = new();

        await new WarmUpRunner([first, second], new TestLogger()).RunAsync();

        Assert.Equal(1, first.Runs);
        Assert.Equal(1, second.Runs);
    }

    [Fact]
    public async Task OneThatFailsIsLoggedAndTheOthersStillRun()
    {
        TestLogger logger = new();
        CountingWarmUp after = new();

        await new WarmUpRunner([new FailingWarmUp(), after], logger).RunAsync();

        Assert.Equal(1, after.Runs);
        Assert.Contains(logger.Entries, entry => entry.EventId == "WarmUpFailed");
    }

    [Fact]
    public async Task NoneToRunIsNothingToDo()
    {
        await new WarmUpRunner([], new TestLogger()).RunAsync();
    }

    [Fact]
    public async Task StartingRunsThemInTheBackground()
    {
        CountingWarmUp warmUp = new();

        new WarmUpRunner([warmUp], new TestLogger()).Start();

        Assert.True(await warmUp.Ran.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private sealed class CountingWarmUp : IWarmUp
    {
        public int Runs { get; private set; }

        public TaskCompletionSource<bool> Ran { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WarmUpAsync(CancellationToken cancellationToken)
        {
            this.Runs++;
            this.Ran.TrySetResult(true);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingWarmUp : IWarmUp
    {
        public Task WarmUpAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("the Start menu is gone");
    }
}
