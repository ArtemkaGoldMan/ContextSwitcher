using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Updates;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>Answers with <see cref="Latest"/>, or throws <see cref="Failure"/> when that is set.</summary>
public sealed class FakeReleaseSource : IReleaseSource
{
    public ReleaseInfo? Latest { get; set; }

    public UpdateException? Failure { get; set; }

    public int Calls { get; private set; }

    public Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken)
    {
        this.Calls++;
        return this.Failure is { } failure ? Task.FromException<ReleaseInfo?>(failure) : Task.FromResult(this.Latest);
    }
}
