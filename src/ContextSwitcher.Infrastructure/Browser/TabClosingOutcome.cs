using ContextSwitcher.Core.Messages;

namespace ContextSwitcher.Infrastructure.Browser;

/// <summary>How many tabs <see cref="IBrowserLauncher.CloseTabsAsync"/> closed, or why it couldn't.</summary>
public sealed record TabClosingOutcome(int Closed, UserMessage? Problem);
