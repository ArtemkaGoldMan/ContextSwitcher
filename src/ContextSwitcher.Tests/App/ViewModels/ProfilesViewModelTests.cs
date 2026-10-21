using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.ViewModels;

/// <summary>
/// <see cref="ProfilesViewModel"/>'s commands run through test doubles whose async members all
/// complete on already-finished <see cref="Task"/>s, and this process has no synchronization
/// context - so <c>ICommand.Execute</c> runs each command's async chain synchronously to
/// completion before returning, and these tests assert immediately without awaiting anything.
/// </summary>
public sealed class ProfilesViewModelTests
{
    private readonly AppState appState = new();

    [Fact]
    public void AddCommandRaisesEditRequestedWithNullContext()
    {
        (ProfilesViewModel viewModel, _, _) = CreateViewModel(TwoContexts());
        bool raised = false;
        ContextDefinition? requested = new ContextDefinition { Id = "sentinel" };
        viewModel.EditRequested += (_, context) =>
        {
            raised = true;
            requested = context;
        };

        viewModel.AddCommand.Execute(null);

        Assert.True(raised);
        Assert.Null(requested);
    }

    /// <summary>The state the switch wrote is published by the switch itself - see SwitchCoordinatorTests.</summary>
    [Fact]
    public void ClickingAProfileSwitchesToIt()
    {
        AppConfiguration configuration = TwoContexts();
        (ProfilesViewModel viewModel, StubContextSwitchService switchService, _) = CreateViewModel(configuration);
        switchService.Result = new ContextSwitchResult("personal", "work", ContextSwitchStatus.Succeeded, [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "corr");

        viewModel.Rows[1].ToggleCommand.Execute(null);

        Assert.Equal("personal", switchService.LastRequest?.TargetContextId);
        Assert.Equal(ContextSwitchSource.ProfilesPage, switchService.LastRequest?.Source);
    }

    /// <summary>
    /// A switch turned away because another was running wrote nothing to the state, so the page
    /// tells the notice itself - otherwise clicking the profile would seem to do nothing at all.
    /// </summary>
    [Fact]
    public void ARejectedSwitchIsReportedInTheNotice()
    {
        AppConfiguration configuration = TwoContexts();
        (ProfilesViewModel viewModel, StubContextSwitchService switchService, _) = CreateViewModel(configuration);
        switchService.Result = new ContextSwitchResult("personal", null, ContextSwitchStatus.Cancelled, [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "corr");

        viewModel.Rows[1].ToggleCommand.Execute(null);

        Assert.True(viewModel.Notice.IsShown);
        Assert.Equal("Couldn't switch to Personal", viewModel.Notice.Title);
        Assert.False(viewModel.HasErrorMessage);
    }

    /// <summary>
    /// There is no Activate button any more: clicking the profile that's on turns it off, as its
    /// button in the menu bar popover does - and a turn-off turned away says so in those words.
    /// </summary>
    [Fact]
    public void ClickingTheProfileThatsOnTurnsItOff()
    {
        AppConfiguration configuration = TwoContexts();
        (ProfilesViewModel viewModel, StubContextSwitchService switchService, _) = CreateViewModel(configuration);
        this.appState.UpdateState(new CurrentContextState { CurrentContextId = "work" });
        switchService.Result = new ContextSwitchResult(string.Empty, "work", ContextSwitchStatus.Cancelled, [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "corr");
        ProfileRowViewModel work = viewModel.Rows.Single(row => row.Context.Id == "work");

        work.ToggleCommand.Execute(null);

        Assert.True(switchService.LastRequest?.IsTurnOff);
        Assert.Equal("Couldn't turn off Work", viewModel.Notice.Title);
    }

    /// <summary>The profile that's on stands out: it glows and is washed in its own colour, and its name is bold.</summary>
    [Fact]
    public void TheProfileThatsOnIsHighlightedInItsOwnColour()
    {
        AppConfiguration configuration = TwoContexts();
        this.appState.UpdateState(new CurrentContextState { CurrentContextId = "work" });
        (ProfilesViewModel viewModel, _, _) = CreateViewModel(configuration);
        ProfileRowViewModel on = viewModel.Rows.Single(row => row.IsActive);
        ProfileRowViewModel off = viewModel.Rows.Single(row => !row.IsActive);

        Assert.Equal(Avalonia.Media.FontWeight.Bold, on.NameWeight);
        Assert.NotEqual(Avalonia.Media.Colors.Transparent, Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(on.CardTint).Color);
        Assert.Equal(Avalonia.Media.Color.Parse("#2F6FED").R, on.Glow[0].Color.R);
        Assert.True(on.GlowUnderPointer[0].Color.A > on.Glow[0].Color.A, "the glow grows under the pointer");

        Assert.Equal(Avalonia.Media.FontWeight.Medium, off.NameWeight);
        Assert.Equal(Avalonia.Media.Colors.Transparent, Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(off.CardTint).Color);
    }

    [Fact]
    public void DuplicateCommandSavesCopyWithUniqueId()
    {
        AppConfiguration configuration = TwoContexts();
        (ProfilesViewModel viewModel, _, InMemoryJsonStore jsonStore) = CreateViewModel(configuration);
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");

        viewModel.Rows[0].DuplicateCommand.Execute(null);

        AppConfiguration? saved = jsonStore.Get<AppConfiguration>(configPaths.SettingsPath);
        Assert.NotNull(saved);
        Assert.Equal(3, saved.Contexts.Count);
        Assert.Contains(saved.Contexts, c => c.Id == "work-copy" && c.DisplayName == "Work Copy");
    }

    [Fact]
    public void DeleteCommandRefusesToDeleteTheActiveProfile()
    {
        AppConfiguration configuration = TwoContexts();
        (ProfilesViewModel viewModel, _, InMemoryJsonStore jsonStore) = CreateViewModel(configuration, activeContextId: "work");
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");

        ProfileRowViewModel workRow = viewModel.Rows[0];
        workRow.DeleteCommand.Execute(null);
        workRow.DeleteCommand.Execute(null);

        Assert.True(viewModel.HasErrorMessage);
        Assert.Null(jsonStore.Get<AppConfiguration>(configPaths.SettingsPath));
    }

    [Fact]
    public void DeleteCommandRemovesNonActiveProfile()
    {
        AppConfiguration configuration = TwoContexts();
        (ProfilesViewModel viewModel, _, InMemoryJsonStore jsonStore) = CreateViewModel(configuration, activeContextId: "work");
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");

        ProfileRowViewModel personalRow = viewModel.Rows[1];
        personalRow.DeleteCommand.Execute(null);
        personalRow.DeleteCommand.Execute(null);

        AppConfiguration? saved = jsonStore.Get<AppConfiguration>(configPaths.SettingsPath);
        Assert.NotNull(saved);
        Assert.Single(saved.Contexts);
        Assert.Equal("work", saved.Contexts[0].Id);
    }

    /// <summary>
    /// The last profile can't go, even with none on - turned off - to stop it being the active one:
    /// an app with no profiles has nothing to switch to.
    /// </summary>
    [Fact]
    public void DeleteCommandKeepsTheLastProfile()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work", AccentColor = "#2F6FED" }]
        };
        (ProfilesViewModel viewModel, _, InMemoryJsonStore jsonStore) = CreateViewModel(configuration, activeContextId: string.Empty);

        viewModel.Rows[0].DeleteCommand.Execute(null);
        viewModel.Rows[0].DeleteCommand.Execute(null);

        Assert.Equal("At least one profile is required.", viewModel.ErrorMessage);
        Assert.Null(jsonStore.Get<AppConfiguration>(new ConfigPaths("/tmp/context-switcher-tests").SettingsPath));
    }

    [Fact]
    public void ACopyOfACopyGetsTheNextFreeId()
    {
        AppConfiguration configuration = TwoContexts() with
        {
            Contexts = [.. TwoContexts().Contexts, new ContextDefinition { Id = "work-copy", DisplayName = "Work Copy", AccentColor = "#2F6FED" }]
        };
        (ProfilesViewModel viewModel, _, InMemoryJsonStore jsonStore) = CreateViewModel(configuration);

        viewModel.Rows[0].DuplicateCommand.Execute(null);

        AppConfiguration saved = jsonStore.Get<AppConfiguration>(new ConfigPaths("/tmp/context-switcher-tests").SettingsPath)!;
        Assert.Equal(["work", "personal", "work-copy", "work-copy-2"], saved.Contexts.Select(context => context.Id));
    }

    /// <summary>The order of the profiles is the order of the list; moving one rewrites it, and the cards follow at once.</summary>
    [Fact]
    public async Task MovingAProfileSavesTheNewOrderAndReordersTheCards()
    {
        (ProfilesViewModel viewModel, _, InMemoryJsonStore jsonStore) = CreateViewModel(ThreeContexts());

        await viewModel.MoveAsync("work", 2);

        AppConfiguration saved = jsonStore.Get<AppConfiguration>(new ConfigPaths("/tmp/context-switcher-tests").SettingsPath)!;
        Assert.Equal(["personal", "study", "work"], saved.Contexts.Select(context => context.Id));
        Assert.Equal(["personal", "study", "work"], this.appState.Configuration.Contexts.Select(context => context.Id));
        Assert.Equal(["Personal", "Study", "Work"], viewModel.Rows.Select(row => row.DisplayName));
    }

    /// <summary>Which profile is on does not change with where it stands in the list.</summary>
    [Fact]
    public async Task MovingAProfileKeepsWhichOneIsActive()
    {
        (ProfilesViewModel viewModel, _, _) = CreateViewModel(ThreeContexts(), activeContextId: "personal");

        await viewModel.MoveAsync("personal", 0);

        Assert.Equal("personal", this.appState.State.CurrentContextId);
        Assert.True(viewModel.Rows[0].IsActive);
    }

    [Theory]
    [InlineData("work", 0)]
    [InlineData("nobody", 1)]
    public async Task MovingToWhereItIsOrOfAProfileThatIsNotThereChangesNothing(string id, int to)
    {
        (ProfilesViewModel viewModel, _, InMemoryJsonStore jsonStore) = CreateViewModel(ThreeContexts());

        await viewModel.MoveAsync(id, to);

        Assert.Null(jsonStore.Get<AppConfiguration>(new ConfigPaths("/tmp/context-switcher-tests").SettingsPath));
        Assert.Equal(["work", "personal", "study"], this.appState.Configuration.Contexts.Select(context => context.Id));
    }

    [Theory]
    [InlineData(99, "personal", "study", "work")]
    [InlineData(-5, "work", "personal", "study")]
    public async Task APlaceBeyondTheEndsIsTheEnd(int to, string first, string second, string third)
    {
        (ProfilesViewModel viewModel, _, _) = CreateViewModel(ThreeContexts());

        await viewModel.MoveAsync("work", to);

        Assert.Equal([first, second, third], this.appState.Configuration.Contexts.Select(context => context.Id));
    }

    [Fact]
    public async Task MovingByOneGoesUpOrDown()
    {
        (ProfilesViewModel viewModel, _, _) = CreateViewModel(ThreeContexts());

        await viewModel.MoveByAsync("personal", +1);
        Assert.Equal(["work", "study", "personal"], this.appState.Configuration.Contexts.Select(context => context.Id));

        await viewModel.MoveByAsync("personal", -2);
        Assert.Equal(["personal", "work", "study"], this.appState.Configuration.Contexts.Select(context => context.Id));
    }

    /// <summary>A move that was not saved leaves the cards in the order the profiles really are in.</summary>
    [Fact]
    public async Task AMoveThatCouldNotBeSavedPutsTheCardsBack()
    {
        (ProfilesViewModel viewModel, _, InMemoryJsonStore jsonStore) = CreateViewModel(ThreeContexts());
        jsonStore.WriteFailurePaths.Add(new ConfigPaths("/tmp/context-switcher-tests").SettingsPath);

        await Assert.ThrowsAsync<IOException>(() => viewModel.MoveAsync("work", 2));

        Assert.Equal(["work", "personal", "study"], this.appState.Configuration.Contexts.Select(context => context.Id));
    }

    /// <summary>The save publishes the order the cards are already in; making them all again for that would take the focus with the old ones.</summary>
    [Fact]
    public async Task TheCardsAreNotMadeAgainWhenTheSavePublishesTheOrderTheyAreInAlready()
    {
        (ProfilesViewModel viewModel, _, _) = CreateViewModel(ThreeContexts());

        await viewModel.MoveAsync("work", 2);
        IReadOnlyList<ProfileRowViewModel> afterTheMove = viewModel.Rows;
        this.appState.UpdateConfiguration(this.appState.Configuration, new ConfigurationValidationResult([]));

        Assert.Same(afterTheMove, viewModel.Rows);
    }

    private static AppConfiguration ThreeContexts() => TwoContexts() with
    {
        Contexts = [.. TwoContexts().Contexts, new ContextDefinition { Id = "study", DisplayName = "Study", AccentColor = "#E0A030" }]
    };

    private static AppConfiguration TwoContexts()
    {
        return new AppConfiguration
        {
            ActiveContextId = "work",
            Contexts =
            [
                new ContextDefinition { Id = "work", DisplayName = "Work", AccentColor = "#2F6FED" },
                new ContextDefinition { Id = "personal", DisplayName = "Personal", AccentColor = "#20A67A" }
            ]
        };
    }

    private (ProfilesViewModel ViewModel, StubContextSwitchService SwitchService, InMemoryJsonStore JsonStore) CreateViewModel(
        AppConfiguration configuration, string activeContextId = "work")
    {
        this.appState.UpdateConfiguration(configuration, new ConfigurationValidationResult([]));
        this.appState.UpdateState(new CurrentContextState { CurrentContextId = activeContextId });

        InMemoryJsonStore jsonStore = new();
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");
        StubContextSwitchService switchService = new();
        ConfigurationStore configurationStore = new(jsonStore, configPaths, new ConfigurationValidator(), this.appState, new TestLogger());

        ProfilesViewModel viewModel = new(this.appState, switchService, configurationStore, new SwitchNoticeViewModel(this.appState, DateTimeOffset.UtcNow));
        return (viewModel, switchService, jsonStore);
    }
}
