using ContextSwitcher.Core.Messages;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ContextSwitcher.Core.Configuration.Validation;

/// <summary>
/// Validates ContextSwitcher configuration objects against the supported schema and business rules.
/// </summary>
public sealed class ConfigurationValidator
{
    private static readonly Regex ContextIdPattern = new("^[a-z0-9][a-z0-9-]*$", RegexOptions.Compiled);
    private static readonly Regex HexColorPattern = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    /// <summary>
    /// Validates the supplied configuration and returns a structured result with any issues.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <returns>A validation result describing any errors found.</returns>
    public ConfigurationValidationResult Validate(AppConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        List<ConfigurationValidationError> errors = [];

        // Older schemas are accepted and read as-is: every version so far is field-compatible, and
        // rejecting them outright would have left the first schema change with no upgrade path at
        // all - an existing config would simply stop being valid. Newer ones are refused, because a
        // file written by a later build may hold fields this one would silently drop on the next
        // save. Add a migration step here when a version stops being readable as-is.
        if (configuration.SchemaVersion > AppConfiguration.CurrentSchemaVersion)
        {
            errors.Add(ConfigurationValidationError.Of(
                "schemaVersion",
                ConfigurationMessages.SchemaTooNew.With(
                    configuration.SchemaVersion.ToString(CultureInfo.InvariantCulture),
                    AppConfiguration.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture))));
        }
        else if (configuration.SchemaVersion < 1)
        {
            errors.Add(ConfigurationValidationError.Of("schemaVersion", ConfigurationMessages.SchemaUnsupported.With()));
        }

        if (configuration.Contexts.Count == 0)
        {
            errors.Add(ConfigurationValidationError.Of("contexts", ConfigurationMessages.NoContexts.With()));
        }

        ValidateContexts(configuration, errors);

        return new ConfigurationValidationResult(errors);
    }

    private static void ValidateContexts(AppConfiguration configuration, List<ConfigurationValidationError> errors)
    {
        HashSet<string> seenContextIds = new(StringComparer.Ordinal);

        for (int i = 0; i < configuration.Contexts.Count; i++)
        {
            ContextDefinition context = configuration.Contexts[i];
            string path = $"contexts[{i}]";

            if (string.IsNullOrWhiteSpace(context.Id) || !ContextIdPattern.IsMatch(context.Id))
            {
                errors.Add(ConfigurationValidationError.Of($"{path}.id", ConfigurationMessages.IdNotUrlSafe.With()));
            }
            else if (!seenContextIds.Add(context.Id))
            {
                errors.Add(ConfigurationValidationError.Of($"{path}.id", ConfigurationMessages.IdNotUnique.With()));
            }

            if (string.IsNullOrWhiteSpace(context.DisplayName))
            {
                errors.Add(ConfigurationValidationError.Of($"{path}.displayName", ConfigurationMessages.DisplayNameRequired.With()));
            }

            if (!HexColorPattern.IsMatch(context.AccentColor))
            {
                errors.Add(ConfigurationValidationError.Of($"{path}.accentColor", ConfigurationMessages.AccentColorInvalid.With()));
            }

            ValidateBrowserManagement(context.BrowserManagement, $"{path}.browser_management", errors);
            ValidateQuickLinks(context.QuickLinks, $"{path}.quickLinks", errors);
        }

        if (!string.IsNullOrWhiteSpace(configuration.ActiveContextId) &&
            !seenContextIds.Contains(configuration.ActiveContextId))
        {
            errors.Add(ConfigurationValidationError.Of("activeContextId", ConfigurationMessages.ActiveContextMissing.With()));
        }
    }

    private static void ValidateBrowserManagement(
        BrowserManagementConfig browserManagement,
        string path,
        List<ConfigurationValidationError> errors)
    {
        ValidateUrls(browserManagement.Urls, $"{path}.urls", errors);
        ValidateUrls(browserManagement.CloseUrls, $"{path}.close_urls", errors);

        switch (browserManagement.Mode)
        {
            case BrowserManagementMode.None:
                break;
            case BrowserManagementMode.Urls:
                if (browserManagement.Urls.Count == 0 && browserManagement.CloseUrls.Count == 0)
                {
                    errors.Add(ConfigurationValidationError.Of($"{path}.urls", ConfigurationMessages.UrlModeNeedsUrl.With()));
                }

                break;
            case BrowserManagementMode.Groups:
                ValidateTabGroups(browserManagement.TabGroups, $"{path}.tab_groups", errors);
                break;
            case BrowserManagementMode.Profiles:
                ValidateProfiles(browserManagement.Profiles, $"{path}.profiles", errors);
                break;
            default:
                errors.Add(ConfigurationValidationError.Of($"{path}.mode", ConfigurationMessages.BrowserModeInvalid.With()));
                break;
        }

        if (browserManagement.Mode is BrowserManagementMode.Urls or BrowserManagementMode.Groups &&
            !IsSupportedInteractiveBrowser(browserManagement.Browser))
        {
            errors.Add(ConfigurationValidationError.Of($"{path}.browser", ConfigurationMessages.BrowserInvalid.With()));
        }

        if (browserManagement.Mode == BrowserManagementMode.Urls
            && browserManagement.Browser == BrowserKind.Default
            && browserManagement.CloseUrls.Count > 0)
        {
            errors.Add(ConfigurationValidationError.Of($"{path}.close_urls", ConfigurationMessages.CloseTabsNeedsBrowser.With()));
        }
    }

    private static void ValidateTabGroups(
        IReadOnlyList<string> tabGroups,
        string path,
        List<ConfigurationValidationError> errors)
    {
        if (tabGroups.Count == 0)
        {
            errors.Add(ConfigurationValidationError.Of(path, ConfigurationMessages.TabGroupsNeedName.With()));
            return;
        }

        for (int i = 0; i < tabGroups.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(tabGroups[i]))
            {
                errors.Add(ConfigurationValidationError.Of($"{path}[{i}]", ConfigurationMessages.TabGroupNameEmpty.With()));
            }
        }
    }

    private static void ValidateProfiles(
        IReadOnlyList<BrowserProfileConfig> profiles,
        string path,
        List<ConfigurationValidationError> errors)
    {
        if (profiles.Count == 0)
        {
            errors.Add(ConfigurationValidationError.Of(path, ConfigurationMessages.ProfilesNeedProfile.With()));
            return;
        }

        for (int i = 0; i < profiles.Count; i++)
        {
            BrowserProfileConfig profile = profiles[i];
            string profilePath = $"{path}[{i}]";

            if (!IsSupportedProfileBrowser(profile.Browser))
            {
                errors.Add(ConfigurationValidationError.Of($"{profilePath}.browser", ConfigurationMessages.ProfileBrowserInvalid.With()));
            }

            if (string.IsNullOrWhiteSpace(profile.ProfileDirectory))
            {
                errors.Add(ConfigurationValidationError.Of($"{profilePath}.profile_directory", ConfigurationMessages.ProfileDirectoryRequired.With()));
            }

            ValidateUrls(profile.Urls, $"{profilePath}.urls", errors);
        }
    }

    private static void ValidateQuickLinks(
        IReadOnlyList<QuickLinkConfig> quickLinks,
        string path,
        List<ConfigurationValidationError> errors)
    {
        for (int i = 0; i < quickLinks.Count; i++)
        {
            if (!IsHttpUrl(quickLinks[i].Url))
            {
                errors.Add(ConfigurationValidationError.Of($"{path}[{i}].url", ConfigurationMessages.QuickLinkUrlInvalid.With()));
            }
        }
    }

    private static void ValidateUrls(
        IReadOnlyList<string> urls,
        string path,
        List<ConfigurationValidationError> errors)
    {
        for (int i = 0; i < urls.Count; i++)
        {
            if (!IsHttpUrl(urls[i]))
            {
                errors.Add(ConfigurationValidationError.Of($"{path}[{i}]", ConfigurationMessages.UrlInvalid.With()));
            }
        }
    }

    private static bool IsHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool IsSupportedInteractiveBrowser(BrowserKind browser)
    {
        return browser is BrowserKind.Default or BrowserKind.Chrome or BrowserKind.Brave or BrowserKind.Safari;
    }

    private static bool IsSupportedProfileBrowser(BrowserKind browser)
    {
        return browser is BrowserKind.Chrome or BrowserKind.Brave;
    }
}
