using System.Text;
using ContextSwitcher.App.Services;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>The Save and Open panels, answered by the test: a file in memory, or cancelled.</summary>
public sealed class FakeSettingsFilePicker : ISettingsFilePicker
{
    private MemoryStream? saved;

    /// <summary>What the Open panel hands over; null plays Cancel.</summary>
    public string? FileToOpen { get; set; }

    public string FileName { get; set; } = "settings backup.json";

    /// <summary>Plays Cancel in the Save panel.</summary>
    public bool CancelSave { get; set; }

    public string? SuggestedName { get; private set; }

    /// <summary>What an export wrote, readable after it closed the file.</summary>
    public string? SavedText => this.saved is null ? null : Encoding.UTF8.GetString(this.saved.ToArray());

    public Task<PickedFile?> PickSaveAsync(string suggestedName)
    {
        this.SuggestedName = suggestedName;
        if (this.CancelSave)
        {
            return Task.FromResult<PickedFile?>(null);
        }

        this.saved = new MemoryStream();
        return Task.FromResult<PickedFile?>(new PickedFile(this.FileName, () => Task.FromResult<Stream>(this.saved)));
    }

    public Task<PickedFile?> PickOpenAsync() =>
        Task.FromResult(this.FileToOpen is null
            ? null
            : new PickedFile(this.FileName, () => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(this.FileToOpen)))));
}
