using System.Text.Json.Serialization;

namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// How an app a profile names by name is found on Windows, where the name in the Start menu is
/// neither the process (<c>Code.exe</c>) nor how a Store app is started. Written when the app is
/// picked on Windows; the Mac goes by the name alone and ignores it.
/// </summary>
public sealed record AppIdentity
{
    /// <summary>The app's AppUserModelID, which starts it: <c>com.squirrel.slack.slack</c>.</summary>
    [JsonPropertyName("windows_app_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WindowsAppId { get; init; }

    /// <summary>The app's executable, which tells it running: <c>slack.exe</c>.</summary>
    [JsonPropertyName("windows_exe")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WindowsExe { get; init; }
}
