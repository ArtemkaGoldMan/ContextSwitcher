using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Platform;
using ContextSwitcher.Core.Security;
using ContextSwitcher.Core.Updates;
using ContextSwitcher.Infrastructure.Automation;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Windows;
using ContextSwitcher.Infrastructure.Windows.Applications;
using Microsoft.Extensions.DependencyInjection;

namespace ContextSwitcher.Tests.Windows;

/// <summary>
/// The Windows app, as AppServices registers it there: everything it asks for is there, with Windows'
/// own choices in place of the Mac's.
/// </summary>
public sealed class WindowsAppServicesTests
{
    /// <summary>A service nothing registered would end the app at the first window that needs it.</summary>
    [Fact]
    public void EverythingTheAppAsksForIsThere()
    {
        using ServiceProvider provider = Build(validate: true);

        Assert.NotNull(provider.GetRequiredService<DashboardViewModel>());
        Assert.NotNull(provider.GetRequiredService<MainAppViewModel>());
        Assert.NotNull(provider.GetRequiredService<OnboardingViewModel>());
        Assert.NotNull(provider.GetRequiredService<IContextSwitchService>());
        Assert.NotEmpty(provider.GetServices<IHousekeeping>());
    }

    [Fact]
    public void EveryTypeOfStepThePlanHoldsHasAHandler()
    {
        using ServiceProvider provider = Build(validate: true);

        AutomationStepType[] neverPlanned = [AutomationStepType.OpenUrls, AutomationStepType.WriteState, AutomationStepType.AnalyticsBoundary];
        Assert.Equal(
            Enum.GetValues<AutomationStepType>().Except(neverPlanned).Order(),
            provider.GetServices<IStepHandler>().Select(handler => handler.Type).Order());
    }

    /// <summary>Everything in %APPDATA%, a file replaced while another holds it open, and Windows' shell.</summary>
    [Fact]
    public void TheWindowsAppHasWindowsOwnChoices()
    {
        using ServiceProvider provider = Build(validate: false);

        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), provider.GetRequiredService<SettingsRoot>().Path);
        Assert.Same(FileReplacer.RetryingWhileHeldOpen, provider.GetRequiredService<IFileReplacer>());
        Assert.Same(WindowsPlatform.Capabilities, provider.GetRequiredService<PlatformCapabilities>());
        Assert.Same(WindowsPlatform.AllowedCommands, provider.GetRequiredService<CommandAllowlist>());
        Assert.Same(WindowsPlatform.Assets, provider.GetRequiredService<ReleaseAssets>());
        Assert.IsType<WindowsPopoverWindow>(provider.GetRequiredService<IPopoverWindow>());
        Assert.IsType<WindowsSingleInstance>(provider.GetRequiredService<ISingleInstance>());
        Assert.IsType<WindowsTrayIconLook>(provider.GetRequiredService<ITrayIconLook>());
        Assert.IsType<WindowsShell>(provider.GetRequiredService<IShell>());
    }

    /// <summary>The quitter and the processes look at the same listing, and the app is given the same one the quitter uses.</summary>
    [Fact]
    public void AppsAreFoundOpenedAndClosedByWindowsOwnParts()
    {
        using ServiceProvider provider = Build(validate: true);

        Assert.IsType<WindowsAppLauncher>(provider.GetRequiredService<IAppLauncher>());
        Assert.IsType<WindowsAppQuitter>(provider.GetRequiredService<IAppQuitter>());
        Assert.Same(provider.GetRequiredService<WindowsAppProcesses>(), provider.GetRequiredService<IAppProcesses>());

        // What the picker lists, and what is read as the app starts, are the one service, so the icons
        // drawn while warming up are the ones the picker finds.
        Assert.Same(provider.GetRequiredService<WindowsInstalledApps>(), provider.GetRequiredService<IInstalledAppsService>());
        Assert.Same(provider.GetRequiredService<WindowsInstalledApps>(), Assert.Single(provider.GetServices<IWarmUp>()));
        Assert.Same(provider.GetRequiredService<AppIdentities>(), provider.GetRequiredService<AppIdentities>());
    }

    private static ServiceProvider Build(bool validate)
    {
        ServiceCollection services = new();
        AppServices.Register(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = validate, ValidateScopes = validate });
    }
}
