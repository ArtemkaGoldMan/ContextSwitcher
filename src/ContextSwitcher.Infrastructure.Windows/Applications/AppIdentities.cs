using ContextSwitcher.Core.Abstractions;
using ConfiguredApp = ContextSwitcher.Core.Configuration.AppIdentity;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// Finds out what an app a profile names is, once it runs. A profile names an app as the Start menu
/// does; Windows names a process by its executable, or by its package for a Store app. The <c>apps</c>
/// map of the settings is asked first - what was recorded when the app was picked, or what a person wrote
/// there to say otherwise: its <c>windows_exe</c>, a full path or a file name, or its <c>windows_app_id</c>.
/// Without an entry it is the Start menu's, which gives an app in three ways, and each says:
/// <list type="bullet">
/// <item>an AppUserModelID with a <c>!</c> is a Store app's, and what is before the <c>!</c> is its
/// package family;</item>
/// <item>a path is the executable itself - its first part may be a known folder's GUID, which is
/// Program Files or the Windows folder, and is written out here;</item>
/// <item>any other id, such as <c>Microsoft.VisualStudioCode</c>, says nothing of the executable, and
/// the target of the Start menu's shortcut of that name does.</item>
/// </list>
/// </summary>
public sealed class AppIdentities
{
    private readonly StartApps startApps;
    private readonly IKnownFolders knownFolders;
    private readonly IAppsMap appsMap;

    public AppIdentities(StartApps startApps, IKnownFolders knownFolders, IAppsMap appsMap)
    {
        ArgumentNullException.ThrowIfNull(startApps);
        ArgumentNullException.ThrowIfNull(knownFolders);
        ArgumentNullException.ThrowIfNull(appsMap);

        this.startApps = startApps;
        this.knownFolders = knownFolders;
        this.appsMap = appsMap;
    }

    /// <summary>
    /// What <paramref name="app"/> is, listing the Start menu first if it hasn't been; null when the
    /// Start menu has no app of the name, or says nothing of how to tell its processes.
    /// </summary>
    public async Task<AppIdentity?> FindAsync(string app, CancellationToken cancellationToken)
    {
        await this.startApps.GetAsync(cancellationToken).ConfigureAwait(false);
        return this.Find(app);
    }

    /// <summary>
    /// What <paramref name="app"/> is by the last listing of the Start menu; null as for
    /// <see cref="FindAsync"/>, and before anything has listed it.
    /// </summary>
    public AppIdentity? Find(string app)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(app);

        if (this.appsMap.Of(app.Trim()) is { } entry && this.FromEntry(entry) is { } configured)
        {
            return configured;
        }

        StartApp? found = this.startApps.Known.FirstOrDefault(
            candidate => string.Equals(candidate.Name, app.Trim(), StringComparison.OrdinalIgnoreCase));
        return found is null ? null : this.IdentityOf(found);
    }

    /// <summary>What <paramref name="app"/> is, from what the Start menu says of it.</summary>
    public AppIdentity? IdentityOf(StartApp app)
    {
        ArgumentNullException.ThrowIfNull(app);

        int bang = app.AppId.IndexOf('!', StringComparison.Ordinal);
        if (bang > 0 && !StartAppsFilter.IsPath(app.AppId))
        {
            return new AppIdentity(ExecutablePath: null, app.AppId[..bang]);
        }

        string? executable = StartAppsFilter.IsPath(app.AppId) ? this.WriteOut(app.AppId) : app.Target;
        return IsExecutable(executable) ? new AppIdentity(executable, PackageFamilyName: null) : null;
    }

    /// <summary>
    /// What an entry of the map says: the executable if it names one - a full path, or a file name that
    /// matches by name wherever the program is - else the app its AppUserModelID opens, found in the Start
    /// menu, or a Store app's package. Null when it says nothing of use, and the Start menu is asked.
    /// </summary>
    private AppIdentity? FromEntry(ConfiguredApp entry)
    {
        string? executable = entry.WindowsExe?.Trim();
        if (!string.IsNullOrEmpty(executable)
            && string.Equals(Path.GetExtension(executable), ".exe", StringComparison.OrdinalIgnoreCase)
            && (Path.IsPathRooted(executable) || Path.GetFileName(executable) == executable))
        {
            return new AppIdentity(executable, PackageFamilyName: null);
        }

        string? id = entry.WindowsAppId?.Trim();
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        StartApp? listed = this.startApps.Known.FirstOrDefault(candidate => string.Equals(candidate.AppId, id, StringComparison.OrdinalIgnoreCase));
        return listed is not null ? this.IdentityOf(listed) : this.IdentityOf(new StartApp(id, id));
    }

    private static bool IsExecutable(string? path) =>
        path is not null && Path.IsPathRooted(path) && string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase);

    /// <summary>Writes out the known folder an id starts with: <c>{7C5A40EF-…}\Steam\Steam.exe</c> is under Program Files (x86).</summary>
    private string? WriteOut(string path)
    {
        if (!path.StartsWith('{'))
        {
            return path;
        }

        int end = path.IndexOf("}\\", StringComparison.Ordinal);
        if (end < 0 || !Guid.TryParse(path.AsSpan(0, end + 1), out Guid folder))
        {
            return null;
        }

        return this.knownFolders.PathOf(folder) is { } root ? Path.Combine(root, path[(end + 2)..]) : null;
    }
}
