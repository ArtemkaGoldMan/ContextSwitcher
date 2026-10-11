using ContextSwitcher.Core.Messages;

namespace ContextSwitcher.Core.Updates;

/// <summary>
/// An update could not be checked for or installed. The message is written for the person reading
/// the Settings page, not for a log.
/// </summary>
public sealed class UpdateException : Exception
{
    public UpdateException(UserMessage detail)
        : base(detail?.English)
    {
        this.Detail = detail!;
    }

    public UpdateException(UserMessage detail, Exception innerException)
        : base(detail?.English, innerException)
    {
        this.Detail = detail!;
    }

    /// <summary>The message, in a form the app's window can translate.</summary>
    public UserMessage Detail { get; }
}
