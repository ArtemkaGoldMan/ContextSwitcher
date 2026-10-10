namespace ContextSwitcher.Core.Updates;

/// <summary>
/// An update could not be checked for or installed. The message is written for the person reading
/// the Settings page, not for a log.
/// </summary>
public sealed class UpdateException : Exception
{
    public UpdateException(string message)
        : base(message)
    {
    }

    public UpdateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
