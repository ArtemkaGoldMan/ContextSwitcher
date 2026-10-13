using System.Text.Json;
using ContextSwitcher.App.Resources;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Serialization;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Settings' "Export and import": profiles and settings saved to a file - a copy to keep, or the
/// way to set up another Mac the same - and read back from one.
///
/// The file is the settings file itself, in the same format. An import is checked before anything
/// changes - a file that isn't Context Switcher's, or one that wouldn't pass the configuration check,
/// is turned down with the reason - and then asked about, since it replaces every profile. The
/// settings it replaces are backed up first, as on every save.
/// </summary>
public sealed class SettingsTransferViewModel : ViewModelBase
{
    private readonly ConfigurationStore configurationStore;
    private string? message;
    private string? problem;
    private AppConfiguration? pending;
    private string? pendingName;

    public SettingsTransferViewModel(ConfigurationStore configurationStore)
    {
        ArgumentNullException.ThrowIfNull(configurationStore);

        this.configurationStore = configurationStore;
        this.ExportCommand = new AsyncRelayCommand(this.ExportAsync);
        this.ImportCommand = new AsyncRelayCommand(this.ImportAsync);
        this.ConfirmImportCommand = new AsyncRelayCommand(this.ConfirmImportAsync);
        this.CancelImportCommand = new RelayCommand(() => this.SetPending(null, null));
    }

    /// <summary>Raised once an import has replaced the settings, for the page to show the new ones.</summary>
    public event EventHandler? Imported;

    /// <summary>The dialogs; set by the page, which has the window they belong to.</summary>
    public ISettingsFilePicker? FilePicker { get; set; }

    public AsyncRelayCommand ExportCommand { get; }

    public AsyncRelayCommand ImportCommand { get; }

    public AsyncRelayCommand ConfirmImportCommand { get; }

    public RelayCommand CancelImportCommand { get; }

    /// <summary>What happened: "Saved to …", "Imported …".</summary>
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

    public bool HasMessage => this.Message is not null;

    /// <summary>Why an export or import didn't happen.</summary>
    public string? Problem
    {
        get => this.problem;
        private set
        {
            if (this.SetProperty(ref this.problem, value))
            {
                this.OnPropertyChanged(nameof(this.HasProblem));
            }
        }
    }

    public bool HasProblem => this.Problem is not null;

    /// <summary>A checked file is waiting for the go-ahead to replace everything.</summary>
    public bool IsConfirmingImport => this.pending is not null;

    public string ConfirmText => Localized.Format(Strings.Settings_ImportConfirm, this.pendingName ?? string.Empty);

    private async Task ExportAsync()
    {
        this.Clear();
        if (this.FilePicker is null || await this.FilePicker.PickSaveAsync(Strings.Settings_ExportFileName).ConfigureAwait(true) is not { } file)
        {
            return;
        }

        try
        {
            Stream stream = await file.OpenAsync().ConfigureAwait(true);
            await using (stream.ConfigureAwait(true))
            {
                await JsonSerializer.SerializeAsync(stream, AppHost.Configuration, ContextSwitcherJson.Options).ConfigureAwait(true);
            }

            this.Message = Localized.Format(Strings.Settings_Exported, file.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.Problem = Localized.Format(Strings.Settings_ExportFailed, ex.Message);
        }
    }

    private async Task ImportAsync()
    {
        this.Clear();
        if (this.FilePicker is null || await this.FilePicker.PickOpenAsync().ConfigureAwait(true) is not { } file)
        {
            return;
        }

        AppConfiguration? imported;
        try
        {
            Stream stream = await file.OpenAsync().ConfigureAwait(true);
            await using (stream.ConfigureAwait(true))
            {
                imported = await JsonSerializer.DeserializeAsync<AppConfiguration>(stream, ContextSwitcherJson.Options).ConfigureAwait(true);
            }
        }
        catch (JsonException)
        {
            imported = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.Problem = Localized.Format(Strings.Settings_ImportInvalid, file.Name, ex.Message);
            return;
        }

        // Any JSON object reads as settings with nothing set; with no profiles it was never ours.
        if (imported is null || imported.Contexts.Count == 0)
        {
            this.Problem = Localized.Format(Strings.Settings_ImportNotSettings, file.Name);
            return;
        }

        ConfigurationValidationResult validation = this.configurationStore.Validate(imported);
        if (!validation.IsValid)
        {
            this.Problem = Localized.Format(Strings.Settings_ImportInvalid, file.Name, string.Join(" ", validation.Errors.Select(MessageText.Of)));
            return;
        }

        // Set up already: the wizard must not run again on this Mac because of where the file came from.
        this.SetPending(imported with { OnboardingCompleted = true }, file.Name);
    }

    private async Task ConfirmImportAsync()
    {
        if (this.pending is not { } configuration)
        {
            return;
        }

        string name = this.pendingName ?? string.Empty;
        this.SetPending(null, null);
        ConfigurationSaveResult result = await this.configurationStore.SaveAsync(configuration, CancellationToken.None).ConfigureAwait(true);
        if (!result.Succeeded)
        {
            this.Problem = Localized.Format(Strings.Settings_ImportInvalid, name, string.Join(" ", result.Errors));
            return;
        }

        this.Message = Localized.Format(Strings.Settings_Imported, name);
        this.Imported?.Invoke(this, EventArgs.Empty);
    }

    private void SetPending(AppConfiguration? configuration, string? name)
    {
        this.pending = configuration;
        this.pendingName = name;
        this.OnPropertyChanged(nameof(this.IsConfirmingImport));
        this.OnPropertyChanged(nameof(this.ConfirmText));
    }

    private void Clear()
    {
        this.Message = null;
        this.Problem = null;
        this.SetPending(null, null);
    }
}
