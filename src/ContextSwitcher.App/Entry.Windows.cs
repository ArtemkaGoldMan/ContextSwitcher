using ContextSwitcher.Infrastructure.Windows.Startup;
using Velopack;

namespace ContextSwitcher.App;

/// <summary>
/// Where the app starts on Windows. Velopack's Update.exe runs a copy with a word of its own as it
/// installs, updates or uninstalls it; <c>VelopackApp</c> answers that and the copy ends, and started any
/// other way it returns at once and the app goes on. It has to be called here, in <c>Main</c> itself: the
/// packer reads this method to check it, and refuses an app in which it isn't. The Mac's is in
/// <c>Entry.cs</c>; the build brings in one.
/// </summary>
internal static class Entry
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => SignInEntryCleanup.Remove(new RegistryStartupEntries(LoginItemOptions.Default), LoginItemOptions.Default))
            .Run();

        Program.Run(args);
    }
}