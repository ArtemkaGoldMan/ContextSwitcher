using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Messages;

namespace ContextSwitcher.App.Resources;

/// <summary>
/// A message from the switch engine, the configuration check or the updater, in the app's language:
/// its <c>Msg_&lt;Key&gt;</c> translation with its values filled in, or its English when there is no
/// key (a switch recorded before messages had keys) or no translation.
/// </summary>
public static class MessageText
{
    public static string Of(UserMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (Strings.ResourceManager.GetString($"Msg_{message.Key}", Strings.Culture) is not { } pattern)
        {
            return message.English;
        }

        try
        {
            return Localized.Format(pattern, [.. message.Arguments]);
        }
        catch (FormatException)
        {
            // A translation that asks for a value the message doesn't carry; the English is still right.
            return message.English;
        }
    }

    public static string Of(StateError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.MessageKey is { } key ? Of(new UserMessage(key, error.MessageArguments, error.Message)) : error.Message;
    }

    /// <summary>"Work → accentColor: …" - the profile and setting it is about, then what's wrong with it.</summary>
    public static string Of(ConfigurationValidationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        string where = error.Subject is { } profile
            ? $"{profile} → {ProfilePrefix.Replace(error.Path, string.Empty)}"
            : error.Path;
        return $"{where}: {(error.Detail is { } detail ? Of(detail) : error.Message)}";
    }

    private static readonly System.Text.RegularExpressions.Regex ProfilePrefix = new(@"^contexts\[\d+\]\.?");
}
