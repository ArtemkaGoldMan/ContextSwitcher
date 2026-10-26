using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Windows.NotYet;

/// <summary>Turning site icons into PNGs on Windows comes in stage 2, with the pages; until then none is.</summary>
public sealed class NotYetImageConverter : IImageConverter
{
    /// <inheritdoc />
    public Task<bool> ToPngAsync(string source, string destination, int size, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
