using ContextSwitcher.Infrastructure.MacOS;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.MacOS;

public sealed class MacNotificationsTests
{
    /// <summary>
    /// The test host is no app bundle, the same as running from source. macOS's notification center
    /// throws an Objective-C exception there that would end the process, so it must never be called.
    /// </summary>
    [Fact]
    public void OutsideAnAppBundleNothingIsShownAndNothingBreaks()
    {
        MacNotifications notifications = new(new TestLogger(), new FakeClock());

        notifications.Show("update", "Context Switcher 9.9.9 is available", "Click to see what's new and install it.");
        notifications.Remove("update");
    }
}
