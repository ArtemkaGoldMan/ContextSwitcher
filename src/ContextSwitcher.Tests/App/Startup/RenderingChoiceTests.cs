using ContextSwitcher.App.Startup;

namespace ContextSwitcher.Tests.App.Startup;

/// <summary>CPU or GPU drawing: the platform's choice, unless the variable says otherwise.</summary>
public sealed class RenderingChoiceTests
{
    [Theory]
    [InlineData(null, false, false)]
    [InlineData(null, true, true)]
    [InlineData("", true, true)]
    [InlineData("nonsense", false, false)]
    [InlineData("nonsense", true, true)]
    public void WithoutAnOverrideThePlatformDecides(string? variable, bool platformDefault, bool expected)
    {
        Assert.Equal(expected, RenderingChoice.UseSoftware(variable, platformDefault));
    }

    [Theory]
    [InlineData("software", false)]
    [InlineData("SOFTWARE", true)]
    public void SoftwareIsAlwaysHonoured(string variable, bool platformDefault)
    {
        Assert.True(RenderingChoice.UseSoftware(variable, platformDefault));
    }

    [Theory]
    [InlineData("gpu", true)]
    [InlineData("GPU", false)]
    public void GpuIsAlwaysHonoured(string variable, bool platformDefault)
    {
        Assert.False(RenderingChoice.UseSoftware(variable, platformDefault));
    }
}
