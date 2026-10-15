using ContextSwitcher.Core.Messages;

namespace ContextSwitcher.Core.Contexts;

/// <summary>
/// The lines a switch records about apps it asked to quit that were still running when it finished -
/// one app each - and how they change as those apps go, stay, or are ended by force.
/// </summary>
public static class ClosingApps
{
    /// <summary>The lines about an app the switch saw still running.</summary>
    private static readonly HashSet<string> StillRunningKeys = new(StringComparer.Ordinal)
    {
        SwitchMessages.CouldNotClose.Key,
        SwitchMessages.StillClosing.Key
    };

    /// <summary>Every line about one such app, from the switch's own to what came of it.</summary>
    private static readonly HashSet<string> AboutAnApp = new(StringComparer.Ordinal)
    {
        SwitchMessages.CouldNotClose.Key,
        SwitchMessages.StillClosing.Key,
        SwitchMessages.StayedInBackground.Key,
        SwitchMessages.CouldNotForceQuit.Key
    };

    /// <summary>The apps <paramref name="errors"/> say were still running when the switch finished.</summary>
    public static IReadOnlyList<string> StillRunning(IReadOnlyList<StateError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return errors
            .Where(error => error.MessageKey is { } key && StillRunningKeys.Contains(key) && error.MessageArguments.Count == 1)
            .Select(error => error.MessageArguments[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Whether the switch said <paramref name="app"/> was still closing, with its windows closed.</summary>
    public static bool WasClosing(IReadOnlyList<StateError> errors, string app)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return errors.Any(error => error.MessageKey == SwitchMessages.StillClosing.Key && IsAbout(error, app));
    }

    /// <summary>The app a line offers to end by force - one that stayed on in the background - or null.</summary>
    public static string? ForceQuittable(StateError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.MessageKey == SwitchMessages.StayedInBackground.Key && error.MessageArguments.Count == 1
            ? error.MessageArguments[0]
            : null;
    }

    /// <summary><paramref name="app"/> has quit: its line goes.</summary>
    public static IReadOnlyList<StateError> Quit(IReadOnlyList<StateError> errors, string app)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return errors.Where(error => !IsAbout(error, app)).ToList();
    }

    /// <summary>
    /// <paramref name="app"/> is still running when the watch is over: with a window it may
    /// be asking to save; with none it stayed on in the background.
    /// </summary>
    public static IReadOnlyList<StateError> Stayed(IReadOnlyList<StateError> errors, string app, bool hasWindow) =>
        Replace(errors, app, hasWindow ? SwitchMessages.CouldNotClose.With(app) : SwitchMessages.StayedInBackground.With(app));

    /// <summary><paramref name="app"/> was still running after the person had it ended by force.</summary>
    public static IReadOnlyList<StateError> ForceQuitFailed(IReadOnlyList<StateError> errors, string app) =>
        Replace(errors, app, SwitchMessages.CouldNotForceQuit.With(app));

    private static IReadOnlyList<StateError> Replace(IReadOnlyList<StateError> errors, string app, UserMessage message)
    {
        ArgumentNullException.ThrowIfNull(errors);

        // A line that already says it is kept as it is, so nothing is written for no change.
        return errors
            .Select(error => IsAbout(error, app) && error.MessageKey != message.Key
                ? error with { Message = message.English, MessageKey = message.Key, MessageArguments = message.Arguments }
                : error)
            .ToList();
    }

    private static bool IsAbout(StateError error, string app) =>
        error.MessageKey is { } key
        && AboutAnApp.Contains(key)
        && error.MessageArguments.Count == 1
        && string.Equals(error.MessageArguments[0], app, StringComparison.OrdinalIgnoreCase);
}
