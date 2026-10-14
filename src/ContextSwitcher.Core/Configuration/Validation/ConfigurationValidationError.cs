using ContextSwitcher.Core.Messages;

namespace ContextSwitcher.Core.Configuration.Validation;

public sealed record ConfigurationValidationError(string Path, string Message)
{
    /// <summary>The same message in a form the app's window can translate.</summary>
    public UserMessage? Detail { get; init; }

    /// <summary>The profile the problem is in, by name; null for one that isn't in a profile.</summary>
    public string? Subject { get; init; }

    public static ConfigurationValidationError Of(string path, UserMessage message) => new(path, message.English) { Detail = message };
}
