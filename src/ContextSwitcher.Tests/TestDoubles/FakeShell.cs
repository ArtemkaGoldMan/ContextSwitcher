using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>Records what the app asked the system to open, and opens nothing.</summary>
public sealed class FakeShell : IShell
{
    public List<string> Links { get; } = [];

    public List<string> ShownFiles { get; } = [];

    public List<SystemPage> Pages { get; } = [];

    /// <summary>Whether a link finds an app to open it - a mail link on a Mac with no mail app doesn't.</summary>
    public bool LinksOpen { get; set; } = true;

    public Task<bool> OpenLinkAsync(string url, CancellationToken cancellationToken)
    {
        this.Links.Add(url);
        return Task.FromResult(this.LinksOpen);
    }

    public void ShowFile(string path) => this.ShownFiles.Add(path);

    public void OpenSystemPage(SystemPage page) => this.Pages.Add(page);
}
