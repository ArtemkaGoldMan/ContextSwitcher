using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>The languages a person has listed, as the test says; English alone unless told otherwise.</summary>
public sealed class FakePreferredLanguages : IPreferredLanguages
{
    public List<string> Languages { get; } = ["en-US"];

    public IReadOnlyList<string> Get() => this.Languages;
}
