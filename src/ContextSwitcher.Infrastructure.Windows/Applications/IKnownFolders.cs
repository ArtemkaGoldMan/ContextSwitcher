namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>Windows' known folders - Program Files, the Windows folder - by the GUID the Start menu names them with.</summary>
public interface IKnownFolders
{
    /// <summary>The folder's path; null when Windows doesn't know the GUID.</summary>
    string? PathOf(Guid folder);
}
