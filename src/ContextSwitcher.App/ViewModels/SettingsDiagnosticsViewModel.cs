using ContextSwitcher.App.Resources;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.Logging;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Settings' "Diagnostics": the detailed log, and what to send when something is wrong - and
/// "Contact", where it is sent: an email in the person's own mail app, or an issue on GitHub. Both
/// copy the diagnostics first, to paste in. Nothing leaves the Mac from here: the person sends it.
///
/// The log is quiet - only problems, each with what led to it. The detailed log is for the case where
/// nothing failed but something is off: everything the app does is written for a week, then it goes
/// quiet again by itself. The report is the log and the settings that matter, in one click, for a
/// message.
/// </summary>
public sealed class SettingsDiagnosticsViewModel : ViewModelBase
{
    /// <summary>How long the detailed log stays on once it is turned on.</summary>
    public static readonly TimeSpan DetailedFor = TimeSpan.FromDays(7);

    private readonly ConfigurationStore configurationStore;
    private readonly IProcessRunner processRunner;
    private readonly DiagnosticsSources? sources;
    private readonly Func<bool?> automation;
    private string? message;
    private string? contactMessage;

    public SettingsDiagnosticsViewModel(
        ConfigurationStore configurationStore,
        IProcessRunner processRunner,
        DiagnosticsSources? sources = null,
        Func<bool?>? automation = null)
    {
        ArgumentNullException.ThrowIfNull(configurationStore);
        ArgumentNullException.ThrowIfNull(processRunner);

        this.configurationStore = configurationStore;
        this.processRunner = processRunner;
        this.sources = sources;
        this.automation = automation ?? (() => null);
        this.CopyCommand = new AsyncRelayCommand(this.CopyAsync);
        this.ShowLogCommand = new RelayCommand(this.ShowLog);
        this.WriteEmailCommand = new AsyncRelayCommand(this.WriteEmailAsync);
        this.OpenIssueCommand = new AsyncRelayCommand(this.OpenIssueAsync);
        this.WhatToWriteCommand = new RelayCommand(() => this.Open(AppLinks.Contact));
    }

    /// <summary>Puts text on the clipboard, said whether it could; set by the page, which has the window the clipboard belongs to.</summary>
    public Func<string, Task<bool>>? ClipboardWriter { get; set; }

    public AsyncRelayCommand CopyCommand { get; }

    public RelayCommand ShowLogCommand { get; }

    public AsyncRelayCommand WriteEmailCommand { get; }

    public AsyncRelayCommand OpenIssueCommand { get; }

    public RelayCommand WhatToWriteCommand { get; }

    /// <summary>The address, for anyone who would rather write from a mail of their own.</summary>
    public string ContactEmail => AppLinks.ContactEmail;

    /// <summary>What happened to the last click in Contact: the message opened, and whether the diagnostics are copied.</summary>
    public string? ContactMessage
    {
        get => this.contactMessage;
        private set
        {
            if (this.SetProperty(ref this.contactMessage, value))
            {
                this.OnPropertyChanged(nameof(this.HasContactMessage));
            }
        }
    }

    public bool HasContactMessage => !string.IsNullOrEmpty(this.ContactMessage);

    /// <summary>Whether everything is being written to the log, as it happens, for now.</summary>
    public bool DetailedLog
    {
        get => AppHost.Configuration.DetailedLogUntil is { } until && until > this.Now;
        set
        {
            if (value != this.DetailedLog)
            {
                _ = this.SetDetailedLogAsync(value);
            }
        }
    }

    /// <summary>"On until 21.10.2026." while it is on; nothing otherwise.</summary>
    public string DetailedLogStatus => this.DetailedLog && AppHost.Configuration.DetailedLogUntil is { } until
        ? Localized.Format(Strings.Settings_DetailedLog_Until, until.ToLocalTime())
        : string.Empty;

    public bool HasDetailedLogStatus => this.DetailedLog;

    /// <summary>What happened to the last click: "Copied.", or why not.</summary>
    public string? Message
    {
        get => this.message;
        private set
        {
            if (this.SetProperty(ref this.message, value))
            {
                this.OnPropertyChanged(nameof(this.HasMessage));
            }
        }
    }

    public bool HasMessage => !string.IsNullOrEmpty(this.Message);

    /// <summary>Reads the setting again - after an import replaced the settings.</summary>
    public void Refresh()
    {
        this.OnPropertyChanged(nameof(this.DetailedLog));
        this.OnPropertyChanged(nameof(this.DetailedLogStatus));
        this.OnPropertyChanged(nameof(this.HasDetailedLogStatus));
    }

    private DateTimeOffset Now => this.sources?.Now() ?? DateTimeOffset.UtcNow;

    private async Task SetDetailedLogAsync(bool on)
    {
        AppConfiguration updated = AppHost.Configuration with { DetailedLogUntil = on ? this.Now + DetailedFor : null };
        try
        {
            ConfigurationSaveResult result = await this.configurationStore.SaveAsync(updated, CancellationToken.None).ConfigureAwait(true);
            this.Message = result.Succeeded ? null : Localized.Format(Strings.Settings_CouldNotSave, string.Join(" ", result.Errors));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.Message = Localized.Format(Strings.Settings_CouldNotSave, ex.Message);
        }

        this.Refresh();
    }

    private async Task CopyAsync()
    {
        bool copied = await this.CopyReportAsync().ConfigureAwait(true);
        this.Message = copied ? Strings.Settings_Diagnostics_Copied : Strings.Settings_Diagnostics_CouldNotCopy;
    }

    private async Task<bool> CopyReportAsync() =>
        this.ClipboardWriter is { } write && await write(this.BuildReport()).ConfigureAwait(true);

    /// <summary>
    /// A message to the developer in the person's own mail app, with the questions worth answering
    /// already in it, and the diagnostics copied to paste below them - a mail link can't carry an
    /// attachment, and the log is too long for one. No mail app opening is told with the address.
    /// </summary>
    private async Task WriteEmailAsync()
    {
        bool copied = await this.CopyReportAsync().ConfigureAwait(true);
        RunInfo run = this.Run();
        string link = MailTo(
            AppLinks.ContactEmail,
            Localized.Format(Strings.Contact_EmailSubject, run.Version),
            Localized.Format(Strings.Contact_EmailBody, run.Version, run.MacOs));
        ProcessResult opened = await this.processRunner.RunAsync(new ProcessStartOptions("open", [link], TimeSpan.FromSeconds(10)), CancellationToken.None).ConfigureAwait(true);
        this.ContactMessage = opened.ExitCode != 0
            ? Localized.Format(Strings.Contact_NoMailApp, AppLinks.ContactEmail)
            : copied ? Strings.Contact_EmailOpened : Strings.Contact_EmailOpenedNotCopied;
    }

    /// <summary>GitHub's issue form, whose Diagnostics field takes the report copied just before.</summary>
    private async Task OpenIssueAsync()
    {
        bool copied = await this.CopyReportAsync().ConfigureAwait(true);
        this.Open(AppLinks.NewIssue);
        this.ContactMessage = copied ? Strings.Contact_IssueOpened : Strings.Contact_IssueOpenedNotCopied;
    }

    /// <summary>
    /// A <c>mailto:</c> link with its subject and body: escaped, with the line breaks mail links
    /// expect (RFC 6068).
    /// </summary>
    public static string MailTo(string address, string subject, string body) =>
        $"mailto:{address}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body.ReplaceLineEndings("\r\n"))}";

    private void Open(string url) =>
        _ = this.processRunner.RunAsync(new ProcessStartOptions("open", [url], TimeSpan.FromSeconds(5)), CancellationToken.None);

    private RunInfo Run() =>
        this.sources?.Run() ?? RunInfo.Current(UpdatesViewModel.RunningVersion.ToString(3), Infrastructure.Files.AppBuild.IsDevelopment);

    /// <summary>The report as it would be copied now.</summary>
    public string BuildReport()
    {
        string logPath = this.sources?.Paths.AppLogPath ?? string.Empty;
        List<string> log = [];
        try
        {
            if (logPath.Length > 0 && File.Exists(logPath))
            {
                log.AddRange(File.ReadLines(logPath).TakeLast(DiagnosticsReport.LogLines));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Add($"(the log could not be read: {ex.GetType().Name})");
        }

        return DiagnosticsReport.Build(new DiagnosticsInput(
            this.Run(),
            AppHost.Configuration,
            this.automation(),
            AppHost.ConfigurationValidation?.Errors.Count ?? 0,
            log,
            this.sources?.Pending() ?? [],
            this.Now));
    }

    /// <summary>The log in Finder; when there is none - a quiet week - the folder it would be in.</summary>
    private void ShowLog()
    {
        if (this.sources is not { } known)
        {
            return;
        }

        IReadOnlyList<string> arguments = File.Exists(known.Paths.AppLogPath) ? ["-R", known.Paths.AppLogPath] : [known.Paths.BaseDirectory];
        _ = this.processRunner.RunAsync(new ProcessStartOptions("open", arguments, TimeSpan.FromSeconds(5)), CancellationToken.None);
    }
}
