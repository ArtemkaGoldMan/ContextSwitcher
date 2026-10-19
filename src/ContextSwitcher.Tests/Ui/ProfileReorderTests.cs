using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views.Pages;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Ui;

/// <summary>Dragging profiles into another order on the Profiles page - the order of the menu bar and the dashboard too.</summary>
public sealed class ProfileReorderTests : UiTest
{
    private readonly AppState appState = new();

    /// <summary>Holding a card and moving it past the next puts it there: the order is saved, and the cards show it.</summary>
    [Fact]
    public async Task DraggingACardDownPastTheNextOnePutsItThere()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, StubContextSwitchService switchService, _) = Create();
            Window window = ShowWindow(Page(viewModel), height: 700);

            Drag(window, CardOf(window, "Work"), CardOf(window, "Personal"), pastTheMiddle: true);
            PumpUntil(WaitUntil(() => this.appState.Configuration.Contexts[0].Id == "personal"));
            Settle(window);

            Assert.Equal(["personal", "work", "study"], this.appState.Configuration.Contexts.Select(c => c.Id));
            Assert.Equal(["Personal", "Work", "Study"], viewModel.Rows.Select(row => row.DisplayName));

            // Moving a card is not clicking it: the profile was not turned on or off.
            Assert.Null(switchService.LastRequest);
        });
    }

    [Fact]
    public async Task DraggingACardUpPutsItAboveTheOnesItPasses()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, _, _) = Create();
            Window window = ShowWindow(Page(viewModel), height: 700);

            Drag(window, CardOf(window, "Study"), CardOf(window, "Work"), pastTheMiddle: false);
            PumpUntil(WaitUntil(() => this.appState.Configuration.Contexts[0].Id == "study"));

            Assert.Equal(["study", "work", "personal"], this.appState.Configuration.Contexts.Select(c => c.Id));
        });
    }

    /// <summary>A card held and let go without having gone anywhere is a click that wavered, and is one.</summary>
    [Fact]
    public async Task APressThatBarelyMovesIsStillAClick()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, StubContextSwitchService switchService, _) = Create();
            Window window = ShowWindow(Page(viewModel), height: 700);
            Button card = CardOf(window, "Personal");
            Point centre = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), window)!.Value;

            window.MouseMove(centre, RawInputModifiers.None);
            window.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(centre + new Point(2, 2), RawInputModifiers.LeftMouseButton);
            window.MouseUp(centre + new Point(2, 2), MouseButton.Left, RawInputModifiers.None);
            Settle(window);

            Assert.Equal("personal", switchService.LastRequest?.TargetContextId);
            Assert.Equal(["work", "personal", "study"], this.appState.Configuration.Contexts.Select(c => c.Id));
        });
    }

    [Fact]
    public async Task EscapeWhileHoldingPutsTheCardBack()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, StubContextSwitchService switchService, _) = Create();
            Window window = ShowWindow(Page(viewModel), height: 700);
            Button card = CardOf(window, "Work");
            Point start = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), window)!.Value;
            Point down = start + new Point(0, card.Bounds.Height * 1.6);

            window.MouseMove(start, RawInputModifiers.None);
            window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
            Hold(window);
            window.MouseMove(down, RawInputModifiers.LeftMouseButton);
            Settle(window);
            Assert.NotNull(FindAll<ContentPresenter>(window).FirstOrDefault(p => p.Classes.Contains("lifted")));

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.MouseUp(down, MouseButton.Left, RawInputModifiers.None);
            Settle(window);

            Assert.DoesNotContain(FindAll<ContentPresenter>(window), p => p.Classes.Contains("lifted") || p.Classes.Contains("reorder"));
            Assert.Equal(["work", "personal", "study"], this.appState.Configuration.Contexts.Select(c => c.Id));
            Assert.Null(switchService.LastRequest);
        });
    }

    /// <summary>The held card is lifted over the others, and the ones it passes make room.</summary>
    [Fact]
    public async Task WhileHeldTheCardIsLiftedAndTheOthersMakeRoom()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, _, _) = Create();
            Window window = ShowWindow(Page(viewModel), height: 700);
            Button work = CardOf(window, "Work");
            Button personal = CardOf(window, "Personal");
            Point start = work.TranslatePoint(new Point(work.Bounds.Width / 2, work.Bounds.Height / 2), window)!.Value;

            window.MouseMove(start, RawInputModifiers.None);
            window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
            Hold(window);
            window.MouseMove(start + new Point(0, work.Bounds.Height * 1.2), RawInputModifiers.LeftMouseButton);
            Settle(window);

            ContentPresenter lifted = work.FindAncestorOfType<ContentPresenter>()!;
            ContentPresenter passed = personal.FindAncestorOfType<ContentPresenter>()!;
            Assert.Contains("lifted", lifted.Classes);
            Assert.NotNull(lifted.RenderTransform);
            Assert.Contains("reorder", passed.Classes);
            Assert.NotNull(passed.RenderTransform);

            window.MouseUp(start + new Point(0, work.Bounds.Height * 1.2), MouseButton.Left, RawInputModifiers.None);
            Settle(window);
        });
    }

    /// <summary>Not everyone drags: Alt with the arrows moves the focused card a place, and the focus stays on it.</summary>
    [Fact]
    public async Task AltAndAnArrowMoveTheFocusedCardOnePlace()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, _, _) = Create();
            Window window = ShowWindow(Page(viewModel), height: 700);
            CardOf(window, "Work").Focus();
            Settle(window);

            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Alt);
            PumpUntil(WaitUntil(() => this.appState.Configuration.Contexts[0].Id == "personal"));
            Settle(window);

            Assert.Equal(["personal", "work", "study"], this.appState.Configuration.Contexts.Select(c => c.Id));
            Button moved = CardOf(window, "Work");
            Assert.True(moved.IsFocused, $"focused: {TopLevel.GetTopLevel(moved)?.FocusManager?.GetFocusedElement()?.GetType().Name}; card focusable={moved.Focusable}, enabled={moved.IsEffectivelyEnabled}, window active={window.IsActive}");

            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.Alt);
            PumpUntil(WaitUntil(() => this.appState.Configuration.Contexts[0].Id == "work"));

            Assert.Equal(["work", "personal", "study"], this.appState.Configuration.Contexts.Select(c => c.Id));
        });
    }

    /// <summary>The first card has no place above it, nor the last below: the arrows do nothing there.</summary>
    [Fact]
    public async Task TheArrowsStopAtTheEnds()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, _, _) = Create();
            Window window = ShowWindow(Page(viewModel), height: 700);
            CardOf(window, "Work").Focus();

            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.Alt);
            Settle(window);

            Assert.Equal(["work", "personal", "study"], this.appState.Configuration.Contexts.Select(c => c.Id));
        });
    }

    /// <summary>A card's own buttons take their clicks; pressing on one is no start of a drag.</summary>
    [Fact]
    public async Task PressingOnAnEditButtonDoesNotStartADrag()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, _, _) = Create();
            ContextDefinition? edited = null;
            viewModel.EditRequested += (_, context) => edited = context;
            Window window = ShowWindow(Page(viewModel), height: 700);
            Button edit = FindAll<Button>(window).First(b => b.Content as string == "Edit" && IsClickable(b));
            Point at = edit.TranslatePoint(new Point(edit.Bounds.Width / 2, edit.Bounds.Height / 2), window)!.Value;

            window.MouseMove(at, RawInputModifiers.None);
            window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(at + new Point(0, 120), RawInputModifiers.LeftMouseButton);
            window.MouseUp(at + new Point(0, 120), MouseButton.Left, RawInputModifiers.None);
            Settle(window);

            Assert.DoesNotContain(FindAll<ContentPresenter>(window), p => p.Classes.Contains("lifted"));
            Assert.Equal(["work", "personal", "study"], this.appState.Configuration.Contexts.Select(c => c.Id));
        });
    }

    /// <summary>A card is picked up by holding it, not by moving it: it lifts once held, and not before.</summary>
    [Fact]
    public async Task ACardIsPickedUpOnlyOnceItHasBeenHeld()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, _, _) = Create();
            Window window = ShowWindow(Page(viewModel), height: 700);
            Button card = CardOf(window, "Personal");
            Point at = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), window)!.Value;

            window.MouseMove(at, RawInputModifiers.None);
            window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            Assert.DoesNotContain(FindAll<ContentPresenter>(window), p => p.Classes.Contains("lifted"));

            Hold(window);
            Assert.Contains(FindAll<ContentPresenter>(window), p => p.Classes.Contains("lifted"));

            window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
            Settle(window);
        });
    }

    /// <summary>Moving at once, before the hold is up, picks nothing up: the press was going somewhere else.</summary>
    [Fact]
    public async Task MovingBeforeTheHoldIsUpDragsNothing()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, StubContextSwitchService switchService, _) = Create();
            Window window = ShowWindow(Page(viewModel), height: 700);
            Button card = CardOf(window, "Work");
            Point start = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), window)!.Value;
            Point far = start + new Point(0, card.Bounds.Height * 2.2);

            window.MouseMove(start, RawInputModifiers.None);
            window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(far, RawInputModifiers.LeftMouseButton);
            LetTheHoldGoBy();
            window.MouseMove(far + new Point(0, 4), RawInputModifiers.LeftMouseButton);
            window.MouseUp(far, MouseButton.Left, RawInputModifiers.None);
            Settle(window);

            Assert.Equal(["work", "personal", "study"], this.appState.Configuration.Contexts.Select(c => c.Id));
            Assert.DoesNotContain(FindAll<ContentPresenter>(window), p => p.Classes.Contains("lifted") || p.Classes.Contains("reorder"));
            Assert.Null(switchService.LastRequest);
        });
    }

    /// <summary>A card held and let go without moving goes back where it was - and is not clicked, so nothing is turned on.</summary>
    [Fact]
    public async Task HoldingACardAndLettingGoWithoutMovingChangesNothingAndTurnsNothingOn()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, StubContextSwitchService switchService, _) = Create();
            Window window = ShowWindow(Page(viewModel), height: 700);
            Button card = CardOf(window, "Personal");
            Point at = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), window)!.Value;

            window.MouseMove(at, RawInputModifiers.None);
            window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            Hold(window);
            window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
            Settle(window);

            Assert.DoesNotContain(FindAll<ContentPresenter>(window), p => p.Classes.Contains("lifted"));
            Assert.Equal(["work", "personal", "study"], this.appState.Configuration.Contexts.Select(c => c.Id));
            Assert.Null(switchService.LastRequest);
        });
    }

    /// <summary>A quick press is a click all the same: only holding is new.</summary>
    [Fact]
    public async Task AQuickPressStillTurnsTheProfileOn()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, StubContextSwitchService switchService, _) = Create();
            Window window = ShowWindow(Page(viewModel), height: 700);

            Click(window, CardOf(window, "Study"));

            Assert.Equal("study", switchService.LastRequest?.TargetContextId);
        });
    }

    /// <summary>The page with a hold short enough for a test to wait out.</summary>
    private static ProfilesPage Page(ProfilesViewModel viewModel) =>
        new() { DataContext = viewModel, HoldToDrag = TimeSpan.FromMilliseconds(25) };

    /// <summary>
    /// Holds on until the card is picked up. The pick-up is counted off on the thread pool and handed
    /// back to the UI thread, so on a busy CI machine it can come well after the hold is up: waiting a
    /// fixed time for it once failed the test with the card not yet lifted.
    /// </summary>
    private static void Hold(Window window) =>
        PumpUntil(WaitUntil(() => FindAll<ContentPresenter>(window).Any(p => p.Classes.Contains("lifted"))));

    /// <summary>Lets the time go by that a card has to be held for, for a press that should pick nothing up.</summary>
    private static void LetTheHoldGoBy() => PumpUntil(Task.Delay(150));

    private static Button CardOf(Window window, string name) =>
        FindAll<Button>(window).Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == name && IsClickable(b));

    /// <summary>Holds <paramref name="card"/>, carries it over <paramref name="onto"/> and lets go.</summary>
    private static void Drag(Window window, Button card, Button onto, bool pastTheMiddle)
    {
        Point start = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), window)!.Value;
        Point over = onto.TranslatePoint(new Point(onto.Bounds.Width / 2, onto.Bounds.Height / 2), window)!.Value;
        Point end = over + new Point(0, pastTheMiddle ? onto.Bounds.Height * 0.25 : -onto.Bounds.Height * 0.25);

        window.MouseMove(start, RawInputModifiers.None);
        window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
        Hold(window);
        window.MouseMove(start + new Point(0, (end.Y - start.Y) / 2), RawInputModifiers.LeftMouseButton);
        window.MouseMove(end, RawInputModifiers.LeftMouseButton);
        window.MouseUp(end, MouseButton.Left, RawInputModifiers.None);
        Settle(window);
    }

    private (ProfilesViewModel ViewModel, StubContextSwitchService SwitchService, ConfigurationStore Store) Create()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts =
            [
                new ContextDefinition { Id = "work", DisplayName = "Work", AccentColor = "#2F6FED" },
                new ContextDefinition { Id = "personal", DisplayName = "Personal", AccentColor = "#20A67A" },
                new ContextDefinition { Id = "study", DisplayName = "Study", AccentColor = "#E0A030" }
            ]
        };
        ConfigurationValidator validator = new();
        this.appState.UpdateConfiguration(configuration, validator.Validate(configuration));
        this.appState.UpdateState(new CurrentContextState { CurrentContextId = "work" });

        InMemoryJsonStore inner = new();
        ConfigPaths paths = new("/tmp/context-switcher-ui-tests");
        inner.Seed(paths.SettingsPath, configuration);
        YieldingJsonStore jsonStore = new(inner);
        ConfigurationStore store = new(jsonStore, paths, validator, this.appState, new TestLogger());
        StubContextSwitchService switchService = new();

        return (new ProfilesViewModel(this.appState, switchService, store, new SwitchNoticeViewModel(this.appState, DateTimeOffset.UtcNow)), switchService, store);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5);
        }
    }
}
