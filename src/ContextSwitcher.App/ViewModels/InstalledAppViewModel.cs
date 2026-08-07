using Avalonia.Media.Imaging;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One selectable app in the Profile Setup app picker.
/// </summary>
public sealed class InstalledAppViewModel
{
    public InstalledAppViewModel(string name, Bitmap? icon, Action<string> pick)
    {
        this.Name = name;
        this.Icon = icon;
        this.PickCommand = new RelayCommand(() => pick(name));
    }

    public string Name { get; }

    public Bitmap? Icon { get; }

    public bool HasIcon => this.Icon is not null;

    public RelayCommand PickCommand { get; }
}
