using ContextSwitcher.App.Services;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Logging;

namespace ContextSwitcher.App.Startup;

/// <summary>
/// Writes into the <c>apps</c> map of the settings how this system finds each app the profiles name,
/// as the app starts and whenever the settings change - an app picked, an answer to "what about
/// these?", a profile imported. Only entries that are missing are added: one a person edited is left
/// as it is, and an app the system doesn't know, or a system that goes by the name alone, adds none
/// and writes nothing. What it records is what the platform's <see cref="IAppIdentityCatalog"/> says.
/// </summary>
public sealed class AppsMapRecorder(IAppState appState, ConfigurationStore store, IAppIdentityCatalog catalog, ILogger logger) : IHousekeeping
{
    private bool recording;
    private bool changedWhileRecording;

    /// <inheritdoc />
    public void Start()
    {
        appState.ConfigurationChanged += (_, _) => _ = this.RecordAsync();
        _ = this.RecordAsync();
    }

    /// <summary>Adds the missing entries, if there are any to add. Returns when it has, or has found there are none.</summary>
    public async Task RecordAsync()
    {
        // The settings are saved on this thread and their change is raised inline, so a second call
        // can only come while this one waits. It is not run beside this one - both would add the same
        // entries - but remembered, so that what changed meanwhile is looked at when this one is done.
        if (this.recording)
        {
            this.changedWhileRecording = true;
            return;
        }

        this.recording = true;
        try
        {
            do
            {
                this.changedWhileRecording = false;
                await this.RecordOnceAsync().ConfigureAwait(true);
            }
            while (this.changedWhileRecording);
        }
        finally
        {
            this.recording = false;
        }
    }

    private async Task RecordOnceAsync()
    {
        if (!appState.ConfigurationValidation.IsValid)
        {
            return;
        }

        // ConfigureAwait(true): the save below raises ConfigurationChanged for view models that
        // build Avalonia objects, which belong to the UI thread.
        Dictionary<string, AppIdentity> found = [];
        foreach (string name in AppsMap.Unrecorded(appState.Configuration))
        {
            AppIdentity? identity = await catalog.IdentifyAsync(name, CancellationToken.None).ConfigureAwait(true);
            if (identity is not null)
            {
                found[name] = identity;
            }
        }

        // Looked at again: the settings may have been saved while the catalog was being asked.
        if (AppsMap.With(appState.Configuration, found) is not { } updated)
        {
            return;
        }

        try
        {
            ConfigurationSaveResult saved = await store.SaveAsync(updated, CancellationToken.None).ConfigureAwait(true);
            if (!saved.Succeeded)
            {
                logger.Warn("Configuration", "AppsMapNotSaved", "How the profiles' apps are found couldn't be saved to the settings.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The store has said what couldn't be written; this runs on its own, with nobody to tell,
            // and the entries are tried again at the next change.
            logger.Warn("Configuration", "AppsMapNotSaved", "How the profiles' apps are found couldn't be saved to the settings.", ex);
        }
    }
}
