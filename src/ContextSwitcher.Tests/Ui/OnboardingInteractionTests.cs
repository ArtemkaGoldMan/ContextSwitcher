using Avalonia.Controls;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Tests.Ui;

/// <summary>
/// The first-run wizard is the only screen a new user is guaranteed to see, and it blocks the app
/// until it is finished or skipped.
/// </summary>
public sealed class OnboardingInteractionTests : UiTest
{
    [Fact]
    public async Task ContinueWalksForwardThroughEveryStepAndBackReturns()
    {
        await OnUiThreadAsync(() =>
        {
            (OnboardingViewModel viewModel, Window window, _) = Show();

            Assert.True(viewModel.IsWelcomeStep);

            Click(window, FindVisibleButton(window, "Continue"));
            Assert.True(viewModel.IsWorkStep);
            Settle(window);

            Click(window, FindVisibleButton(window, "Continue"));
            Assert.True(viewModel.IsPersonalStep);
            Settle(window);

            Click(window, FindVisibleButton(window, "Continue"));
            Assert.True(viewModel.IsDoneStep);
            Settle(window);

            Click(window, FindVisibleButton(window, "Back"));
            Assert.True(viewModel.IsPersonalStep);
            Settle(window);
        });
    }

    /// <summary>
    /// Continue is replaced by Finish on the last step, so the wizard cannot be walked past its end.
    /// </summary>
    [Fact]
    public async Task TheLastStepOffersFinishInsteadOfContinue()
    {
        await OnUiThreadAsync(() =>
        {
            (OnboardingViewModel viewModel, Window window, _) = Show();

            for (int step = 0; step < 3; step++)
            {
                Click(window, FindVisibleButton(window, "Continue"));
                Settle(window);
            }

            Assert.True(viewModel.IsDoneStep);
            Assert.False(viewModel.CanGoNext);
            Assert.DoesNotContain(FindAll<Button>(window), b => b.Content as string == "Continue" && IsClickable(b));
            Assert.Contains(FindAll<Button>(window), b => (b.Content as string)?.StartsWith("Finish", StringComparison.Ordinal) == true && IsClickable(b));
        });
    }

    [Fact]
    public async Task FinishingWritesTheProfilesAndMarksOnboardingComplete()
    {
        await OnUiThreadAsync(() =>
        {
            (_, Window window, IAppState appState) = Show();

            for (int step = 0; step < 3; step++)
            {
                Click(window, FindVisibleButton(window, "Continue"));
                Settle(window);
            }

            Button finish = FindAll<Button>(window)
                .First(b => (b.Content as string)?.StartsWith("Finish", StringComparison.Ordinal) == true && IsClickable(b));
            Click(window, finish);
            PumpUntil(WaitUntil(() => appState.Configuration.OnboardingCompleted));
            Settle(window);

            Assert.True(appState.Configuration.OnboardingCompleted);
            Assert.Equal(2, appState.Configuration.Contexts.Count);

            // The wizard shows a briefcase and a house; the saved profiles carry the same icons.
            Assert.Equal(["briefcase", "house"], appState.Configuration.Contexts.Select(c => c.Icon));
        });
    }

    [Fact]
    public async Task SkippingStillLeavesAUsableConfiguration()
    {
        await OnUiThreadAsync(() =>
        {
            (_, Window window, IAppState appState) = Show();

            Click(window, FindVisibleButton(window, "Skip setup"));
            PumpUntil(WaitUntil(() => appState.Configuration.OnboardingCompleted));
            Settle(window);

            Assert.True(appState.Configuration.OnboardingCompleted);
            Assert.NotEmpty(appState.Configuration.Contexts);
        });
    }

    private static (OnboardingViewModel ViewModel, Window Window, IAppState AppState) Show()
    {
        UiScenario scenario = UiScenario.With(new AppConfiguration
        {
            ActiveContextId = "default",
            OnboardingCompleted = false,
            Contexts = [new ContextDefinition { Id = "default", DisplayName = "Default" }]
        });

        OnboardingViewModel viewModel = scenario.Onboarding();
        OnboardingWindow window = new() { DataContext = viewModel };
        window.Show();
        Settle(window);
        return (viewModel, window, scenario.AppState);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 400 && !condition(); attempt++)
        {
            await Task.Delay(5);
        }
    }
}
