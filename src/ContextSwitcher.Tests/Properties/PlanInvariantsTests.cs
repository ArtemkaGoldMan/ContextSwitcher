using System.Text.Json;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Tests.Properties;

/// <summary>
/// What a plan must be whatever the two profiles are - checked on a few hundred random ones rather
/// than the handful anyone thought to write down. Each seed is a different pair of profiles, the same
/// every run, so a failure names the seed to replay it.
/// </summary>
public sealed class PlanInvariantsTests
{
    private static readonly string[] Pool =
        ["Slack", "Cursor", "Visual Studio Code", "Spotify", "Telegram", "Figma", "Notes", "Zoom", "Docker", "Obsidian"];

    public static TheoryData<int> Seeds => new(Enumerable.Range(1, 120));

    /// <summary>An app the person said never to touch is in no step at all, however a profile lists it.</summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void AnIgnoredAppIsInNoStep(int seed)
    {
        (ContextDefinition? previous, ContextDefinition? target, string[] ignored) = Draw(seed);

        AutomationPlan plan = new AutomationPlanBuilder().Build(previous, target, null, ignored);

        foreach (string app in ignored)
        {
            Assert.DoesNotContain(plan.Steps.SelectMany(AppsIn), listed => string.Equals(listed, app, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>An app is never quit and opened in the same switch: the entered profile's apps are left running.</summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoAppIsBothClosedAndLaunched(int seed)
    {
        (ContextDefinition? previous, ContextDefinition? target, string[] ignored) = Draw(seed);

        AutomationPlan plan = new AutomationPlanBuilder().Build(previous, target, null, ignored);

        string[] closed = AppsOf(plan, AutomationStepType.CloseApplications);
        string[] launched = AppsOf(plan, AutomationStepType.LaunchApplications);
        Assert.Empty(closed.Intersect(launched, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>An app is not closed and hidden in one switch, and a hidden one is not also opened.</summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoAppIsBothHiddenAndClosed(int seed)
    {
        (ContextDefinition? previous, ContextDefinition? target, string[] ignored) = Draw(seed);

        AutomationPlan plan = new AutomationPlanBuilder().Build(previous, target, null, ignored);

        Assert.Empty(AppsOf(plan, AutomationStepType.HideApplications).Intersect(AppsOf(plan, AutomationStepType.CloseApplications), StringComparer.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryStepHasItsOwnId(int seed)
    {
        (ContextDefinition? previous, ContextDefinition? target, string[] ignored) = Draw(seed);

        AutomationPlan plan = new AutomationPlanBuilder().Build(previous, target, null, ignored);

        Assert.Equal(plan.Steps.Count, plan.Steps.Select(step => step.Id).Distinct().Count());
    }

    /// <summary>The same two profiles make the same plan, step for step.</summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void ThePlanForTheSameInputIsTheSame(int seed)
    {
        (ContextDefinition? previous, ContextDefinition? target, string[] ignored) = Draw(seed);

        string once = Describe(new AutomationPlanBuilder().Build(previous, target, null, ignored));
        string twice = Describe(new AutomationPlanBuilder().Build(previous, target, null, ignored));

        Assert.Equal(once, twice);
    }

    /// <summary>Nothing to leave and nothing to enter is nothing to do.</summary>
    [Fact]
    public void NoProfilesMeansNoSteps()
    {
        Assert.DoesNotContain(new AutomationPlanBuilder().Build(null, null, null, []).Steps, step => step.Type != AutomationStepType.WriteState && step.Type != AutomationStepType.AnalyticsBoundary);
    }

    private static (ContextDefinition? Previous, ContextDefinition? Target, string[] Ignored) Draw(int seed)
    {
        Random random = new(seed);
        string[] Some() => Pool.Where(_ => random.Next(100) < 30).ToArray();

        ContextDefinition Profile(string id)
        {
            string[] launch = Some();
            string[] close = Some();
            string[] hide = Some().Except(close, StringComparer.OrdinalIgnoreCase).ToArray();
            return new ContextDefinition { Id = id, DisplayName = id, LaunchApps = launch, CloseApps = close, HideApps = hide };
        }

        ContextDefinition? previous = random.Next(10) == 0 ? null : Profile("a");
        ContextDefinition? target = random.Next(10) == 0 ? null : Profile("b");
        return (previous, target, Some());
    }

    private static string[] AppsOf(AutomationPlan plan, AutomationStepType type) =>
        plan.Steps.Where(step => step.Type == type).SelectMany(AppsIn).ToArray();

    private static IEnumerable<string> AppsIn(AutomationStep step) =>
        step.Arguments.TryGetValue("apps", out string? json) ? JsonSerializer.Deserialize<string[]>(json) ?? [] : [];

    private static string Describe(AutomationPlan plan) =>
        string.Join('\n', plan.Steps.Select(step => $"{step.Id}|{step.Type}|{string.Join(';', step.Arguments.OrderBy(a => a.Key).Select(a => $"{a.Key}={a.Value}"))}"));
}
