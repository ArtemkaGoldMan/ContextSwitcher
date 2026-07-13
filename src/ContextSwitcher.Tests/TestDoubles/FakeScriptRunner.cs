using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Tests.TestDoubles;

public sealed class FakeScriptRunner : IScriptRunner
{
    private readonly Queue<ProcessResult> queuedResults = new();

    public List<string> Scripts { get; } = [];

    public ProcessResult DefaultResult { get; set; } = new(0, string.Empty, string.Empty, false);

    public void Enqueue(ProcessResult result) => this.queuedResults.Enqueue(result);

    public Task<ProcessResult> RunAsync(string script, TimeSpan timeout, CancellationToken cancellationToken)
    {
        this.Scripts.Add(script);
        ProcessResult result = this.queuedResults.Count > 0 ? this.queuedResults.Dequeue() : this.DefaultResult;
        return Task.FromResult(result);
    }
}
