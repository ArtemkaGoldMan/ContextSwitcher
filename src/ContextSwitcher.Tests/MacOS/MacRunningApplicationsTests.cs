using ContextSwitcher.Core.Applications;
using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.MacOS;

/// <summary>Against the real list of running apps: it reads, and touches nothing.</summary>
public sealed class MacRunningApplicationsTests
{
    /// <summary>Apps with a place in the Dock, each named as Finder shows it - never Finder, never the app asking.</summary>
    [Fact]
    public void ListsDockAppsByTheirFinderNames()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        IReadOnlyList<RunningApp> apps = new MacRunningApplications().List();

        Assert.DoesNotContain(apps, app => app.IsCalled("Finder"));
        Assert.DoesNotContain(apps, app => app.IsCalled("ContextSwitcher"));
        Assert.All(apps, app =>
        {
            Assert.False(string.IsNullOrWhiteSpace(app.Name));
            Assert.False(app.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase), app.Name);
        });
    }
}
