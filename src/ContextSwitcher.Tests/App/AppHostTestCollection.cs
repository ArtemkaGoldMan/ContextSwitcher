namespace ContextSwitcher.Tests.App;

/// <summary>
/// Groups every test that reads or writes <c>AppHost</c>'s static <c>Configuration</c>/<c>State</c>
/// properties so they run sequentially rather than racing each other across parallel xUnit
/// collections - that state is process-global, not per-instance.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AppHostTestCollection
{
    public const string Name = "AppHost";
}
