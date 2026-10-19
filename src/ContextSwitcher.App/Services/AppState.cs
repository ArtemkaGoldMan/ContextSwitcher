using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;

namespace ContextSwitcher.App.Services;

/// <summary>
/// The app's settings and state, held for the run. Until start-up loads them it holds the defaults: no
/// profiles, nothing wrong, no switch yet.
/// </summary>
public sealed class AppState : IAppState
{
    /// <inheritdoc />
    public AppConfiguration Configuration { get; private set; } = new();

    /// <inheritdoc />
    public ConfigurationValidationResult ConfigurationValidation { get; private set; } = new([]);

    /// <inheritdoc />
    public CurrentContextState State { get; private set; } = new();

    /// <inheritdoc />
    public event EventHandler? ConfigurationChanged;

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public void UpdateConfiguration(AppConfiguration configuration, ConfigurationValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(validation);

        this.Configuration = configuration;
        this.ConfigurationValidation = validation;
        this.ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void UpdateState(CurrentContextState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        this.State = state;
        this.StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
