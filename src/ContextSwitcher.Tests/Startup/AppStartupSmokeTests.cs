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

    private string ConfigDirectory => Path.Combine(this.home, ".config", "ContextSwitcher");

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

    private async Task<(int ExitCode, string Output)> RunAsync(params string[] arguments)
    {
        ProcessStartInfo start = new(AppExecutable())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.Environment["HOME"] = this.home;
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
