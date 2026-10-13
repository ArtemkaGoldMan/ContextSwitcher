using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.MacOS;

public sealed class MacLoginItemTests
{
    /// <summary>
    /// The test host is no app bundle, the same as running from source: there is no main app service,
    /// so nothing is offered and nothing is changed in the person's Login Items.
    /// </summary>
    [Fact]
    public void OutsideAnAppBundleNothingIsOfferedOrChanged()
    {
        MacLoginItem loginItem = new();

        Assert.Equal(LoginItemStatus.Unavailable, loginItem.Status);
        Assert.Null(loginItem.SetEnabled(true));
        Assert.Equal(LoginItemStatus.Unavailable, loginItem.Status);
    }
}
