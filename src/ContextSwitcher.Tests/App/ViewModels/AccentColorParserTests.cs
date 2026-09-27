using Avalonia.Media;
using Avalonia.Media.Immutable;
using ContextSwitcher.App.ViewModels;

namespace ContextSwitcher.Tests.App.ViewModels;

public sealed class AccentColorParserTests
{
    /// <summary>
    /// A plain SolidColorBrush is an AvaloniaObject and belongs to the thread that created it, so
    /// one built while a view model was rebuilt off the UI thread threw from inside Border.Render
    /// on the next frame. Immutable brushes carry no thread affinity, which keeps accent swatches
    /// safe whichever thread constructs the view model.
    /// </summary>
    [Fact]
    public void ToBrushReturnsABrushWithNoThreadAffinity()
    {
        IBrush brush = AccentColorParser.ToBrush("#2F6FED");

        Assert.IsType<ImmutableSolidColorBrush>(brush);
    }

    [Fact]
    public async Task ABrushBuiltOffOneThreadIsReadableFromAnother()
    {
        IBrush brush = await Task.Run(() => AccentColorParser.ToBrush("#20A67A"));

        // Reading Transform is what the compositor does, and what used to throw.
        Exception? error = Record.Exception(() => _ = brush.Transform);

        Assert.Null(error);
        Assert.Equal(Color.Parse("#20A67A"), Assert.IsType<ImmutableSolidColorBrush>(brush).Color);
    }

    [Fact]
    public void InvalidHexFallsBackToGray()
    {
        Assert.Same(Brushes.Gray, AccentColorParser.ToBrush("not-a-color"));
    }
}
