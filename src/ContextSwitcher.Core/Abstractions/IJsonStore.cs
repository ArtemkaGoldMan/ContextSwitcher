namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Provides a small persistence abstraction for reading and writing JSON documents.
/// </summary>
public interface IJsonStore
{
    /// <summary>
    /// Reads a JSON file from disk and deserializes it into the requested CLR type.
    /// </summary>
    /// <typeparam name="T">The CLR type to deserialize into.</typeparam>
    /// <param name="path">The path to the JSON file.</param>
    /// <param name="cancellationToken">A token that can cancel the read.</param>
    /// <returns>The deserialized value, or <see langword="null"/> when the file does not exist.</returns>
    Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Writes a JSON value to disk using an atomic write strategy.
    /// </summary>
    /// <typeparam name="T">The CLR type to serialize.</typeparam>
    /// <param name="path">The destination path for the JSON file.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="cancellationToken">A token that can cancel the write.</param>
    Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken = default)
        where T : class;
}
