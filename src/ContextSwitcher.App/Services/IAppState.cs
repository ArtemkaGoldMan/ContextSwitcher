using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;

namespace ContextSwitcher.App.Services;

/// <summary>
/// The settings the app runs with and where it stands - the active profile, the last switch - and the
/// news that either changed. One for the whole run, given to whatever reads them, so every window shows
/// the same and a save or a switch in one is seen at once in the others.
/// </summary>
public interface IAppState
{
    /// <summary>The settings loaded at start-up, or saved since.</summary>
    AppConfiguration Configuration { get; }

    /// <summary>What is wrong with <see cref="Configuration"/>, if anything.</summary>
    ConfigurationValidationResult ConfigurationValidation { get; }

    /// <summary>The active profile and how the last switch went, as <c>state.json</c> has them.</summary>
    CurrentContextState State { get; }

    /// <summary>Raised after <see cref="UpdateConfiguration"/>, so long-lived view models refresh without a restart.</summary>
    event EventHandler? ConfigurationChanged;

    /// <summary>Raised after <see cref="UpdateState"/>, so every window reflects a switch made in another, or by another process.</summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Replaces the settings and their validation, then raises <see cref="ConfigurationChanged"/>. The
    /// caller has already written and checked them - see <see cref="ConfigurationStore"/>.
    /// </summary>
    void UpdateConfiguration(AppConfiguration configuration, ConfigurationValidationResult validation);

    /// <summary>Replaces the state after a switch, or after another process changed it, then raises <see cref="StateChanged"/>.</summary>
    void UpdateState(CurrentContextState state);
}
