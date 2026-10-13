using System.Diagnostics;
using System.Text.Json;

namespace ContextSwitcher.Tests.Startup;

/// <summary>
/// Runs the built app itself - Program.Main, AppHost's whole service graph, the command line - in a
/// home folder of its own. The other tests build their parts by hand, so a service added to a
/// constructor and never registered passed them all while the app failed at startup; this catches
/// that. Only commands that start no window, so nothing appears on screen.
/// </summary>
public sealed class AppStartupSmokeTests : IDisposable
{
    private readonly string home = Path.Combine(Path.GetTempPath(), $"cs-smoke-{Guid.NewGuid():N}");

    public AppStartupSmokeTests()
    {
        Directory.CreateDirectory(this.home);
    }

    /// <summary>Where this build keeps everything: a development (Debug) build its own folder, apart from the installed app's.</summary>
    private string ConfigDirectory => Path.Combine(
        this.home, ".config", AppExecutable().Contains("/bin/Debug/", StringComparison.Ordinal) ? "ContextSwitcher-dev" : "ContextSwitcher");

    public void Dispose() => Directory.Delete(this.home, recursive: true);

    /// <summary>A Mac that has never run it: the app writes a default configuration and checks it.</summary>
    [Fact]
    public async Task AFreshMacGetsAValidDefaultConfiguration()
    {
        (int exitCode, string output) = await this.RunAsync("validate-config", "--json");

        Assert.Equal(0, exitCode);
        Assert.True(JsonDocument.Parse(output).RootElement.GetProperty("isValid").GetBoolean());
        Assert.True(File.Exists(Path.Combine(this.ConfigDirectory, "settings.json")));
    }

    [Fact]
    public async Task ADryRunSwitchAndStatusRunEndToEnd()
    {
        (int switchExit, string switchOutput) = await this.RunAsync("switch", "--context", "default", "--dry-run", "--json");
        (int statusExit, string statusOutput) = await this.RunAsync("status", "--json");

        Assert.Equal(0, switchExit);
        JsonElement result = JsonDocument.Parse(switchOutput).RootElement;
        Assert.Equal(("Succeeded", true), (result.GetProperty("status").GetString(), result.GetProperty("dryRun").GetBoolean()));
        Assert.Equal(0, statusExit);
        Assert.Equal(string.Empty, JsonDocument.Parse(statusOutput).RootElement.GetProperty("currentContextId").GetString());
    }

    /// <summary>
    /// Run from the code, the app keeps to its own folder and never opens the installed app's - which
    /// is what made a fresh install show the profiles being worked on.
    /// </summary>
    [Fact]
    public async Task ADevelopmentBuildKeepsToItsOwnFolder()
    {
        await this.RunAsync("validate-config");

        bool development = AppExecutable().Contains("/bin/Debug/", StringComparison.Ordinal);
        Assert.Equal(development, File.Exists(Path.Combine(this.home, ".config", "ContextSwitcher-dev", "settings.json")));
        Assert.Equal(!development, File.Exists(Path.Combine(this.home, ".config", "ContextSwitcher", "settings.json")));
    }

    /// <summary>CONTEXTSWITCHER_CONFIG_DIR puts everything in another folder, whichever the build.</summary>
    [Fact]
    public async Task TheConfigFolderCanBeNamed()
    {
        string elsewhere = Path.Combine(this.home, "elsewhere");

        (int exitCode, _) = await this.RunAsync(["validate-config"], new Dictionary<string, string> { ["CONTEXTSWITCHER_CONFIG_DIR"] = elsewhere });

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(elsewhere, "settings.json")));
        Assert.False(Directory.Exists(Path.Combine(this.home, ".config")));
    }

    /// <summary>A settings file that isn't JSON is moved aside, not lost, and the app still starts.</summary>
    [Fact]
    public async Task ABrokenSettingsFileIsMovedAsideAndSaidSo()
    {
        Directory.CreateDirectory(this.ConfigDirectory);
        await File.WriteAllTextAsync(Path.Combine(this.ConfigDirectory, "settings.json"), "{ this isn't json");

        (int exitCode, string output) = await this.RunAsync("validate-config");

        Assert.Equal(4, exitCode);
        Assert.Contains("moved aside", output, StringComparison.Ordinal);
        Assert.Single(Directory.GetFiles(this.ConfigDirectory, "settings.json.corrupt.*"));
    }

    private Task<(int ExitCode, string Output)> RunAsync(params string[] arguments) =>
        this.RunAsync(arguments, new Dictionary<string, string>());

    private async Task<(int ExitCode, string Output)> RunAsync(string[] arguments, Dictionary<string, string> environment)
    {
        ProcessStartInfo start = new(AppExecutable())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.Environment["HOME"] = this.home;

        // Only what the test asks for: one set in the shell running the tests would send the app elsewhere.
        start.Environment.Remove("CONTEXTSWITCHER_CONFIG_DIR");
        foreach ((string name, string value) in environment)
        {
            start.Environment[name] = value;
        }
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(timeout.Token);
        return (process.ExitCode, await output + await errors);
    }

    /// <summary>The app as built beside these tests, in the same configuration.</summary>
    private static string AppExecutable()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".github")))
        {
            directory = directory.Parent;
        }

        string configuration = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ? "Release" : "Debug";
        string executable = Path.Combine(directory!.FullName, "src", "ContextSwitcher.App", "bin", configuration, "net10.0", "ContextSwitcher");
        Assert.True(File.Exists(executable), $"The app isn't built at {executable}.");
        return executable;
    }
}
