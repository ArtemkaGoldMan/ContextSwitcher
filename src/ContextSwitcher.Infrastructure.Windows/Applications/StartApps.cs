using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Core.ProcessExecution;
using System.Text.Json;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// The apps in the Start menu, desktop and Store alike, which is where a person looks for an app.
/// Listed by PowerShell's <c>Get-StartApps</c> and kept for a few minutes.
/// </summary>
public sealed class StartApps
{
    /// <summary>
    /// What PowerShell runs, fixed: nothing from the settings goes into it, and it holds no double
    /// quote, which a command line would take apart. Each name, id and target is printed as Base64 of
    /// UTF-8 ([Convert]::ToBase64String), which survives any console code page. The target is where
    /// the Start menu's shortcut of that name points, read with the shell's own WScript.Shell.
    /// </summary>
    public const string Script =
        "$shell = New-Object -ComObject WScript.Shell; $targets = @{}; " +
        "foreach ($root in [Environment]::GetFolderPath('CommonPrograms'), [Environment]::GetFolderPath('Programs')) { " +
        "Get-ChildItem -LiteralPath $root -Recurse -Filter *.lnk -ErrorAction SilentlyContinue | ForEach-Object { " +
        "$target = $shell.CreateShortcut($_.FullName).TargetPath; " +
        "if ($target -and -not $targets.ContainsKey($_.BaseName)) { $targets[$_.BaseName] = $target } } }; " +
        "$encode = { param($text) [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$text)) }; " +
        "Get-StartApps | ForEach-Object { [pscustomobject]@{ n = (& $encode $_.Name); i = (& $encode $_.AppID); " +
        "t = (& $encode $targets[$_.Name]) } } | ConvertTo-Json -Compress";

    private readonly IProcessRunner processRunner;
    private readonly IClock clock;
    private readonly ILogger logger;
    private readonly StartAppsOptions options;
    private readonly SemaphoreSlim gate = new(1, 1);

    private IReadOnlyList<StartApp> apps = [];
    private DateTimeOffset? listedAt;

    public StartApps(IProcessRunner processRunner, IClock clock, ILogger logger, StartAppsOptions options)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        this.processRunner = processRunner;
        this.clock = clock;
        this.logger = logger;
        this.options = options;
    }

    /// <summary>
    /// The apps the last listing found, however long ago; empty before the first. What looks an app up
    /// by its name without waiting for PowerShell, once something has listed them.
    /// </summary>
    public IReadOnlyList<StartApp> Known => this.apps;

    /// <summary>The apps in the Start menu by name; empty when Windows couldn't list them, and then tried again next time.</summary>
    public async Task<IReadOnlyList<StartApp>> GetAsync(CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this.listedAt is { } at && this.clock.UtcNow - at < this.options.KeepFor)
            {
                return this.apps;
            }

            ProcessResult result = await this.processRunner.RunAsync(
                new ProcessStartOptions("powershell", ["-NoProfile", "-NonInteractive", "-Command", Script], this.options.Timeout),
                cancellationToken).ConfigureAwait(false);
            if (result.TimedOut || result.ExitCode != 0)
            {
                this.logger.Warn("App", "StartAppsNotListed", $"The Start menu's apps couldn't be listed: {result.StandardError.Trim()}");
                return [];
            }

            try
            {
                this.apps = StartAppsParser.Parse(result.StandardOutput, this.options.Filter);
            }
            catch (JsonException ex)
            {
                this.logger.Warn("App", "StartAppsNotRead", "The Start menu's apps came back in a form that couldn't be read.", ex);
                return [];
            }

            this.listedAt = this.clock.UtcNow;
            return this.apps;
        }
        finally
        {
            this.gate.Release();
        }
    }
}
