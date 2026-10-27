using System.Diagnostics;
using ContextSwitcher.Infrastructure.Windows.Applications;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Windows.Applications;

/// <summary>
/// The Windows calls themselves, asked about what is certain on any PC that runs the tests: this very
/// process. Nothing is ended or closed here - that is for the fakes to pretend.
/// </summary>
public sealed class SystemApiTests
{
    [Fact]
    public void ThisProcessIsAmongTheProcessesWithItsPathAndParent()
    {
        SystemProcesses system = new(new TestLogger());

        IReadOnlyList<ProcessInfo> all = system.List()!;

        ProcessInfo self = Assert.Single(all, process => process.Pid == system.CurrentProcessId);
        Assert.Equal(Environment.ProcessPath, self.ImagePath, ignoreCase: true);
        Assert.True(self.ParentPid > 0);
        Assert.Null(self.PackageFamilyName);
        using Process current = Process.GetCurrentProcess();
        Assert.Equal(current.StartTime.ToUniversalTime(), self.StartedAt!.Value, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ADescendantIsFoundThroughItsParentAndWhenItStarted()
    {
        SystemProcesses system = new(new TestLogger());
        using Process child = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 30 127.0.0.1 > nul") { CreateNoWindow = true, UseShellExecute = false })!;
        try
        {
            IReadOnlyList<ProcessInfo> all = system.List()!;
            ProcessInfo self = all.Single(process => process.Pid == system.CurrentProcessId);

            HashSet<int> tree = AppProcessTree.WithChildren([self], all, self: -1);

            Assert.Contains(child.Id, tree);
        }
        finally
        {
            child.Kill(entireProcessTree: true);
        }
    }

    /// <summary>The one thing here that ends a process: a child this test started and nothing else.</summary>
    [Fact]
    public void ATestsOwnChildProcessIsEnded()
    {
        SystemProcesses system = new(new TestLogger());
        using Process child = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 30 127.0.0.1 > nul") { CreateNoWindow = true, UseShellExecute = false })!;
        try
        {
            Assert.True(system.Terminate(child.Id));
            Assert.True(child.WaitForExit(5000));
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
            }
        }
    }

    [Fact]
    public void ANonexistentProcessCannotBeEnded()
    {
        Assert.False(new SystemProcesses(new TestLogger()).Terminate(int.MaxValue - 1));
    }

    [Fact]
    public void WindowsCanBeListedAndEachSaysWhoseItIsAndHowBig()
    {
        IReadOnlyList<WindowInfo> windows = new SystemWindows(new TestLogger()).List()!;

        Assert.All(windows, window => Assert.True(window.Pid >= 0 && window.Width >= 0 && window.Height >= 0));
    }

    /// <summary>Null would post to this app's own thread, and 0xFFFF is HWND_BROADCAST: every window on the desktop.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(0xFFFF)]
    [InlineData(-1)]
    [InlineData(0x7FFFFFF0)]
    public void ACloseRequestToWhatIsNotAWindowIsRefused(int handle)
    {
        Assert.False(new SystemWindows(new TestLogger()).RequestClose(handle));
    }

    /// <summary>Minimising or restoring "window 0" or HWND_BROADCAST would act on this app, or on every window.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(0xFFFF)]
    [InlineData(-1)]
    [InlineData(0x7FFFFFF0)]
    public void MinimisingOrRestoringWhatIsNotAWindowIsRefused(int handle)
    {
        SystemWindows windows = new(new TestLogger());

        Assert.False(windows.Minimize(handle));
        Assert.False(windows.Restore(handle));
    }

    [Fact]
    public void KnownFoldersAreWrittenOutAsWindowsDoes()
    {
        WindowsKnownFolders folders = new();

        Assert.Equal(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            folders.PathOf(Guid.Parse("7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E")),
            ignoreCase: true);
        Assert.Equal(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            folders.PathOf(Guid.Parse("F38BF404-1D43-42F2-9305-67DE0B28FC23")),
            ignoreCase: true);
        Assert.Null(folders.PathOf(Guid.Parse("00000000-0000-0000-0000-000000000001")));
    }
}
