using System.Globalization;
using System.Runtime.CompilerServices;

namespace ContextSwitcher.Tests.Localization;

/// <summary>
/// The tests find buttons by their English text and compare English messages, so they run in English
/// whatever language the Mac running them is set to - set before any test, and before the headless
/// UI thread starts.
/// </summary>
internal static class TestCulture
{
#pragma warning disable CA2255 // A test assembly is exactly where a process-wide setting like this belongs.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void UseEnglish()
    {
        CultureInfo english = CultureInfo.GetCultureInfo("en");
        CultureInfo.DefaultThreadCurrentUICulture = english;
        CultureInfo.CurrentUICulture = english;
    }
}
