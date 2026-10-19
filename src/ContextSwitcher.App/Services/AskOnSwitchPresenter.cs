using Avalonia.Threading;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views;
using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.App.Services;

/// <summary>Shows the window asking about everything else open, and waits for the answer.</summary>
public interface IAskOnSwitchPresenter
{
    /// <summary>The answer - or a skip, if the window is closed.</summary>
    Task<AskOnSwitchResult> AskAsync(AskOnSwitchViewModel viewModel);
}

/// <summary>
/// Asks in an <see cref="AskOnSwitchWindow"/>, on the UI thread, named after the app. The apps' and
/// sites' icons are filled in once found, after it has opened - it never waits on them.
/// </summary>
public sealed class WindowAskOnSwitchPresenter(IInstalledAppsService installedApps, ISiteIcons siteIcons, IAppInfo appInfo) : IAskOnSwitchPresenter
{
    /// <inheritdoc />
    public async Task<AskOnSwitchResult> AskAsync(AskOnSwitchViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            AskOnSwitchWindow window = new(viewModel) { Title = appInfo.DisplayName };
            window.Show();
            window.Activate();
            _ = viewModel.LoadIconsAsync(installedApps, CancellationToken.None);
            _ = viewModel.LoadSiteIconsAsync(siteIcons, CancellationToken.None);
        });

        return await viewModel.Completion.ConfigureAwait(true);
    }
}
