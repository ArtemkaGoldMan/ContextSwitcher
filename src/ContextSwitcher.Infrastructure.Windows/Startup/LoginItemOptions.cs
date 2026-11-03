namespace ContextSwitcher.Infrastructure.Windows.Startup;

/// <summary>Where Windows keeps what opens at sign-in, and which builds aren't offered it.</summary>
/// <param name="ValueName">The name the app's entry has in both keys.</param>
/// <param name="RunKey">The key under the current user whose values are commands Windows runs at sign-in.</param>
/// <param name="ApprovedKey">
/// The key under the current user where Startup apps in Windows' Settings and the Task Manager record that
/// an entry was turned off: a value of the same name, of which the first byte is odd.
/// </param>
/// <param name="BuildFolders">A program run from a folder with one of these in its path is a build from the code, not an installed app.</param>
public sealed record LoginItemOptions(string ValueName, string RunKey, string ApprovedKey, IReadOnlyList<string> BuildFolders)
{
    /// <summary>
    /// <c>Run</c> is what every Windows app that opens at sign-in uses, and what Startup apps lists;
    /// <c>StartupApproved\Run</c> is how that list remembers a person's turning one off. A Debug or
    /// Release build run from the code lives in <c>bin\Debug</c> or <c>bin\Release</c>, and is never
    /// put there: it would open at every sign-in for as long as the folder stood.
    /// </summary>
    public static LoginItemOptions Default { get; } = new(
        "ContextSwitcher",
        @"Software\Microsoft\Windows\CurrentVersion\Run",
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
        [@"\bin\Debug\", @"\bin\Release\"]);
}
