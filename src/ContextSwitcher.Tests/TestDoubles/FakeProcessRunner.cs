using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Tests.TestDoubles;

public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Queue<ProcessResult> queuedResults = new();

    public List<ProcessStartOptions> Calls { get; } = [];

    public ProcessResult DefaultResult { get; set; } = new(0, string.Empty, string.Empty, false);

    public void Enqueue(ProcessResult result) => this.queuedResults.Enqueue(result);

    public Task<ProcessResult> RunAsync(ProcessStartOptions options, CancellationToken cancellationToken)
    {
        this.Calls.Add(options);
        ProcessResult result = this.queuedResults.Count > 0 ? this.queuedResults.Dequeue() : this.DefaultResult;
        return Task.FromResult(result);
    }
}
