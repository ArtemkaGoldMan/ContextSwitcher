using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.MacOS;
using ContextSwitcher.Infrastructure.ProcessExecution;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.MacOS;

/// <summary>
/// The Mac's image converter: <c>sips</c>, the one place it is run - for the apps' icons and the
/// sites' alike.
/// </summary>
public sealed class SipsImageConverterTests : IDisposable
{
    /// <summary>A real 1×1 PNG, which <c>sips</c> reads like any icon.</summary>
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly string directory = Path.Combine(Path.GetTempPath(), $"cs-sips-{Guid.NewGuid():N}");

    public SipsImageConverterTests() => Directory.CreateDirectory(this.directory);

    public void Dispose() => Directory.Delete(this.directory, recursive: true);

    [Fact]
    public async Task AsksSipsForAPngOfTheSizeWithinItsTime()
    {
        FakeProcessRunner runner = new() { DefaultResult = new ProcessResult(1, string.Empty, "unreadable", false) };
        SipsImageConverter converter = new(runner, TimeSpan.FromSeconds(3));

        bool converted = await converter.ToPngAsync("/in/icon.icns", "/out/icon.png", 64, CancellationToken.None);

        ProcessStartOptions call = Assert.Single(runner.Calls);
        Assert.Equal("sips", call.FileName);
        Assert.Equal(["-s", "format", "png", "-Z", "64", "/in/icon.icns", "--out", "/out/icon.png"], call.Arguments);
        Assert.Equal(TimeSpan.FromSeconds(3), call.Timeout);
        Assert.False(converted);
    }

    [MacOSFact]
    public async Task TheRealSipsMakesThePng()
    {
        string source = Path.Combine(this.directory, "icon.png");
        string destination = Path.Combine(this.directory, "small.png");
        await File.WriteAllBytesAsync(source, Png);

        bool converted = await new SipsImageConverter(new ProcessRunner(MacPlatform.AllowedCommands), SipsImageConverter.DefaultTimeout)
            .ToPngAsync(source, destination, 32, CancellationToken.None);

        Assert.True(converted);
        Assert.True(File.Exists(destination));
    }
}
