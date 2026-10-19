using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Infrastructure.Automation;
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
}
