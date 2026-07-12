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

        if (configuration.SchemaVersion != 1)
        {
            errors.Add(new ConfigurationValidationError("schemaVersion", "Unsupported configuration schema version."));
        }

        if (configuration.Contexts.Count == 0)
        {
            errors.Add(new ConfigurationValidationError("contexts", "At least one context is required."));
        }

        ValidateContexts(configuration, errors);
        ValidateHotkeys(configuration, errors);

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
                errors.Add(new ConfigurationValidationError($"{path}.id", "Context id must be lowercase and URL-safe."));
            }
            else if (!seenContextIds.Add(context.Id))
            {
                errors.Add(new ConfigurationValidationError($"{path}.id", "Context id must be unique."));
            }

            if (string.IsNullOrWhiteSpace(context.DisplayName))
            {
                errors.Add(new ConfigurationValidationError($"{path}.displayName", "Context display name is required."));
            }

            if (!HexColorPattern.IsMatch(context.AccentColor))
            {
                errors.Add(new ConfigurationValidationError($"{path}.accentColor", "Accent color must be a 6-digit hex color."));
            }

            ValidateBrowserManagement(context.BrowserManagement, $"{path}.browser_management", errors);
            ValidateQuickLinks(context.QuickLinks, $"{path}.quickLinks", errors);
        }

        if (!string.IsNullOrWhiteSpace(configuration.ActiveContextId) &&
            !seenContextIds.Contains(configuration.ActiveContextId))
        {
            errors.Add(new ConfigurationValidationError("activeContextId", "Active context must reference an existing context."));
        }
    }

    private static void ValidateHotkeys(AppConfiguration configuration, List<ConfigurationValidationError> errors)
    {
        HashSet<string> contextIds = configuration.Contexts
            .Select(context => context.Id)
            .ToHashSet(StringComparer.Ordinal);

        for (int i = 0; i < configuration.Hotkeys.Count; i++)
        {
            HotkeyConfig hotkey = configuration.Hotkeys[i];

            if (!contextIds.Contains(hotkey.ContextId))
            {
                errors.Add(new ConfigurationValidationError($"hotkeys[{i}].contextId", "Hotkey context must reference an existing context."));
            }
        }
    }

    private static void ValidateBrowserManagement(
        BrowserManagementConfig browserManagement,
        string path,
        List<ConfigurationValidationError> errors)
    {
        ValidateUrls(browserManagement.Urls, $"{path}.urls", errors);

        switch (browserManagement.Mode)
        {
            case BrowserManagementMode.None:
                break;
            case BrowserManagementMode.Urls:
                if (browserManagement.Urls.Count == 0)
                {
                    errors.Add(new ConfigurationValidationError($"{path}.urls", "URL mode requires at least one URL."));
                }

                break;
            case BrowserManagementMode.Groups:
                ValidateTabGroups(browserManagement.TabGroups, $"{path}.tab_groups", errors);
                break;
            case BrowserManagementMode.Profiles:
                ValidateProfiles(browserManagement.Profiles, $"{path}.profiles", errors);
                break;
            default:
                errors.Add(new ConfigurationValidationError($"{path}.mode", "Browser management mode is invalid."));
                break;
        }

        if (browserManagement.Mode is BrowserManagementMode.Urls or BrowserManagementMode.Groups &&
            !IsSupportedInteractiveBrowser(browserManagement.Browser))
        {
            errors.Add(new ConfigurationValidationError($"{path}.browser", "Browser must be Default, Chrome, Brave, or Safari."));
        }
    }

    private static void ValidateTabGroups(
        IReadOnlyList<string> tabGroups,
        string path,
        List<ConfigurationValidationError> errors)
    {
        if (tabGroups.Count == 0)
        {
            errors.Add(new ConfigurationValidationError(path, "Tab groups mode requires at least one tab group name."));
            return;
        }

        for (int i = 0; i < tabGroups.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(tabGroups[i]))
            {
                errors.Add(new ConfigurationValidationError($"{path}[{i}]", "Tab group name cannot be empty."));
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
            errors.Add(new ConfigurationValidationError(path, "Profiles mode requires at least one browser profile."));
            return;
        }

        for (int i = 0; i < profiles.Count; i++)
        {
            BrowserProfileConfig profile = profiles[i];
            string profilePath = $"{path}[{i}]";

            if (!IsSupportedProfileBrowser(profile.Browser))
            {
                errors.Add(new ConfigurationValidationError($"{profilePath}.browser", "Profile browser must be Chrome or Brave."));
            }

            if (string.IsNullOrWhiteSpace(profile.ProfileDirectory))
            {
                errors.Add(new ConfigurationValidationError($"{profilePath}.profile_directory", "Profile directory is required."));
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
                errors.Add(new ConfigurationValidationError($"{path}[{i}].url", "Quick link URL must use http or https."));
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
                errors.Add(new ConfigurationValidationError($"{path}[{i}]", "URL must use http or https."));
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
