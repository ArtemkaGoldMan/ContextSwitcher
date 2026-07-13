using ContextSwitcher.Infrastructure.Hotkeys;
using SharpHook.Data;

namespace ContextSwitcher.Tests.Hotkeys;

public sealed class AcceleratorParserTests
{
    [Fact]
    public void TryParseParsesMultipleModifiersAndLetterKey()
    {
        bool parsed = AcceleratorParser.TryParse("Cmd+Alt+Ctrl+W", out EventMask modifiers, out KeyCode key);

        Assert.True(parsed);
        Assert.Equal(EventMask.Meta | EventMask.Alt | EventMask.Ctrl, modifiers);
        Assert.Equal(KeyCode.VcW, key);
    }

    [Fact]
    public void TryParseIsCaseInsensitive()
    {
        bool parsed = AcceleratorParser.TryParse("cmd+w", out EventMask modifiers, out KeyCode key);

        Assert.True(parsed);
        Assert.Equal(EventMask.Meta, modifiers);
        Assert.Equal(KeyCode.VcW, key);
    }

    [Fact]
    public void TryParseParsesDigitKey()
    {
        bool parsed = AcceleratorParser.TryParse("Cmd+1", out _, out KeyCode key);

        Assert.True(parsed);
        Assert.Equal(KeyCode.Vc1, key);
    }

    [Theory]
    [InlineData("Cmd+Esc", KeyCode.VcEscape)]
    [InlineData("Cmd+Return", KeyCode.VcEnter)]
    [InlineData("Cmd+Del", KeyCode.VcDelete)]
    [InlineData("Cmd+F1", KeyCode.VcF1)]
    public void TryParseNormalizesNamedKeyAliases(string accelerator, KeyCode expected)
    {
        bool parsed = AcceleratorParser.TryParse(accelerator, out _, out KeyCode key);

        Assert.True(parsed);
        Assert.Equal(expected, key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("W")]
    [InlineData("Foo+W")]
    [InlineData("Cmd+NotAKey")]
    public void TryParseRejectsInvalidAccelerators(string accelerator)
    {
        bool parsed = AcceleratorParser.TryParse(accelerator, out _, out _);

        Assert.False(parsed);
    }
}
