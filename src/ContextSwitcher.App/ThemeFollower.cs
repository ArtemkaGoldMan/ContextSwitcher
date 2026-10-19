using Avalonia;
using Avalonia.Styling;
using ContextSwitcher.App.Services;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App;

/// <summary>
/// The app's theme follows the setting from the first window on, and changes the moment the setting
/// does: Settings saves the choice, the save publishes the configuration, and this applies it. The
/// theme is a UI-thread property; a configuration published from any other thread is applied there
/// rather than throwing.
/// </summary>
public sealed class ThemeFollower : IDisposable
{
    private readonly IAppState appState;
    private readonly Application application;

    public ThemeFollower(IAppState appState, Application application)
    {
        ArgumentNullException.ThrowIfNull(appState);
        ArgumentNullException.ThrowIfNull(application);

        this.appState = appState;
        this.application = application;

        this.Apply();
        this.appState.ConfigurationChanged += this.OnConfigurationChanged;
    }

    /// <summary>
    /// "Match macOS" is Avalonia's default variant, which tracks the system's appearance live; light
    /// and dark pin it. The menu bar icon is a template image, so macOS draws that either way.
    /// </summary>
    public static ThemeVariant ThemeFor(AppearanceMode appearance) => appearance switch
    {
        AppearanceMode.Light => ThemeVariant.Light,
        AppearanceMode.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default
    };

    public void Dispose() => this.appState.ConfigurationChanged -= this.OnConfigurationChanged;

    private void OnConfigurationChanged(object? sender, EventArgs e)
    {
        if (this.application.CheckAccess())
        {
            this.Apply();
        }
        else
        {
            this.application.Dispatcher.Post(this.Apply);
        }
    }

    private void Apply() => this.application.RequestedThemeVariant = ThemeFor(this.appState.Configuration.Appearance);
}
