using ContextSwitcher.Core.Applications;
using ContextSwitcher.Infrastructure.Applications;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Applications;

public sealed class InstalledAppsServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cs-apps-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(this.root, recursive: true);

    /// <summary>
    /// Plenty of installers put their app in a folder of its own. An app the picker cannot find is
    /// one the user has to type, so the scan looks one folder down - but not inside bundles, and not
    /// further, where it would be wading through every installer's support files.
    /// </summary>
    [Fact]
    public async Task AppsInAFolderOfTheirOwnAreFound()
    {
        string applications = Path.Combine(this.root, "Applications");
        Bundle(applications, "Calculator.app");
        Bundle(applications, "Adobe Photoshop 2025", "Adobe Photoshop 2025.app");
        Bundle(applications, "Setapp", "CleanShot X.app");
        Bundle(applications, "Calculator.app", "Contents", "Helper.app");
        Bundle(applications, "Tools", "More", "Too Deep.app");

        InstalledAppsService service = new(new FakeProcessRunner(), new ConfigPaths(Path.Combine(this.root, "config")), [applications]);

        IReadOnlyList<InstalledApp> apps = await service.GetInstalledAppsAsync(CancellationToken.None);

        Assert.Equal(["Adobe Photoshop 2025", "Calculator", "CleanShot X"], apps.Select(app => app.Name));
    }

    private static void Bundle(params string[] parts) => Directory.CreateDirectory(Path.Combine(parts));
}
