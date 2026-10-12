using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;

namespace ContextSwitcher.Tests.App.Startup;

public sealed class ActiveAtStartupTests
{
    private static readonly AppConfiguration Configuration = new() { ActiveContextId = "work" };

    [Fact]
    public void BeforeAnySwitchTheConfiguredProfileCounts()
    {
        Assert.Equal("work", AppHost.ActiveAtStartup(new CurrentContextState(), Configuration));
    }

    [Fact]
    public void TheLastSwitchedToProfileCounts()
    {
        CurrentContextState state = new() { CurrentContextId = "personal", LastSwitchCompletedAt = DateTimeOffset.UtcNow };

        Assert.Equal("personal", AppHost.ActiveAtStartup(state, Configuration));
    }

    /// <summary>A profile turned off stays off across a restart: no time is counted for any.</summary>
    [Fact]
    public void AfterTurningOffNoneCounts()
    {
        CurrentContextState state = new() { PreviousContextId = "work", LastSwitchCompletedAt = DateTimeOffset.UtcNow };

        Assert.Equal(string.Empty, AppHost.ActiveAtStartup(state, Configuration));
    }
}
