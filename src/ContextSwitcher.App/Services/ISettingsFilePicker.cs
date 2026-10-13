namespace ContextSwitcher.App.Services;

/// <summary>The Save and Open dialogs for a settings file, so the page's logic can be tested without them.</summary>
public interface ISettingsFilePicker
{
    /// <summary>Asks where to save; null when the person cancels.</summary>
    Task<PickedFile?> PickSaveAsync(string suggestedName);

    /// <summary>Asks which file to open; null when the person cancels.</summary>
    Task<PickedFile?> PickOpenAsync();
}

/// <summary>A file the person picked: its name, to say back to them, and a way into it.</summary>
public sealed record PickedFile(string Name, Func<Task<Stream>> OpenAsync);
