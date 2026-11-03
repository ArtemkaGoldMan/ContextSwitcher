namespace ContextSwitcher.App;

/// <summary>
/// Where the app starts on the Mac: straight into <see cref="Program.Run"/>. A bundle is dragged in and
/// out and has no installer to answer. Windows' is in <c>Entry.Windows.cs</c>; the build brings in one.
/// </summary>
internal static class Entry
{
    [STAThread]
    public static void Main(string[] args) => Program.Run(args);
}