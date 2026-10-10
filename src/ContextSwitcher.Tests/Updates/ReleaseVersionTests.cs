using ContextSwitcher.Core.Updates;

namespace ContextSwitcher.Tests.Updates;

public sealed class ReleaseVersionTests
{
    [Theory]
    [InlineData("v0.2.0", "0.2.0")]
    [InlineData("0.2.0", "0.2.0")]
    [InlineData("V1.10.3", "1.10.3")]
    [InlineData("0.1.0+4f2a9c1", "0.1.0")]
    [InlineData("1.0.0-beta.2", "1.0.0")]
    [InlineData("1.2", "1.2.0")]
    [InlineData("v3", "3.0.0")]
    [InlineData(" 0.2.0\n", "0.2.0")]
    public void ReadsTagsAndBundleVersions(string text, string expected)
    {
        Assert.True(ReleaseVersion.TryParse(text, out Version version));
        Assert.Equal(expected, ReleaseVersion.Format(version));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v.x")]
    public void RejectsWhatIsNotAVersion(string? text) => Assert.False(ReleaseVersion.TryParse(text, out _));

    /// <summary>Numbers compare as numbers: 0.10.0 is newer than 0.9.0, which a string compare gets wrong.</summary>
    [Fact]
    public void ComparesAsNumbers()
    {
        ReleaseVersion.TryParse("v0.10.0", out Version newer);
        ReleaseVersion.TryParse("0.9.0", out Version older);
        ReleaseVersion.TryParse("1.2", out Version shortForm);
        ReleaseVersion.TryParse("1.2.0", out Version longForm);

        Assert.True(newer > older);
        Assert.Equal(shortForm, longForm);
    }
}
