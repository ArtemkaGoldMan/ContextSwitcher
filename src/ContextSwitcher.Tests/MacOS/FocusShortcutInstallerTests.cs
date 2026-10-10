using System.Xml.Linq;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.MacOS;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.MacOS;

public sealed class FocusShortcutInstallerTests : IDisposable
{
    private readonly string work = Path.Combine(Path.GetTempPath(), "cs-focus-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(this.work))
        {
            Directory.Delete(this.work, recursive: true);
        }
    }

    /// <summary>
    /// A built-in mode becomes one "Set Focus" action that turns that mode on until turned off,
    /// signed by the system's own tool and opened - which is what makes Shortcuts ask to add it -
    /// under the exact name a switch will run.
    /// </summary>
    [Fact]
    public async Task ABuiltInModeIsSignedThenOpenedUnderTheNameASwitchRuns()
    {
        SigningRunner runner = new();
        FocusShortcutInstaller installer = new(runner, this.work);

        string? problem = await installer.OfferAsync("Work", turnOff: false, CancellationToken.None);

        Assert.Null(problem);
        Assert.Equal(["shortcuts", "open"], runner.Calls.Select(call => call.FileName));
        string signed = runner.Calls[1].Arguments.Single();
        Assert.Equal("ContextSwitcher - Focus Work.shortcut", Path.GetFileName(signed));
        Assert.Equal(["sign", "--mode", "anyone", "--input"], runner.Calls[0].Arguments.Take(4));

        XElement action = Assert.Single(Actions(File.ReadAllText(runner.Calls[0].Arguments[4])));
        Assert.Equal("is.workflow.actions.dnd.set", ValueAfter(action, "WFWorkflowActionIdentifier"));
        XElement parameters = DictAfter(action, "WFWorkflowActionParameters");
        Assert.Equal("1", ValueAfter(parameters, "Enabled"));
        Assert.Equal("Turned Off", ValueAfter(parameters, "AssertionType"));
        Assert.Equal("com.apple.focus.work", ValueAfter(DictAfter(parameters, "FocusModes"), "Identifier"));
    }

    /// <summary>
    /// A mode's Off Shortcut turns off that mode alone. One Off Shortcut for every built-in mode
    /// failed in real use - "a Focus named Reading does not exist on this device" - on a Mac that had
    /// never set Reading up, and stopped before turning anything else off.
    /// </summary>
    [Fact]
    public async Task AnOffShortcutTurnsOffOnlyItsOwnMode()
    {
        SigningRunner runner = new();
        await new FocusShortcutInstaller(runner, this.work).OfferAsync("Do Not Disturb", turnOff: true, CancellationToken.None);

        XElement action = Assert.Single(Actions(File.ReadAllText(runner.Calls[0].Arguments[4])));
        XElement parameters = DictAfter(action, "WFWorkflowActionParameters");
        Assert.Equal("0", ValueAfter(parameters, "Enabled"));
        Assert.Equal("com.apple.donotdisturb.mode.default", ValueAfter(DictAfter(parameters, "FocusModes"), "Identifier"));
        Assert.Equal("ContextSwitcher - Focus Off - Do Not Disturb.shortcut", Path.GetFileName(runner.Calls[1].Arguments.Single()));
    }

    [Fact]
    public async Task AModeTheUserMadeCannotBeWrittenAndSaysSo()
    {
        SigningRunner runner = new();

        string? problem = await new FocusShortcutInstaller(runner, this.work).OfferAsync("Deep Work", turnOff: false, CancellationToken.None);

        Assert.Contains("you made yourself", problem, StringComparison.Ordinal);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task AFailedSignatureIsReportedAndNothingIsOpened()
    {
        FakeProcessRunner runner = new() { DefaultResult = new ProcessResult(1, string.Empty, "Error: signing failed", false) };

        string? problem = await new FocusShortcutInstaller(runner, this.work).OfferAsync("Sleep", turnOff: false, CancellationToken.None);

        Assert.Equal("Couldn't prepare the Shortcut: Error: signing failed", problem);
        Assert.Single(runner.Calls);
    }

    /// <summary>
    /// The real `shortcuts sign` accepts what is generated - the step a typo would break. Not on CI:
    /// signing goes through the Mac's iCloud account, which a build server doesn't have.
    /// </summary>
    [Fact]
    public async Task TheGeneratedFileIsAcceptedByTheRealSigner()
    {
        if (!OperatingSystem.IsMacOS() || !File.Exists("/usr/bin/shortcuts") || Environment.GetEnvironmentVariable("CI") == "true")
        {
            return;
        }

        OpenNothingRunner runner = new();
        string? problem = await new FocusShortcutInstaller(runner, this.work).OfferAsync("Do Not Disturb", turnOff: true, CancellationToken.None);

        Assert.Null(problem);
        Assert.True(new FileInfo(runner.Opened!).Length > 0);
    }

    private static IEnumerable<XElement> Actions(string plist) =>
        DictAfter(XDocument.Parse(plist).Root!.Element("dict")!, "WFWorkflowActions").Elements("dict");

    private static XElement DictAfter(XElement dict, string key) =>
        dict.Elements("key").Single(k => k.Value == key).ElementsAfterSelf().First();

    private static string ValueAfter(XElement dict, string key) =>
        dict.Elements("key").Single(k => k.Value == key).ElementsAfterSelf().First().Value;

    /// <summary>Pretends to sign by copying the input to the output, so the flow can be followed without the real tool.</summary>
    private sealed class SigningRunner : Core.Abstractions.IProcessRunner
    {
        public List<ProcessStartOptions> Calls { get; } = [];

        public Task<ProcessResult> RunAsync(ProcessStartOptions options, CancellationToken cancellationToken)
        {
            this.Calls.Add(options);
            if (options.FileName == "shortcuts")
            {
                File.Copy(options.Arguments[4], options.Arguments[6]);
            }

            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty, false));
        }
    }

    /// <summary>Runs the real signer but never opens the result: that would put a dialog on screen.</summary>
    private sealed class OpenNothingRunner : Core.Abstractions.IProcessRunner
    {
        private readonly Infrastructure.ProcessExecution.ProcessRunner real = new();

        public string? Opened { get; private set; }

        public Task<ProcessResult> RunAsync(ProcessStartOptions options, CancellationToken cancellationToken)
        {
            if (options.FileName == "open")
            {
                this.Opened = options.Arguments.Single();
                return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty, false));
            }

            return this.real.RunAsync(options, cancellationToken);
        }
    }
}
