using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Infrastructure.Files;
using Microsoft.Extensions.DependencyInjection;

// The same namespace as the macOS project's PlatformServices, so the app calls whichever its build
// brings in without naming a platform.
namespace ContextSwitcher.Infrastructure.Platform;

/// <summary>
/// What the app has on Windows: so far, where everything is kept and how a file is replaced. The
/// Windows versions of what the shared code asks of the system come with stage 1 of
/// <c>docs/windows-plan.md</c>; until then the app's Windows build is compiled, not run.
/// </summary>
public static class PlatformServices
{
    /// <summary>Registers Windows' services and data.</summary>
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(SettingsRoot.AppData);
        services.AddSingleton<IFileReplacer>(FileReplacer.RetryingWhileHeldOpen);
    }
}
