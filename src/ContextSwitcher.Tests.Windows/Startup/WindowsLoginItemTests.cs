using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.Windows.Startup;
using ContextSwitcher.Tests.TestDoubles;
using Microsoft.Win32;

namespace ContextSwitcher.Tests.Windows.Startup;

/// <summary>Opening at sign-in as Windows keeps it: a command in <c>Run</c>, and Startup apps' note of its being turned off.</summary>
public sealed class WindowsLoginItemTests
{
    private const string Installed = @"C:\Users\someone\AppData\Local\ContextSwitcher\ContextSwitcher.exe";
    private const string Name = "ContextSwitcher";

    private readonly FakeStartupEntries entries = new();
    private readonly FakeProcessRunner runner = new();

    private WindowsLoginItem Item(string? path = Installed) => new(this.entries, LoginItemOptions.Default, path, this.runner);

    [Fact]
    public void AnInstalledAppStartsOff()
    {
        Assert.Equal(LoginItemStatus.Off, this.Item().Status);
    }

    [Fact]
    public void TurningItOnWritesItsQuotedPathAndThenItIsOn()
    {
        WindowsLoginItem item = this.Item();

        string? problem = item.SetEnabled(true);

        Assert.Null(problem);
        Assert.Equal($"\"{Installed}\"", this.entries.Commands[Name]);
        Assert.Equal(LoginItemStatus.On, item.Status);
    }

    [Fact]
    public void TurningItOffTakesTheEntryAwayAndKeepsNoNoteOfItsOwn()
    {
        WindowsLoginItem item = this.Item();
        item.SetEnabled(true);
        this.entries.Approvals[Name] = [2, 0, 0, 0];

        Assert.Null(item.SetEnabled(false));

        Assert.Empty(this.entries.Commands);
        Assert.Empty(this.entries.Approvals);
        Assert.Equal(LoginItemStatus.Off, item.Status);
    }

    [Fact]
    public void TurningOffWhatIsNotOnIsNotAFailure()
    {
        Assert.Null(this.Item().SetEnabled(false));
    }

    /// <summary>Turned off in Settings → Startup apps: the entry is there, and the note says no.</summary>
    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(1)]
    public void AnEntryTheyTurnedOffInStartupAppsNeedsApproval(byte first)
    {
        WindowsLoginItem item = this.Item();
        item.SetEnabled(true);
        this.entries.Approvals[Name] = [first, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        Assert.Equal(LoginItemStatus.NeedsApproval, item.Status);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(0)]
    public void AnEntryTheyLeftOnIsOn(byte first)
    {
        WindowsLoginItem item = this.Item();
        item.SetEnabled(true);
        this.entries.Approvals[Name] = [first, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        Assert.Equal(LoginItemStatus.On, item.Status);
    }

    [Fact]
    public void TurningItOnAgainIsTheApprovalAndTakesTheNoteAway()
    {
        WindowsLoginItem item = this.Item();
        item.SetEnabled(true);
        this.entries.Approvals[Name] = [3, 0, 0, 0];

        Assert.Null(item.SetEnabled(true));

        Assert.Empty(this.entries.Approvals);
        Assert.Equal(LoginItemStatus.On, item.Status);
    }

    [Fact]
    public void AnEntryWithNoNoteIsOn()
    {
        this.entries.Commands[Name] = $"\"{Installed}\"";

        Assert.Equal(LoginItemStatus.On, this.Item().Status);
    }

    /// <summary>The app was moved, or another copy wrote it: opening it would run what isn't here.</summary>
    [Fact]
    public void AnEntryThatNamesAnotherPathIsShownOffSoThatTurningItOnWritesThisOne()
    {
        this.entries.Commands[Name] = @"""D:\Old\ContextSwitcher.exe""";
        WindowsLoginItem item = this.Item();

        Assert.Equal(LoginItemStatus.Off, item.Status);

        item.SetEnabled(true);

        Assert.Equal($"\"{Installed}\"", this.entries.Commands[Name]);
    }

    [Fact]
    public void ThePathIsComparedWithoutCase()
    {
        this.entries.Commands[Name] = $"\"{Installed.ToUpperInvariant()}\"";

        Assert.Equal(LoginItemStatus.On, this.Item().Status);
    }

    [Fact]
    public void APathWithSpacesIsQuotedAsItNeedsToBe()
    {
        WindowsLoginItem item = this.Item(@"C:\Program Files\Context Switcher\ContextSwitcher.exe");

        item.SetEnabled(true);

        Assert.Equal(@"""C:\Program Files\Context Switcher\ContextSwitcher.exe""", this.entries.Commands[Name]);
    }

    /// <summary>A build run from the code would open at every sign-in for as long as its folder stood.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ContextSwitcher.exe")]
    [InlineData(@"C:\Apps\ContextSwitcher")]
    [InlineData(@"C:\Apps\ContextSwitcher.dll")]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe")]
    [InlineData(@"C:\Code\ContextSwitcher\src\ContextSwitcher.App\bin\Debug\net10.0-windows10.0.19041.0\ContextSwitcher.App.exe")]
    [InlineData(@"C:\Code\ContextSwitcher\src\ContextSwitcher.App\BIN\RELEASE\net10.0\ContextSwitcher.App.exe")]
    public void AnAppThatIsNotInstalledIsNotOffered(string? path)
    {
        WindowsLoginItem item = this.Item(path);

        Assert.Equal(LoginItemStatus.Unavailable, item.Status);
        Assert.NotNull(item.SetEnabled(true));
        Assert.Empty(this.entries.Commands);
    }

    [Fact]
    public void WindowsRefusingTheWriteIsSaidAndNothingIsClaimed()
    {
        this.entries.FailWith = new UnauthorizedAccessException("Access to the registry key is denied.");
        WindowsLoginItem item = this.Item();

        string? problem = item.SetEnabled(true);

        Assert.Contains("denied", problem);
        Assert.Empty(this.entries.Commands);
    }

    [Fact]
    public void AKeyThatCannotBeReadIsNotOffered()
    {
        this.entries.FailWith = new System.Security.SecurityException("no");

        Assert.Equal(LoginItemStatus.Unavailable, this.Item().Status);
    }

    [Fact]
    public void SettingsIsOpenedAtStartupApps()
    {
        this.Item().OpenSystemSettings();

        ProcessStartOptions call = Assert.Single(this.runner.Calls);
        Assert.Equal("explorer", call.FileName);
        Assert.Equal(["ms-settings:startupapps"], call.Arguments);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(new byte[0], false)]
    [InlineData(new byte[] { 2 }, false)]
    [InlineData(new byte[] { 6, 1, 2 }, false)]
    [InlineData(new byte[] { 3 }, true)]
    [InlineData(new byte[] { 7, 0, 0 }, true)]
    public void OnlyAnOddFirstByteIsTurnedOff(byte[]? approval, bool turnedOff)
    {
        Assert.Equal(turnedOff, WindowsLoginItem.TurnedOff(approval));
    }

    // The same, against the real registry, in a key of the test's own.

    [Fact]
    public void AgainstTheRealRegistryTheEntryIsWrittenReadAndTakenAway()
    {
        string root = $@"Software\ContextSwitcherTests\{Guid.NewGuid():N}";
        LoginItemOptions options = new(Name, $@"{root}\Run", $@"{root}\Approved", []);
        try
        {
            WindowsLoginItem item = new(new RegistryStartupEntries(options), options, Installed, this.runner);
            Assert.Equal(LoginItemStatus.Off, item.Status);

            Assert.Null(item.SetEnabled(true));

            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(options.RunKey)!)
            {
                Assert.Equal($"\"{Installed}\"", run.GetValue(Name));
            }

            Assert.Equal(LoginItemStatus.On, item.Status);

            using (RegistryKey approved = Registry.CurrentUser.CreateSubKey(options.ApprovedKey))
            {
                approved.SetValue(Name, new byte[] { 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
            }

            Assert.Equal(LoginItemStatus.NeedsApproval, item.Status);

            Assert.Null(item.SetEnabled(true));
            Assert.Equal(LoginItemStatus.On, item.Status);

            Assert.Null(item.SetEnabled(false));
            Assert.Equal(LoginItemStatus.Off, item.Status);
            using RegistryKey after = Registry.CurrentUser.OpenSubKey(options.RunKey)!;
            Assert.Null(after.GetValue(Name));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\ContextSwitcherTests", throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void TheRealRegistryAnswersNothingForWhatIsNotThereAndLosesNothingTakingItAway()
    {
        string root = $@"Software\ContextSwitcherTests\{Guid.NewGuid():N}";
        RegistryStartupEntries real = new(new LoginItemOptions(Name, $@"{root}\Run", $@"{root}\Approved", []));
        try
        {
            Assert.Null(real.Command(Name));
            Assert.Null(real.Approval(Name));
            real.RemoveCommand(Name);
            real.RemoveApproval(Name);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\ContextSwitcherTests", throwOnMissingSubKey: false);
        }
    }

    /// <summary>The real keys are the ones Windows' own Startup apps reads, so the default must point at those and nowhere else.</summary>
    [Fact]
    public void TheDefaultKeysAreTheOnesWindowsReads()
    {
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", LoginItemOptions.Default.RunKey);
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", LoginItemOptions.Default.ApprovedKey);
        Assert.Equal(Name, LoginItemOptions.Default.ValueName);
    }

    private sealed class FakeStartupEntries : IStartupEntries
    {
        public Dictionary<string, string> Commands { get; } = [];

        public Dictionary<string, byte[]> Approvals { get; } = [];

        public Exception? FailWith { get; set; }

        public string? Command(string name)
        {
            this.Maybe();
            return this.Commands.GetValueOrDefault(name);
        }

        public void SetCommand(string name, string command)
        {
            this.Maybe();
            this.Commands[name] = command;
        }

        public void RemoveCommand(string name)
        {
            this.Maybe();
            this.Commands.Remove(name);
        }

        public byte[]? Approval(string name)
        {
            this.Maybe();
            return this.Approvals.GetValueOrDefault(name);
        }

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
