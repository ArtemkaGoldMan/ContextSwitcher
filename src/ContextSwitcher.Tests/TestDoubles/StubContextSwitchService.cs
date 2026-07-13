using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Contexts;

namespace ContextSwitcher.Tests.TestDoubles;

public sealed class StubContextSwitchService : IContextSwitchService
{
    public ContextSwitchRequest? LastRequest { get; private set; }

    public ContextSwitchResult Result { get; set; } = new(
        "work", null, ContextSwitchStatus.Succeeded, [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "correlation-id");

    public Task<ContextSwitchResult> SwitchAsync(ContextSwitchRequest request, CancellationToken cancellationToken)
    {
        this.LastRequest = request;
        return Task.FromResult(this.Result);
    }
}
