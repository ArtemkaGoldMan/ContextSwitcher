using ContextSwitcher.App.Resources;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.Logging;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Settings' "Diagnostics": the detailed log, and what to send when something is wrong.
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
    }

    /// <summary>Puts text on the clipboard, said whether it could; set by the page, which has the window the clipboard belongs to.</summary>
    public Func<string, Task<bool>>? ClipboardWriter { get; set; }

    public AsyncRelayCommand CopyCommand { get; }

    public RelayCommand ShowLogCommand { get; }

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
        bool copied = this.ClipboardWriter is { } write && await write(this.BuildReport()).ConfigureAwait(true);
        this.Message = copied ? Strings.Settings_Diagnostics_Copied : Strings.Settings_Diagnostics_CouldNotCopy;
    }

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
            this.sources?.Run() ?? RunInfo.Current(UpdatesViewModel.RunningVersion.ToString(3), Infrastructure.Files.AppBuild.IsDevelopment),
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
