using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>A switch lock held in memory: free unless a test says another process holds it.</summary>
public sealed class FakeSwitchLock : ISwitchLock
{
    /// <summary>Whether another process holds the lock, so it can't be taken.</summary>
    public bool HeldElsewhere { get; set; }

    /// <summary>How many times the lock was taken and not yet given back.</summary>
    public int Held { get; private set; }

    public IDisposable? TryAcquire()
    {
        if (this.HeldElsewhere)
        {
            return null;
        }

        this.Held++;
        return new Release(this);
    }

    private sealed class Release(FakeSwitchLock owner) : IDisposable
    {
        public void Dispose() => owner.Held--;
    }
}
