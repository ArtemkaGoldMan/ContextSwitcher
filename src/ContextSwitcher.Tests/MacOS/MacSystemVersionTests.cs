using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.MacOS;

/// <summary>Which system the first line of a log and of a report names.</summary>
public sealed class MacSystemVersionTests
{
    /// <summary>The system's own file where there is one; what the runtime says where there isn't, as in a test host.</summary>
    [Fact]
    public void AMacIsNamedWithItsVersion()
    {
        MacSystemVersion system = new();

        Assert.Equal("macOS", system.Name);
        Assert.False(string.IsNullOrWhiteSpace(system.Read()));
    }
}
