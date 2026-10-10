using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views;
using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Tests.App;

namespace ContextSwitcher.Tests.Ui;

/// <summary>How the menu bar popover is drawn: its outline, and its chart.</summary>
[Collection(AppHostTestCollection.Name)]
public sealed class DashboardLookTests : UiTest
{
    /// <summary>
    /// The card is rounded, so the window's own square corners must be fully see-through. Its shadow
    /// used to be clipped at the window's edges, which left those corners faintly grey: a square
    /// frame around a round card, the first thing anyone saw on opening the dashboard.
    /// </summary>
    [Fact]
    public async Task ThePopoversSquareCornersAreFullyTransparent()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            DashboardWindow window = new(scenario.Dashboard(), () => { });
            window.Show();
            Settle(window);

            WriteableBitmap frame = window.CaptureRenderedFrame()!;
            using ILockedFramebuffer pixels = frame.Lock();
            int right = pixels.Size.Width - 1, bottom = pixels.Size.Height - 1;
            byte Alpha(int x, int y) => System.Runtime.InteropServices.Marshal.ReadByte(pixels.Address, (y * pixels.RowBytes) + (x * 4) + 3);

            Assert.Equal([0, 0, 0, 0], new[] { Alpha(0, 0), Alpha(right, 0), Alpha(0, bottom), Alpha(right, bottom) }.Select(a => (int)a));
            window.Close();
        });
    }

    /// <summary>
    /// The last seven days as a rounded column per day: the busiest day fills the chart's height,
    /// the others scale to it, today's label is bold, and the legend gives each profile's total.
    /// </summary>
    [Fact]
    public async Task TheChartDrawsAColumnPerDayScaledToTheBusiestOne()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            scenario.Clock.UtcNow = new DateTimeOffset(2026, 10, 8, 18, 0, 0, TimeSpan.Zero);
            Record(scenario, "work", new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero), hours: 6);
            Record(scenario, "personal", new DateTimeOffset(2026, 10, 5, 19, 0, 0, TimeSpan.Zero), hours: 2);
            Record(scenario, "work", new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero), hours: 4);

            DashboardViewModel viewModel = scenario.Dashboard();
            DashboardWindow window = new(viewModel, () => { });
            window.Show();
            PumpUntil(WaitUntil(() => viewModel.HasBalanceData));
            Settle(window);

            List<BalanceDayViewModel> days = viewModel.BalanceChart.Days.ToList();
            Assert.Equal(7, days.Count);
            Assert.True(days[^1].IsToday);
            Assert.Equal(5, days.Count(day => day.IsEmpty));

            // The busiest day (8h) fills the 56px chart; 4h is half of it.
            Assert.Equal(56, days.Max(day => day.Segments.Sum(segment => segment.Height)), precision: 1);
            Assert.Equal(28, days.Single(day => day.Segments.Count == 1).Segments[0].Height, precision: 1);

            TextBlock today = FindControl<TextBlock>(window, t => t.Text == days[^1].Label && t.Classes.Contains("caption"));
            Assert.Equal(FontWeight.SemiBold, today.FontWeight);
            Assert.Equal(
                ["Work 10h", "Personal 2h"],
                viewModel.BalanceChart.Totals.Select(total => $"{total.DisplayName} {total.TotalHoursDisplay}"));

            // Drawn, not just modelled: every recorded hour is a visible colored bar.
            List<Border> bars = window.GetVisualDescendants().OfType<Border>()
                .Where(b => b.DataContext is BalanceSegmentViewModel && IsClickable(b))
                .ToList();
            Assert.Equal(3, bars.Count);
            window.Close();
        });
    }

    /// <summary>The popover's close button, and Escape, put it away like a click outside does.</summary>
    [Fact]
    public async Task TheCloseButtonAndEscapeBothDismissThePopover()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            DashboardWindow window = new(scenario.Dashboard(), () => { });
            window.Show();
            Settle(window);

            Click(window, FindControl<Button>(window, b => Avalonia.Automation.AutomationProperties.GetName(b) == "Close"));
            Assert.False(window.IsVisible, "the close button left the popover open");

            window.Show();
            Settle(window);
            window.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Escape, null);
            Settle(window);
            Assert.False(window.IsVisible, "Escape left the popover open");
            window.Close();
        });
    }

    private static void Record(UiScenario scenario, string contextId, DateTimeOffset start, int hours) =>
        scenario.Sessions.AppendCompletedSessionAsync(
            new ContextSession
            {
                SessionId = $"{contextId}-{start:O}",
                ContextId = contextId,
                StartedAt = start,
                EndedAt = start.AddHours(hours),
                DurationSeconds = hours * 3600
            },
            CancellationToken.None).GetAwaiter().GetResult();

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 400 && !condition(); attempt++)
        {
            await Task.Delay(5);
        }
    }
}
