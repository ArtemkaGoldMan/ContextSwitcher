using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Messages;

namespace ContextSwitcher.Core.Platform;

/// <summary>
/// What this computer can do that another may not: Focus, music, Docker, a Dock icon, web pages and
/// which browsers - and so profiles and closing tabs by id - and whether it asks before one app
/// drives another. Profile Setup and Settings offer only what it has, the plan
/// has no step for what it lacks, and importing settings says what of them can't be used here. Data,
/// which each platform's registrations choose.
/// </summary>
public sealed record PlatformCapabilities
{
    /// <summary>Focus modes, turned on and off through Shortcuts.</summary>
    public required bool Focus { get; init; }

    /// <summary>Playing an Apple Music playlist.</summary>
    public required bool AppleMusic { get; init; }

    /// <summary>Playing a Spotify playlist.</summary>
    public required bool Spotify { get; init; }

    /// <summary>Starting and stopping Docker containers.</summary>
    public required bool Docker { get; init; }

    /// <summary>An icon among the running apps - the Dock - besides the one in the menu bar.</summary>
    public required bool DockIcon { get; init; }

    /// <summary>
    /// Whether the system asks the person before the app drives another app - macOS's Automation
    /// permission - so Settings shows whether it was given.
    /// </summary>
    public required bool AutomationPermission { get; init; }

    /// <summary>
    /// Opening a profile's pages in a browser, and closing them. Without it, a profile has no browser
    /// to set and no step opens or closes a page, whichever browsers there are.
    /// </summary>
    public required bool BrowserPages { get; init; }

    /// <summary>The browsers a profile can choose besides the default one, in the order they're offered.</summary>
    public required IReadOnlyList<BrowserKind> Browsers { get; init; }

    /// <summary>
    /// The browsers a Focus filter switches to one of their tab groups or profiles as the Focus turns
    /// on - which is how a profile gets them, since no other app can open them.
    /// </summary>
    public required IReadOnlyList<BrowserKind> FocusFilterBrowsers { get; init; }

    /// <summary>Whether <paramref name="player"/> can be played here; none at all always can.</summary>
    public bool Plays(MediaPlayerKind player) => player switch
    {
        MediaPlayerKind.AppleMusic => this.AppleMusic,
        MediaPlayerKind.Spotify => this.Spotify,
        _ => true
    };

    /// <summary>Whether any player can be played here, so a profile has music to choose.</summary>
    public bool PlaysMusic => this.AppleMusic || this.Spotify;

    /// <summary>Whether a profile can use <paramref name="browser"/> here; the default browser always can.</summary>
    public bool HasBrowser(BrowserKind browser) => browser == BrowserKind.Default || this.Browsers.Contains(browser);

    /// <summary>Whether <paramref name="browser"/> is here and another app can open its profiles.</summary>
    public bool OpensBrowserProfiles(BrowserKind browser) => this.Browsers.Contains(browser) && KnownBrowsers.HasProfiles(browser);

    /// <summary>
    /// Whether <paramref name="browser"/> is here and its tabs can be closed by id, so the tabs a
    /// profile opened are remembered and closed wherever they have gone.
    /// </summary>
    public bool ClosesTabsById(BrowserKind browser) => this.Browsers.Contains(browser) && KnownBrowsers.KeepsTabIds(browser);

    /// <summary>Whether a Focus filter can switch <paramref name="browser"/> to a tab group or profile here.</summary>
    public bool FocusFiltersBrowser(BrowserKind browser) => this.Focus && this.FocusFilterBrowsers.Contains(browser);

    /// <summary>The browsers here whose profiles another app can open.</summary>
    public IReadOnlyList<BrowserKind> BrowsersWithProfiles => this.Browsers.Where(KnownBrowsers.HasProfiles).ToList();

    /// <summary>
    /// What of <paramref name="configuration"/> this computer can't do, a line each - for settings
    /// that came from another computer. It stays in the settings, and does nothing here.
    /// </summary>
    public IReadOnlyList<UserMessage> Unsupported(AppConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        List<UserMessage> unsupported = [];
        foreach (ContextDefinition context in configuration.Contexts)
        {
            if (!this.Focus && context.Focus.Enabled && !string.IsNullOrWhiteSpace(context.Focus.ModeName))
            {
                unsupported.Add(ConfigurationMessages.FocusNotHere.With(context.DisplayName));
            }

            if (!this.Plays(context.Media.Player))
            {
                MessageTemplate notHere = context.Media.Player == MediaPlayerKind.Spotify
                    ? ConfigurationMessages.SpotifyNotHere
                    : ConfigurationMessages.AppleMusicNotHere;
                unsupported.Add(notHere.With(context.DisplayName));
            }

            if (!this.Docker && (context.Docker.Start.Count > 0 || context.Docker.Stop.Count > 0))
            {
                unsupported.Add(ConfigurationMessages.DockerNotHere.With(context.DisplayName));
            }

            BrowserManagementConfig browser = context.BrowserManagement;
            if (!this.BrowserPages && browser.Mode != BrowserManagementMode.None)
            {
                unsupported.Add(ConfigurationMessages.PagesNotHere.With(context.DisplayName));
                continue;
            }

            if (browser.Mode == BrowserManagementMode.Urls && !this.HasBrowser(browser.Browser))
            {
                unsupported.Add(ConfigurationMessages.BrowserNotHere.With(context.DisplayName, KnownBrowsers.Name(browser.Browser)));
            }

            if (browser.Mode == BrowserManagementMode.Profiles)
            {
                foreach (BrowserKind withProfiles in browser.Profiles.Select(profile => profile.Browser).Distinct().Where(kind => !this.OpensBrowserProfiles(kind)))
                {
                    unsupported.Add(ConfigurationMessages.BrowserProfilesNotHere.With(context.DisplayName, KnownBrowsers.Name(withProfiles)));
                }
            }
        }

        return unsupported;
    }
}
