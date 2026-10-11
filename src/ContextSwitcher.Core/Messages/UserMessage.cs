using System.Globalization;

namespace ContextSwitcher.Core.Messages;

/// <summary>
/// Something to tell the person using the app - a step that went wrong, a setting that isn't valid -
/// in two forms: <see cref="English"/>, for the log and the command line, which stay English so bug
/// reports are readable; and <see cref="Key"/> with its <see cref="Arguments"/>, which the app's
/// window looks up in its translations (<c>Msg_&lt;Key&gt;</c> in Strings.resx).
/// </summary>
public sealed record UserMessage(string Key, IReadOnlyList<string> Arguments, string English)
{
    public override string ToString() => this.English;
}

/// <summary>One kind of message: its key and its English pattern, such as "Could not close: {0}.".</summary>
public sealed record MessageTemplate(string Key, string English)
{
    /// <summary>The message with <paramref name="arguments"/> filled in.</summary>
    public UserMessage With(params string[] arguments) =>
        new(this.Key, arguments, string.Format(CultureInfo.InvariantCulture, this.English, arguments));
}
