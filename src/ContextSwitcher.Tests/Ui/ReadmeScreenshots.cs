using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using ContextSwitcher.App.Resources;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.Core.Platform;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Ui;

/// <summary>
/// Draws the screenshots of the main window in the README and the user guide - the Profiles page,
/// the card a switch with problems leaves, and Settings - in light and dark, at twice the size, in a
/// macOS window frame.
/// The app's windows can't be clicked through from a script, so they are drawn here, from the same
/// pages, with demo profiles. Only when <c>CONTEXTSWITCHER_SCREENSHOTS</c> names the folder to write
/// them to - scripts/readme-screenshots.sh does; in an ordinary test run it does nothing.
/// </summary>
public sealed class ReadmeScreenshots : UiTest
{
    private const string FolderVariable = "CONTEXTSWITCHER_SCREENSHOTS";
    private const double Scale = 2;
    private const double Width = 960;

    [Fact]
    public async Task DrawTheMainWindowScreenshots()
    {
        if (Environment.GetEnvironmentVariable(FolderVariable) is not { Length: > 0 } folder)
        {
            return;
        }

        Directory.CreateDirectory(folder);
        CultureInfo? language = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            await OnUiThreadAsync(() =>
            {
                foreach (ThemeVariant theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                {
                    Shoot(folder, "profiles", theme, 560, (_, _) => { });
                    Shoot(folder, "switch-problems", theme, 600, (scenario, _) => scenario.AppState.UpdateState(WithProblems()));
                    Shoot(folder, "settings", theme, 850, (_, viewModel) => viewModel.ShowSettings());
                }
            });
        }
        finally
        {
            Strings.Culture = language;
        }
    }

    /// <summary>Three profiles, Personal on - the ones every screenshot in the README and the user guide shows.</summary>
    private static AppConfiguration Demo() => new()
    {
        ActiveContextId = "personal",
        IgnoredApps = ["Telegram"],
        Contexts =
        [
            new ContextDefinition { Id = "work", DisplayName = "Work", Icon = "briefcase", AccentColor = "#2F6FED" },
            new ContextDefinition { Id = "personal", DisplayName = "Personal", Icon = "house", AccentColor = "#20A67A" },
            new ContextDefinition { Id = "study", DisplayName = "Study", Icon = "book", AccentColor = "#8B5CF6" }
        ]
    };

    /// <summary>A switch to Personal that left an app asking to save, and one in the background.</summary>
    private static CurrentContextState WithProblems()
    {
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        return new CurrentContextState
        {
            CurrentContextId = "personal",
            PreviousContextId = "work",
            LastSwitchCompletedAt = completedAt,
            LastSwitchStatus = nameof(ContextSwitchStatus.SucceededWithWarnings),
            LastErrors = [Line(SwitchMessages.CouldNotClose.With("Visual Studio Code")), Line(SwitchMessages.StayedInBackground.With("Steam"))]
        };

        StateError Line(UserMessage message) => new()
        {
            StepId = "CloseApplications.work",
            Message = message.English,
            MessageKey = message.Key,
            MessageArguments = message.Arguments,
            OccurredAt = completedAt
        };
    }

    /// <summary>
    /// Draws the window as a Retina screen does, at twice its size: its page goes into a macOS frame
    /// inside the same window, and the window is drawn at a scaling of two. Scaling the frame itself
    /// up, or drawing it into a bitmap of twice the density, put the active profile's card in the
    /// wrong place and everything else at four times the size.
    /// </summary>
    private static void Shoot(string folder, string name, ThemeVariant theme, double height, Action<UiScenario, MainAppViewModel> prepare)
    {
        UiScenario scenario = UiScenario.With(Demo());
        scenario.AppState.UpdateState(new CurrentContextState { CurrentContextId = "personal" });
        UpdateChecker checker = new(
            scenario.Releases, scenario.Updater, new FakeSystemNotifications(), scenario.AppState, new AppInfo(), scenario.Clock, new TestLogger());
        MainAppViewModel viewModel = new(
            scenario.AppState, scenario.SwitchService, scenario.ConfigurationStore, scenario.Permissions,
            scenario.Shell, scenario.Analytics, scenario.InstalledApps,
            scenario.Notice, new UpdatesViewModel(checker, scenario.Shell), scenario.Diagnostics, new FakePreferredLanguages(), PlatformCapabilities.MacOS,
            new FakeLoginItem { Status = LoginItemStatus.On }, scenario.ProfileSetup);

        // See-through from the start: a window takes its transparency when it is first shown.
        MainAppWindow window = new(viewModel)
        {
            Width = Width,
            Height = height,
            RequestedThemeVariant = theme,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent]
        };
        window.Show();
        prepare(scenario, viewModel);
        PumpUntil(Task.Delay(300));
        Settle(window);

        IBrush? background = window.Background;
        Control page = (Control)window.Content!;
        window.Content = null;
        window.Background = Brushes.Transparent;
        window.Content = Framed(page, height, theme, background);
        window.Width = Width + Margin.Left + Margin.Right;
        window.Height = height + TitleBarHeight + 1 + Margin.Top + Margin.Bottom;
        window.SetRenderScaling(Scale);
        PumpUntil(Task.Delay(200));
        Settle(window);

        Save(window, System.IO.Path.Combine(folder, $"{name}-{(theme == ThemeVariant.Dark ? "dark" : "light")}.png"));
        window.Close();
    }

    /// <summary>
    /// Saves the window's frame with its transparency. The frame keeps the see-through room around
    /// the window, but is marked opaque, which saved it as solid white or black; the same pixels are
    /// saved again as what they are - with alpha - and at the 144 dpi a Mac screenshot has.
    /// </summary>
    private static void Save(Window window, string path)
    {
        WriteableBitmap frame = window.CaptureRenderedFrame()!;
        using ILockedFramebuffer pixels = frame.Lock();
        using WriteableBitmap withAlpha = new(pixels.Format, AlphaFormat.Premul, pixels.Address, pixels.Size, new Vector(144, 144), pixels.RowBytes);
        withAlpha.Save(path, new PngBitmapEncoderOptions());
    }

    private const double TitleBarHeight = 28;

    /// <summary>Transparent room around the window for its shadow, deeper below, as macOS leaves it.</summary>
    private static readonly Thickness Margin = new(44, 30, 44, 56);

    /// <summary>
    /// The page as macOS frames a window: a title bar with its three buttons and the title, rounded
    /// corners, a hairline edge and the shadow, on a transparent ground - as a screenshot of a window
    /// comes out.
    /// </summary>
    private static Border Framed(Control page, double height, ThemeVariant theme, IBrush? background)
    {
        bool dark = theme == ThemeVariant.Dark;

        Grid titleBar = new() { Height = TitleBarHeight, Background = Brush(dark ? "#2C2C2C" : "#EBEBEB") };
        StackPanel buttons = new() { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(13, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        foreach ((string fill, string rim) in new[] { ("#FF5F57", "#E14640"), ("#FEBC2E", "#DFA023"), ("#28C840", "#1DAD2B") })
        {
            buttons.Children.Add(new Ellipse { Width = 12, Height = 12, Fill = Brush(fill), Stroke = Brush(rim), StrokeThickness = 0.5 });
        }

        titleBar.Children.Add(buttons);
        titleBar.Children.Add(new TextBlock
        {
            Text = "Context Switcher",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush(dark ? "#DCDCDC" : "#4B4B4B"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        Border edge = new() { Height = 1, Background = Brush(dark ? "#141414" : "#D4D4D4") };
        page.Width = Width;
        page.Height = height;
        DockPanel body = new() { Background = background };
        DockPanel.SetDock(titleBar, Dock.Top);
        DockPanel.SetDock(edge, Dock.Top);
        body.Children.Add(titleBar);
        body.Children.Add(edge);
        body.Children.Add(page);

        Border window = new()
        {
            CornerRadius = new CornerRadius(10),
            Background = background,
            BorderBrush = Brush(dark ? "#5A5A5A" : "#2E000000"),
            BorderThickness = new Thickness(0.5),
            BoxShadow = BoxShadows.Parse("0 16 36 0 #59000000"),
            Child = new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = body }
        };

        return new Border { Padding = Margin, Background = Brushes.Transparent, Child = window };
    }

    private static SolidColorBrush Brush(string color) => new(Color.Parse(color));
}
