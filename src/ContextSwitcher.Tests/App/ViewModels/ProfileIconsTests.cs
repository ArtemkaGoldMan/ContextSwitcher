using ContextSwitcher.App.ViewModels;

namespace ContextSwitcher.Tests.App.ViewModels;

public sealed class ProfileIconsTests
{
    /// <summary>
    /// Configs written before the picker existed store whatever was typed into the old text field,
    /// and a later build may add icons this one lacks. Either way something must still be drawn.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("rocket")]
    public void AnUnknownNameFallsBackToTheCircle(string? name)
    {
        Assert.Equal("circle", ProfileIcons.Find(name).Name);
    }

    [Fact]
    public void StoredNamesMatchWhateverTheirCase()
    {
        Assert.Equal("briefcase", ProfileIcons.Find(" Briefcase ").Name);
    }

    [Fact]
    public void EveryIconHasADistinctNameAndALabel()
    {
        Assert.Equal(ProfileIcons.All.Count, ProfileIcons.All.Select(i => i.Name).Distinct().Count());
        Assert.All(ProfileIcons.All, icon => Assert.False(string.IsNullOrWhiteSpace(icon.Label)));
        Assert.Contains(ProfileIcons.All, icon => icon.Name == ProfileIcons.Fallback);
    }
}
