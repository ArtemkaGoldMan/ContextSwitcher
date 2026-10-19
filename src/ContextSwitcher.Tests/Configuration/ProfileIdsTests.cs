using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Tests.Configuration;

/// <summary>
/// A new profile's id - from its name, for a copy, or before it is named - follows one rule: the first
/// of the base, base-2, base-3 that no profile has. The pages that make them used three versions of it.
/// </summary>
public sealed class ProfileIdsTests
{
    private static readonly ContextDefinition[] Taken =
    [
        new() { Id = "work" },
        new() { Id = "work-copy" },
        new() { Id = "profile" },
        new() { Id = "deep-work" },
        new() { Id = "deep-work-2" }
    ];

    [Theory]
    [InlineData("Study", "study")]
    [InlineData("  Deep Work!  ", "deep-work-3")]
    [InlineData("Side project #2", "side-project-2")]
    [InlineData("Робота", "profile-2")]
    [InlineData("!!!", "profile-2")]
    public void AnIdIsMadeFromTheNameAndNeverClashes(string name, string expected)
    {
        Assert.Equal(expected, ProfileIds.FromName(name, Taken));
    }

    [Fact]
    public void ACopysIdSaysSoAndNeverClashes()
    {
        Assert.Equal("work-copy-2", ProfileIds.ForCopyOf("work", Taken));
        Assert.Equal("study-copy", ProfileIds.ForCopyOf("study", Taken));
    }

    [Fact]
    public void AProfileNotYetNamedIsProfileAndNeverClashes()
    {
        Assert.Equal("profile-2", ProfileIds.ForNewProfile(Taken));
        Assert.Equal("profile", ProfileIds.ForNewProfile([]));
    }
}
