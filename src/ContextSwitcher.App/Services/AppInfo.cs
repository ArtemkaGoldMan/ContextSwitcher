using System.Reflection;
using ContextSwitcher.Core.Updates;

namespace ContextSwitcher.App.Services;

/// <summary>This build of the app, as its assembly and the build it was made with say.</summary>
public sealed class AppInfo : IAppInfo
{
    /// <inheritdoc />
    public Version Version { get; } =
        ReleaseVersion.TryParse(typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, out Version version)
            ? version
            : new Version(0, 0, 0);

    /// <inheritdoc />
    public bool IsDevelopment =>
#if DEBUG
        true;
#else
        false;
#endif

    /// <inheritdoc />
    public string DisplayName => this.IsDevelopment ? "Context Switcher DEV" : "Context Switcher";
}
