using ContextSwitcher.Core.Logging;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.App.Resources;

namespace ContextSwitcher.App.Services;

/// <summary>
/// The single place the Main App window's view models go through to persist edits to
/// <c>settings.json</c>: re-validates, backs up the previous file, writes atomically, and publishes it
/// in <see cref="IAppState"/> so the Dashboard reflects the change without a restart (docs/architecture.md,
/// "The composition root", and the backup before each save in "Files").
/// </summary>
public sealed class ConfigurationStore
{
    private readonly IJsonStore jsonStore;
    private readonly ConfigPaths configPaths;
    private readonly ConfigurationValidator validator;
    private readonly IAppState appState;
    private readonly ILogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationStore"/> class.
    /// </summary>
    public ConfigurationStore(IJsonStore jsonStore, ConfigPaths configPaths, ConfigurationValidator validator, IAppState appState, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(jsonStore);
        ArgumentNullException.ThrowIfNull(configPaths);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(appState);
        ArgumentNullException.ThrowIfNull(logger);

        this.jsonStore = jsonStore;
        this.configPaths = configPaths;
        this.validator = validator;
        this.appState = appState;
        this.logger = logger;
    }

    /// <summary>Checks <paramref name="configuration"/> as a save would, without saving it.</summary>
    public ConfigurationValidationResult Validate(AppConfiguration configuration) => this.validator.Validate(configuration);

    /// <summary>
    /// Validates <paramref name="configuration"/> and, only if valid, backs up the current
    /// <c>settings.json</c>, writes the new configuration, and publishes it via
    /// <see cref="IAppState.UpdateConfiguration"/>. Never persists an invalid configuration.
    /// </summary>
    public async Task<ConfigurationSaveResult> SaveAsync(AppConfiguration configuration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        ConfigurationValidationResult validation = this.validator.Validate(configuration);
        if (!validation.IsValid)
        {
            // A save the editors should not have been able to ask for. Which settings, by their paths
            // without the profile's number - nothing the person typed.
            this.logger.Warn(
                "Configuration",
                "SaveRejected",
                $"A save was refused with {validation.Errors.Count} problem(s): "
                    + validation.DescribeForLog());
            return ConfigurationSaveResult.Invalid(validation.Errors.Select(MessageText.Of).ToList());
        }

        // ConfigureAwait(true): UpdateConfiguration below raises ConfigurationChanged inline, and
        // its subscribers rebuild view models that construct Avalonia objects. Resuming on the
        // thread pool meant those were built off the UI thread - the brushes among them ended up
        // owned by a pool thread, and the next frame that drew one threw "the calling thread cannot
        // access this object because a different thread owns it" from inside Border.Render.
        try
        {
            await this.jsonStore.BackupAsync(this.configPaths.SettingsPath, this.configPaths.BackupsDirectory, cancellationToken)
                .ConfigureAwait(true);
            await this.jsonStore.WriteAsync(this.configPaths.SettingsPath, configuration, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.logger.Error("Files", "SettingsNotSaved", "settings.json could not be saved, so the change was lost.", ex);
            throw;
        }

        this.appState.UpdateConfiguration(configuration, validation);
        return ConfigurationSaveResult.Success;
    }
}

/// <summary>
/// The outcome of <see cref="ConfigurationStore.SaveAsync"/>.
/// </summary>
public sealed record ConfigurationSaveResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    /// <summary>
    /// Gets a successful, error-free result.
    /// </summary>
    public static ConfigurationSaveResult Success { get; } = new(true, []);

    /// <summary>
    /// Builds a failed result carrying the validation error messages.
    /// </summary>
    public static ConfigurationSaveResult Invalid(IReadOnlyList<string> errors) => new(false, errors);
}
