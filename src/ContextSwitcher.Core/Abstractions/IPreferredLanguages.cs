namespace ContextSwitcher.Core.Abstractions;

/// <summary>The languages the person has listed in the system's settings, most preferred first: "en-US", "pl-PL".</summary>
public interface IPreferredLanguages
{
    /// <summary>The list as the system has it now.</summary>
    IReadOnlyList<string> Get();
}
