using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Infrastructure.Files;

namespace ContextSwitcher.App.Services;

/// <summary>
/// The single place the Main App window's view models go through to persist edits to
/// <c>settings.json</c>: re-validates, backs up the previous file, writes atomically, and updates
/// <see cref="AppHost"/> so the Dashboard reflects the change without a restart (agent.md section
/// 11.1.2's "Profile Setup" and "Settings" pages, and section 6's backup-before-write rule).
/// </summary>
public sealed class ConfigurationStore
{
    private readonly IJsonStore jsonStore;
    private readonly ConfigPaths configPaths;
    private readonly ConfigurationValidator validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationStore"/> class.
    /// </summary>
    public ConfigurationStore(IJsonStore jsonStore, ConfigPaths configPaths, ConfigurationValidator validator)
    {
        ArgumentNullException.ThrowIfNull(jsonStore);
        ArgumentNullException.ThrowIfNull(configPaths);
        ArgumentNullException.ThrowIfNull(validator);

        this.jsonStore = jsonStore;
        this.configPaths = configPaths;
        this.validator = validator;
    }

    /// <summary>
    /// Validates <paramref name="configuration"/> and, only if valid, backs up the current
    /// <c>settings.json</c>, writes the new configuration, and publishes it via
    /// <see cref="AppHost.UpdateConfiguration"/>. Never persists an invalid configuration.
    /// </summary>
    public async Task<ConfigurationSaveResult> SaveAsync(AppConfiguration configuration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        ConfigurationValidationResult validation = this.validator.Validate(configuration);
        if (!validation.IsValid)
        {
            return ConfigurationSaveResult.Invalid(validation.Errors.Select(error => $"{error.Path}: {error.Message}").ToList());
        }

        await this.jsonStore.BackupAsync(this.configPaths.SettingsPath, this.configPaths.BackupsDirectory, cancellationToken)
            .ConfigureAwait(false);
        await this.jsonStore.WriteAsync(this.configPaths.SettingsPath, configuration, cancellationToken).ConfigureAwait(false);

        AppHost.UpdateConfiguration(configuration, validation);
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
