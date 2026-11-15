using ContextSwitcher.Infrastructure.Windows.Startup;
using Microsoft.Win32;

namespace ContextSwitcher.Tests.Windows.Startup;

/// <summary>What uninstalling takes out of what opens at sign-in: the app's own entry, and nobody else's.</summary>
public sealed class SignInEntryCleanupTests
{
    private const string Name = "ContextSwitcher";

    [Fact]
    public void TheAppsEntryAndItsNoteAreTakenOut()
    {
        FakeEntries entries = new();
        entries.Commands[Name] = "\"C:\\App\\ContextSwitcher.exe\"";
        entries.Approvals[Name] = [3, 0, 0, 0];

        SignInEntryCleanup.Remove(entries, LoginItemOptions.Default);

        Assert.Empty(entries.Commands);
        Assert.Empty(entries.Approvals);
    }

    [Fact]
    public void AnotherAppsEntryIsLeftWhereItIs()
    {
        FakeEntries entries = new();
        entries.Commands[Name] = "mine";
        entries.Commands["OneDrive"] = "theirs";
        entries.Approvals["OneDrive"] = [2, 0, 0, 0];

        SignInEntryCleanup.Remove(entries, LoginItemOptions.Default);

        Assert.Equal("theirs", entries.Commands["OneDrive"]);
        Assert.Equal<byte>([2, 0, 0, 0], entries.Approvals["OneDrive"]);
    }

    [Fact]
    public void ThereBeingNothingToTakeOutIsNotAProblem()
    {
        SignInEntryCleanup.Remove(new FakeEntries(), LoginItemOptions.Default);
    }

    /// <summary>Uninstalling must go on whatever Windows says: an entry left behind only fails to open what is no longer there.</summary>
    [Theory]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(System.Security.SecurityException))]
    [InlineData(typeof(IOException))]
    public void WindowsRefusingTheKeyDoesNotStopTheUninstall(Type failure)
    {
        FakeEntries entries = new() { FailWith = (Exception)Activator.CreateInstance(failure)! };

        SignInEntryCleanup.Remove(entries, LoginItemOptions.Default);
    }

    [Fact]
    public void AMistakeOfOurOwnIsNotSwallowed()
    {
        FakeEntries entries = new() { FailWith = new InvalidOperationException("a bug") };

        Assert.Throws<InvalidOperationException>(() => SignInEntryCleanup.Remove(entries, LoginItemOptions.Default));
    }

    [Fact]
    public void AgainstTheRealRegistryOnlyTheAppsOwnValueGoes()
    {
        string root = $@"Software\ContextSwitcherTests\{Guid.NewGuid():N}";
        LoginItemOptions options = new(Name, $@"{root}\Run", $@"{root}\Approved", []);
        try
        {
            using (RegistryKey run = Registry.CurrentUser.CreateSubKey(options.RunKey))
            {
                run.SetValue(Name, "mine");
                run.SetValue("Other", "theirs");
            }

            using (RegistryKey approved = Registry.CurrentUser.CreateSubKey(options.ApprovedKey))
            {
                approved.SetValue(Name, new byte[] { 3, 0, 0, 0 }, RegistryValueKind.Binary);
                approved.SetValue("Other", new byte[] { 2, 0, 0, 0 }, RegistryValueKind.Binary);
            }

            SignInEntryCleanup.Remove(new RegistryStartupEntries(options), options);

            using RegistryKey runAfter = Registry.CurrentUser.OpenSubKey(options.RunKey)!;
            using RegistryKey approvedAfter = Registry.CurrentUser.OpenSubKey(options.ApprovedKey)!;
            Assert.Null(runAfter.GetValue(Name));
            Assert.Equal("theirs", runAfter.GetValue("Other"));
            Assert.Null(approvedAfter.GetValue(Name));
            Assert.NotNull(approvedAfter.GetValue("Other"));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\ContextSwitcherTests", throwOnMissingSubKey: false);
        }
    }

    private sealed class FakeEntries : IStartupEntries
    {
        public Dictionary<string, string> Commands { get; } = [];

        public Dictionary<string, byte[]> Approvals { get; } = [];

        public Exception? FailWith { get; set; }

        public string? Command(string name) => this.Commands.GetValueOrDefault(name);

        public void SetCommand(string name, string command) => this.Commands[name] = command;

        public void RemoveCommand(string name)
        {
            this.Maybe();
            this.Commands.Remove(name);
        }

        public byte[]? Approval(string name) => this.Approvals.GetValueOrDefault(name);

        public void RemoveApproval(string name)
        {
            this.Maybe();
            this.Approvals.Remove(name);
        }

        private void Maybe()
        {
            if (this.FailWith is { } failure)
            {
                throw failure;
            }
        }
    }
}
