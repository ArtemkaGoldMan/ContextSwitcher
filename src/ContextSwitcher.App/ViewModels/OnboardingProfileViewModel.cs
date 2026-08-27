using System.Collections.ObjectModel;
using Avalonia.Media;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One profile being built by the first-run wizard. Deliberately a much smaller surface than
/// <see cref="ProfileSetupViewModel"/> - name, colour and apps only. Everything else keeps its
/// default and is discoverable later in Profile Setup; asking a first-time user about Docker
/// containers or switch-policy criticality is exactly what the wizard exists to avoid.
/// </summary>
public sealed class OnboardingProfileViewModel : ViewModelBase
{
    private string displayName;

    public OnboardingProfileViewModel(
        string id,
        string displayName,
        string menuBarLabel,
        string accentColor,
        AppPickerViewModel appPicker)
    {
        this.Id = id;
        this.displayName = displayName;
        this.MenuBarLabel = menuBarLabel;
        this.AccentColor = accentColor;
        this.AppPicker = appPicker;
        this.AccentBrush = AccentColorParser.ToBrush(accentColor);
        this.Apps = [];
    }

    public string Id { get; }

    public string MenuBarLabel { get; }

    public string AccentColor { get; }

    public IBrush AccentBrush { get; }

    public AppPickerViewModel AppPicker { get; }

    public string DisplayName
    {
        get => this.displayName;
        set => this.SetProperty(ref this.displayName, value);
    }

    public ObservableCollection<AppRowViewModel> Apps { get; }

    public bool HasApps => this.Apps.Count > 0;

    /// <summary>
    /// Adds a picked app with both toggles on, matching Profile Setup's behavior.
    /// </summary>
    public void AddApp(string appName)
    {
        if (this.Apps.Any(row => string.Equals(row.Name, appName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        this.Apps.Add(new AppRowViewModel(appName, launchOnEnter: true, closeOnLeave: true, this.RemoveApp)
        {
            Icon = this.AppPicker.ResolveIcon(appName)
        });

        this.AppPicker.SearchText = string.Empty;
        this.AppPicker.Refresh();
        this.OnPropertyChanged(nameof(this.HasApps));
    }

    /// <summary>
    /// Materializes the wizard's answers into a real context. Unset fields keep
    /// <see cref="ContextDefinition"/>'s defaults so nothing is silently configured behind the
    /// user's back.
    /// </summary>
    public ContextDefinition ToContextDefinition()
    {
        return new ContextDefinition
        {
            Id = this.Id,
            DisplayName = string.IsNullOrWhiteSpace(this.DisplayName) ? this.Id : this.DisplayName.Trim(),
            MenuBarLabel = this.MenuBarLabel,
            AccentColor = this.AccentColor,
            LaunchApps = this.Apps.Where(a => a.LaunchOnEnter).Select(a => a.Name).ToList(),
            CloseApps = this.Apps.Where(a => a.CloseOnLeave).Select(a => a.Name).ToList()
        };
    }

    private void RemoveApp(AppRowViewModel row)
    {
        this.Apps.Remove(row);
        this.AppPicker.Refresh();
        this.OnPropertyChanged(nameof(this.HasApps));
    }
}
