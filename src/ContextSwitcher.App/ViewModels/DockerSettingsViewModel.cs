using System.Collections.ObjectModel;
using ContextSwitcher.App.Resources;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App.ViewModels;

/// <summary>Profile Setup's Docker section: the containers started on entering the profile, and those stopped on leaving.</summary>
public sealed class DockerSettingsViewModel : ViewModelBase
{
    private readonly ISystemCatalog catalog;

    public DockerSettingsViewModel(ISystemCatalog catalog, DockerResourceConfig source)
    {
        this.catalog = catalog;

        // Containers already in config are names Docker gave them; shown, not offered for editing.
        this.DockerStart = new ObservableCollection<EditableStringRowViewModel>(
            source.Start.Select(name => ContainerRow(name, this.RemoveDockerStart, isEditable: false)));
        this.DockerStop = new ObservableCollection<EditableStringRowViewModel>(
            source.Stop.Select(name => ContainerRow(name, this.RemoveDockerStop, isEditable: false)));
        this.DockerStartPicker = new ChoicePickerViewModel(
            this.LoadContainersAsync,
            () => this.DockerStart.Select(row => row.Value.Trim()),
            choice => this.DockerStart.Add(ContainerRow(choice.Value, this.RemoveDockerStart, isEditable: false)),
            Strings.Picker_ContainersEmpty,
            Strings.Picker_DockerNotRunning,
            "IconBox",
            Strings.Picker_SearchContainers);
        this.DockerStopPicker = new ChoicePickerViewModel(
            this.LoadContainersAsync,
            () => this.DockerStop.Select(row => row.Value.Trim()),
            choice => this.DockerStop.Add(ContainerRow(choice.Value, this.RemoveDockerStop, isEditable: false)),
            Strings.Picker_ContainersEmpty,
            Strings.Picker_DockerNotRunning,
            "IconBox",
            Strings.Picker_SearchContainers);

        this.AddDockerStartCommand = new RelayCommand(() => this.DockerStart.Add(ContainerRow(string.Empty, this.RemoveDockerStart, isEditable: true)));
        this.AddDockerStopCommand = new RelayCommand(() => this.DockerStop.Add(ContainerRow(string.Empty, this.RemoveDockerStop, isEditable: true)));
    }

    public ObservableCollection<EditableStringRowViewModel> DockerStart { get; }

    public ObservableCollection<EditableStringRowViewModel> DockerStop { get; }

    /// <summary>"+ Add container" for Start: the containers Docker has, or a note that it isn't running.</summary>
    public ChoicePickerViewModel DockerStartPicker { get; }

    /// <summary>"+ Add container" for Stop.</summary>
    public ChoicePickerViewModel DockerStopPicker { get; }

    public RelayCommand AddDockerStartCommand { get; }

    public RelayCommand AddDockerStopCommand { get; }

    /// <summary>The section as the profile keeps it: the containers named, each once trimmed.</summary>
    public DockerResourceConfig ToConfig() => new()
    {
        Start = this.DockerStart.Where(d => !string.IsNullOrWhiteSpace(d.Value)).Select(d => d.Value.Trim()).ToList(),
        Stop = this.DockerStop.Where(d => !string.IsNullOrWhiteSpace(d.Value)).Select(d => d.Value.Trim()).ToList()
    };

    private static EditableStringRowViewModel ContainerRow(string name, Action<EditableStringRowViewModel> remove, bool isEditable) =>
        new(name, remove, isEditable, "IconBox", Strings.ProfileSetup_ContainerName);

    private void RemoveDockerStart(EditableStringRowViewModel row)
    {
        this.DockerStart.Remove(row);
        this.DockerStartPicker.Refresh();
    }

    private void RemoveDockerStop(EditableStringRowViewModel row)
    {
        this.DockerStop.Remove(row);
        this.DockerStopPicker.Refresh();
    }

    private async Task<IReadOnlyList<PickerChoice>?> LoadContainersAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string>? containers = await this.catalog.GetDockerContainersAsync(cancellationToken).ConfigureAwait(true);
        return containers?.Select(name => new PickerChoice(name, name)).ToList();
    }
}
