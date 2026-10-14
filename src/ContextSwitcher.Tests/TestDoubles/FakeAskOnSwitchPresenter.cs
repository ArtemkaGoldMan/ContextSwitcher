using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>Answers the way a test says, standing in for the person at the window.</summary>
public sealed class FakeAskOnSwitchPresenter : IAskOnSwitchPresenter
{
    /// <summary>What the person does with the window; skips by default.</summary>
    public Action<AskOnSwitchViewModel> Answer { get; set; } = viewModel => viewModel.Skip();

    /// <summary>Every window asked, in order.</summary>
    public List<AskOnSwitchViewModel> Asked { get; } = [];

    public Task<AskOnSwitchResult> AskAsync(AskOnSwitchViewModel viewModel)
    {
        this.Asked.Add(viewModel);
        this.Answer(viewModel);
        return viewModel.Completion;
    }
}
