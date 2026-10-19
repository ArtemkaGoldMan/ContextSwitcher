using ContextSwitcher.App.Services;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>A build of the app at the version given.</summary>
public sealed class FakeAppInfo(Version version, bool development = false) : IAppInfo
{
    public Version Version { get; } = version;

    public bool IsDevelopment { get; } = development;

    public string DisplayName => this.IsDevelopment ? "Context Switcher DEV" : "Context Switcher";
}
