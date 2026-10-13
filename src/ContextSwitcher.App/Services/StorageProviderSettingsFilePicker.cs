using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ContextSwitcher.App.Resources;

namespace ContextSwitcher.App.Services;

/// <summary>The settings file dialogs, as macOS's own Save and Open panels through Avalonia.</summary>
public sealed class StorageProviderSettingsFilePicker(TopLevel topLevel) : ISettingsFilePicker
{
    private static readonly FilePickerFileType Json = new("JSON") { Patterns = ["*.json"], AppleUniformTypeIdentifiers = ["public.json"] };

    /// <inheritdoc />
    public async Task<PickedFile?> PickSaveAsync(string suggestedName)
    {
        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.Settings_Export,
            SuggestedFileName = suggestedName,
            DefaultExtension = "json",
            FileTypeChoices = [Json],
            ShowOverwritePrompt = true
        }).ConfigureAwait(true);

        return file is null ? null : new PickedFile(file.Name, file.OpenWriteAsync);
    }

    /// <inheritdoc />
    public async Task<PickedFile?> PickOpenAsync()
    {
        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.Settings_Import,
            AllowMultiple = false,
            FileTypeFilter = [Json]
        }).ConfigureAwait(true);

        return files.Count == 0 ? null : new PickedFile(files[0].Name, files[0].OpenReadAsync);
    }
}
