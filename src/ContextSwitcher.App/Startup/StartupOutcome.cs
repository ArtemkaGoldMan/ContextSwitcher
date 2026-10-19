namespace ContextSwitcher.App.Startup;

/// <summary>
/// What start-up found that the command line reports: whether the settings file could not be read and
/// was moved aside, so the app runs on a fresh default rather than the person's own.
/// </summary>
public sealed record StartupOutcome(bool ConfigurationWasQuarantined);
