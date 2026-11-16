namespace ContextSwitcher.App.Startup;

/// <summary>
/// Whether the interface is drawn on the CPU or the GPU. The platform says which it starts with
/// (<c>PlatformCapabilities.SoftwareRendering</c>); the <c>CONTEXTSWITCHER_RENDERING</c> variable
/// overrides it either way, for trying the other one.
/// </summary>
public static class RenderingChoice
{
    /// <summary>The variable that overrides the platform's choice: <c>software</c> or <c>gpu</c>.</summary>
    public const string Variable = "CONTEXTSWITCHER_RENDERING";

    /// <summary>Whether to draw on the CPU, given what the variable holds and the platform's own choice.</summary>
    public static bool UseSoftware(string? variable, bool platformDefault)
    {
        if (string.Equals(variable, "software", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(variable, "gpu", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return platformDefault;
    }
}
