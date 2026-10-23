using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Platform;
using ContextSwitcher.Core.Security;
using ContextSwitcher.Infrastructure.Automation;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.MacOS;
using Microsoft.Extensions.DependencyInjection;

namespace ContextSwitcher.Tests.Startup;

/// <summary>What the app is made of, as AppServices registers it.</summary>
public sealed class AppServicesTests
{
    /// <summary>
    /// A step with no handler is skipped without a word, so a registration dropped in a refactor
    /// would quietly stop a step. Every type the plan builder writes has one.
    /// </summary>
    [Fact]
    public void EveryTypeOfStepThePlanHoldsHasAHandler()
    {
        ServiceCollection services = new();
        AppServices.Register(services);
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        AutomationStepType[] neverPlanned = [AutomationStepType.OpenUrls, AutomationStepType.WriteState, AutomationStepType.AnalyticsBoundary];
        Assert.Equal(
            Enum.GetValues<AutomationStepType>().Except(neverPlanned).Order(),
            provider.GetServices<IStepHandler>().Select(handler => handler.Type).Order());
    }

    /// <summary>
    /// The tests build the Mac app, whose platform registrations choose what it has always had:
    /// everything in <c>~/.config</c>, a file replaced with one move, and the Mac's capabilities and
    /// commands.
    /// </summary>
    [Fact]
    public void TheMacAppKeepsWhatItHasAlwaysChosen()
    {
        ServiceCollection services = new();
        AppServices.Register(services);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
            provider.GetRequiredService<SettingsRoot>().Path);
        Assert.Same(FileReplacer.Once, provider.GetRequiredService<IFileReplacer>());
        Assert.Same(MacPlatform.Capabilities, provider.GetRequiredService<PlatformCapabilities>());
        Assert.Same(MacPlatform.AllowedCommands, provider.GetRequiredService<CommandAllowlist>());

        // macOS keeps to one copy itself and colours the menu bar icon: nothing new on a Mac.
        Assert.IsType<MacSingleInstance>(provider.GetRequiredService<ISingleInstance>());
        Assert.Equal(TrayIconLook.Template, provider.GetRequiredService<ITrayIconLook>().Current);
    }
}
