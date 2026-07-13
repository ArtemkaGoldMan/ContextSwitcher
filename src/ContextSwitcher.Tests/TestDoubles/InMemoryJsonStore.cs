using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Tests.TestDoubles;

public sealed class InMemoryJsonStore : IJsonStore
{
    private readonly Dictionary<string, object> values = [];

    public HashSet<string> WriteFailurePaths { get; } = [];

    public void Seed<T>(string path, T value)
        where T : class
    {
        this.values[path] = value;
    }

    public T? Get<T>(string path)
        where T : class
    {
        return this.values.TryGetValue(path, out object? value) ? value as T : null;
    }

    public Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken = default)
        where T : class
    {
        return Task.FromResult(this.values.TryGetValue(path, out object? value) ? value as T : null);
    }

    public Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken = default)
        where T : class
    {
        if (this.WriteFailurePaths.Contains(path))
        {
            throw new IOException($"Simulated write failure for '{path}'.");
        }

        this.values[path] = value;
        return Task.CompletedTask;
    }
}
