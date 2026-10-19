using System.Globalization;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Converts images with macOS's built-in <c>sips</c>: an app's <c>.icns</c> in about 28 ms, a site's
/// favicon in less. Whatever <c>sips</c> can't read, or doesn't finish in time, has no PNG.
/// </summary>
public sealed class SipsImageConverter : IImageConverter
{
    /// <summary>Long enough for an app's largest icon; a favicon takes a fraction of it.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly IProcessRunner processRunner;
    private readonly TimeSpan timeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="SipsImageConverter"/> class.
    /// </summary>
    /// <param name="processRunner">Runs <c>sips</c>.</param>
    /// <param name="timeout">How long one conversion may take.</param>
    public SipsImageConverter(IProcessRunner processRunner, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        this.processRunner = processRunner;
        this.timeout = timeout;
    }

    /// <inheritdoc />
    public async Task<bool> ToPngAsync(string source, string destination, int size, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        ProcessResult result = await this.processRunner
            .RunAsync(
                new ProcessStartOptions(
                    "sips",
                    ["-s", "format", "png", "-Z", size.ToString(CultureInfo.InvariantCulture), source, "--out", destination],
                    this.timeout),
                cancellationToken)
            .ConfigureAwait(false);

        return result.ExitCode == 0 && File.Exists(destination);
    }
}
