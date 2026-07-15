using System.Windows.Input;

namespace ContextSwitcher.App.ViewModels;

public sealed class QuickLinkViewModel(string title, string url, ICommand openCommand)
{
    public string Title { get; } = title;

    public string Url { get; } = url;

    public ICommand OpenCommand { get; } = openCommand;
}
