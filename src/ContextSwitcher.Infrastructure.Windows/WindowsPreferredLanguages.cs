using System.Globalization;
using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Windows;

/// <summary>
/// Windows' display language, the one its own windows are in. Windows keeps a longer list of preferred
/// languages, but .NET is handed the first, which is the one people choose.
/// </summary>
public sealed class WindowsPreferredLanguages : IPreferredLanguages
{
    /// <inheritdoc />
    public IReadOnlyList<string> Get() =>
        CultureInfo.CurrentUICulture is { Name.Length: > 0 } culture ? [culture.Name] : [];
}
