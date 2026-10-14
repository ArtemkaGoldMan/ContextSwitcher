using ContextSwitcher.App.Services;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.Logging;

namespace ContextSwitcher.Tests.App.Services;

public sealed class DiagnosticsReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 13, 0, 0, TimeSpan.Zero);

    private static readonly AppConfiguration Configuration = new()
    {
        ActiveContextId = "work",
        Language = "uk",
        AskOnSwitch = true,
        IgnoredApps = ["Telegram", "Messages"],
        Contexts =
        [
            new ContextDefinition
            {
                Id = "work",
                DisplayName = "Quarterly secret project",
                LaunchApps = ["Slack", "Cursor"],
                CloseApps = ["Spotify"],
                BrowserManagement = new BrowserManagementConfig { Mode = BrowserManagementMode.Urls, Urls = ["https://linear.app/private-team"] }
            },
            new ContextDefinition { Id = "play", DisplayName = "Evenings", HideApps = ["Mail"] }
        ]
    };

    [Fact]
    public void ItSaysWhichAppOnWhichMacAndWhatIsSwitchedOn()
    {
        string report = DiagnosticsReport.Build(Input());

        Assert.Contains("Version: 0.6.1 (release)", report, StringComparison.Ordinal);
        Assert.Contains("macOS: 26.6 (arm64)", report, StringComparison.Ordinal);
        Assert.Contains("Language: uk", report, StringComparison.Ordinal);
        Assert.Contains("Automation permission: not granted", report, StringComparison.Ordinal);
        Assert.Contains("Profiles: 2", report, StringComparison.Ordinal);
        Assert.Contains("browser: pages 1, browser profiles 0, none 1", report, StringComparison.Ordinal);
        Assert.Contains("apps to open 2, to close 1, to hide 1", report, StringComparison.Ordinal);
        Assert.Contains("Ask about other apps and tabs: on, never-touch apps: 2", report, StringComparison.Ordinal);
    }

    /// <summary>It is made to be sent, so it is counts and settings: nobody's profile names, apps or addresses.</summary>
    [Fact]
    public void ItHoldsCountsAndNeverNamesOrAddresses()
    {
        string report = DiagnosticsReport.Build(Input());

        Assert.DoesNotContain("secret", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Evenings", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Telegram", report, StringComparison.Ordinal);
        Assert.DoesNotContain("linear.app", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Slack", report, StringComparison.Ordinal);
    }

    /// <summary>A log an older version wrote can hold an address or an email; it is cleaned again here.</summary>
    [Fact]
    public void TheLogInItIsCleanedAgain()
    {
        string report = DiagnosticsReport.Build(Input(logLines: ["{\"message\":\"Could not open 'https://mail.example/inbox/42' for me@example.com\"}"]));

        Assert.Contains("https://mail.example/…", report, StringComparison.Ordinal);
        Assert.DoesNotContain("inbox/42", report, StringComparison.Ordinal);
        Assert.DoesNotContain("me@example.com", report, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheLastLinesOfALongLogAreIncluded()
    {
        List<string> lines = Enumerable.Range(0, DiagnosticsReport.LogLines + 25).Select(i => $"line {i}").ToList();

        string report = DiagnosticsReport.Build(Input(logLines: lines));

        Assert.Contains("line 25", report, StringComparison.Ordinal);
        Assert.DoesNotContain("line 24\n", report.Replace("\r", string.Empty), StringComparison.Ordinal);
        Assert.Contains($"line {DiagnosticsReport.LogLines + 24}", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ANothingLogIsSaidSo()
    {
        string report = DiagnosticsReport.Build(Input());

        Assert.Equal(2, report.Split("  (nothing)").Length - 1);
    }

    [Fact]
    public void WhatIsOnlyInMemoryIsListedToo()
    {
        LogEntry entry = new()
        {
            Timestamp = Now,
            Level = LogLevel.Information,
            Category = "ContextSwitch",
            EventId = "SwitchPlanned",
            Message = "Planned 3 steps."
        };

        string report = DiagnosticsReport.Build(Input(pending: [entry]));

        Assert.Contains("13:00:00 Information ContextSwitch/SwitchPlanned: Planned 3 steps.", report, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDetailedLogIsSaidToBeOnWhileItIs()
    {
        AppConfiguration on = Configuration with { DetailedLogUntil = Now.AddDays(3) };
        AppConfiguration expired = Configuration with { DetailedLogUntil = Now.AddDays(-1) };

        Assert.Contains("Detailed log: on until 2026-10-17", DiagnosticsReport.Build(Input(on)), StringComparison.Ordinal);
        Assert.Contains("Detailed log: off", DiagnosticsReport.Build(Input(expired)), StringComparison.Ordinal);
    }

    private static DiagnosticsInput Input(AppConfiguration? config = null, IReadOnlyList<string>? logLines = null, IReadOnlyList<LogEntry>? pending = null) => new(
        new RunInfo("ContextSwitcher", "0.6.1", "26.6", "arm64", "release", 1),
        config ?? Configuration,
        false,
        0,
        logLines ?? [],
        pending ?? [],
        Now);
}
