using System.Runtime.InteropServices;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>Windows' known folders, from <c>SHGetKnownFolderPath</c>, whose GUIDs the Start menu names them by.</summary>
public sealed class WindowsKnownFolders : IKnownFolders
{
    /// <inheritdoc />
    public string? PathOf(Guid folder)
    {
        int result = SHGetKnownFolderPath(in folder, 0, 0, out nint path);
        try
        {
            return result == 0 && path != 0 ? Marshal.PtrToStringUni(path) : null;
        }
        finally
        {
            Marshal.FreeCoTaskMem(path);
        }
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(in Guid folder, uint flags, nint token, out nint path);
}
