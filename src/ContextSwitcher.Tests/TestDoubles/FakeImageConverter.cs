using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>
/// An image converter that copies the file as it is, so what uses one can be tested on any system
/// without <c>sips</c>. It can be told to fail, as <c>sips</c> does on a file it can't read.
/// </summary>
public sealed class FakeImageConverter : IImageConverter
{
    /// <summary>Every conversion asked for: the file, where it went, and the size.</summary>
    public List<(string Source, string Destination, int Size)> Calls { get; } = [];

    /// <summary>Whether conversions fail and write nothing.</summary>
    public bool Fails { get; set; }

    public Task<bool> ToPngAsync(string source, string destination, int size, CancellationToken cancellationToken)
    {
        this.Calls.Add((source, destination, size));
        if (this.Fails)
        {
            return Task.FromResult(false);
        }

        File.Copy(source, destination, overwrite: true);
        return Task.FromResult(true);
    }
}
