# Context Switcher V2 Agent Specification

This file is the permanent engineering brief, system prompt, architectural blueprint, and implementation checklist for **Context Switcher V2**. Treat it as the source of truth when creating code, reviewing changes, writing tests, preparing releases, and deciding whether a feature belongs in scope.

## 1. Product Mission

Context Switcher V2 is an open-source, lightweight macOS menu bar utility for developers and IT professionals who need strict separation between work and personal/hobby environments on the same Apple Silicon Mac.

The application must make switching contexts feel instant, reversible, and calm:

- Close or freeze distracting and resource-heavy processes from the previous context.
- Launch the correct applications, browser URLs, tab groups, optional browser profiles, Docker resources, music, wallpaper, theme, and Focus state for the new context.
- Preserve work-life boundaries by making the current mode obvious from the menu bar and dashboard.
- Track local context time without cloud storage, telemetry, or account requirements.

Non-negotiable product qualities:

- Native-feeling macOS experience.
- Menu bar first; no Dock icon during normal operation.
- Low memory footprint; avoid Electron-class overhead.
- All user configuration and analytics remain local by default.
- Every automation action must be explicit, inspectable, logged, and recoverable.
- The app must remain usable without donations, accounts, or network access.

## 2. Technical Baseline

| Area                 | Decision                                                                  |
| -------------------- | ------------------------------------------------------------------------- |
| Runtime              | .NET 10 LTS                                                               |
| Target OS            | macOS on Apple Silicon, `osx-arm64`                                       |
| UI                   | Avalonia UI with Metal/Skia rendering                                     |
| App Model            | Menu bar app with dropdown dashboard and optional settings windows        |
| Menu Bar Integration | `H.NotifyIcon.Avalonia`                                                   |
| Global Hotkeys       | `SharpHotkeys`                                                            |
| Charts               | `LiveCharts2.Avalonia`                                                    |
| DI                   | `Microsoft.Extensions.DependencyInjection`                                |
| Persistence          | Local JSON under `~/.config/ContextSwitcher/`                             |
| Scripts              | `osascript`, `open`, `defaults`, `shortcuts`, `docker`, app-specific CLIs |
| License              | MIT                                                                       |
| Release Packaging    | GitHub Actions on macOS runner, `.app` bundle, `.dmg` release asset       |
| Signing              | Unsigned by default; document quarantine removal                          |

Do not introduce a database, web server, telemetry platform, Electron shell, cloud synchronization, or paid licensing gate unless explicitly approved in a later specification.

## 3. Repository Layout

Create and maintain this structure:

```text
ContextSwitcher/
  agent.md
  LICENSE
  README.md
  .editorconfig
  .gitignore
  global.json
  ContextSwitcher.sln
  src/
    ContextSwitcher.App/
      ContextSwitcher.App.csproj
      App.axaml
      App.axaml.cs
      Program.cs
      app.manifest
      Assets/
        Icons/
          menu-work.pdf
          menu-personal.pdf
          menu-neutral.pdf
        Themes/
      Views/
        DashboardWindow.axaml
        DashboardWindow.axaml.cs
        SettingsWindow.axaml
        SettingsWindow.axaml.cs
      ViewModels/
        DashboardViewModel.cs
        ContextCardViewModel.cs
        SettingsViewModel.cs
      Controls/
        ContextSwitchButton.axaml
        QuickLinkButton.axaml
        InlineStatusBadge.axaml
      Styles/
        Colors.axaml
        Typography.axaml
        Controls.axaml
    ContextSwitcher.Core/
      ContextSwitcher.Core.csproj
      Abstractions/
        IClock.cs
        IJsonStore.cs
        ILogger.cs
        IProcessRunner.cs
        IScriptRunner.cs
        IContextSwitchService.cs
        IHotkeyService.cs
        IAnalyticsService.cs
      Analytics/
        ContextSession.cs
        ContextSessionLog.cs
        AnalyticsService.cs
        BalanceSummary.cs
      Automation/
        AutomationPlan.cs
        AutomationResult.cs
        AutomationStep.cs
        AutomationStepType.cs
        AutomationFailurePolicy.cs
      Configuration/
        AppConfiguration.cs
        ContextDefinition.cs
        BrowserManagementConfig.cs
        BrowserProfileConfig.cs
        DockerResourceConfig.cs
        HotkeyConfig.cs
        MediaConfig.cs
        ThemeConfig.cs
        WallpaperConfig.cs
        QuickLinkConfig.cs
        Validation/
          ConfigurationValidator.cs
          ConfigurationValidationError.cs
      Contexts/
        ContextSwitchRequest.cs
        ContextSwitchResult.cs
        ContextSwitchService.cs
        CurrentContextState.cs
      Logging/
        LogEntry.cs
        LogLevel.cs
        LocalJsonLogger.cs
      Security/
        CommandAllowlist.cs
        CommandTemplate.cs
    ContextSwitcher.Infrastructure/
      ContextSwitcher.Infrastructure.csproj
      AppleScript/
        AppleScriptBuilder.cs
        AppleScriptRunner.cs
      Browser/
        BrowserLauncher.cs
      Cli/
        CliCommandRouter.cs
        CliExitCodes.cs
      Files/
        JsonFileStore.cs
        ConfigPaths.cs
      Hotkeys/
        SharpHotkeyService.cs
      MacOS/
        FocusModeController.cs
        MacThemeController.cs
        WallpaperController.cs
        MenuBarAppHost.cs
      ProcessExecution/
        ProcessRunner.cs
        ProcessResult.cs
        ProcessStartOptions.cs
    ContextSwitcher.Tests/
      ContextSwitcher.Tests.csproj
      Configuration/
      Contexts/
      Automation/
      Analytics/
      TestDoubles/
  scripts/
    package-dmg.sh
    install-local.sh
    clear-quarantine.sh
  docs/
    configuration.md
    automation-permissions.md
    shortcuts-integration.md
    release-process.md
  .github/
    workflows/
      release.yml
      ci.yml
```

Project references:

- `ContextSwitcher.App` references `Core` and `Infrastructure`.
- `ContextSwitcher.Infrastructure` references `Core`.
- `ContextSwitcher.Tests` references `Core` and `Infrastructure`.
- `Core` must not reference Avalonia, macOS-specific APIs, shell execution libraries, or UI packages.

## 4. Architectural Boundaries

### 4.1 Core

`ContextSwitcher.Core` owns domain models, validation, context switching orchestration, analytics rules, error contracts, and interfaces.

Core rules:

- No direct filesystem, process, shell, UI, or wall-clock calls.
- All side effects happen through abstractions.
- All models must be serializable with `System.Text.Json`.
- All switch operations return structured results, not bare booleans.

### 4.2 Infrastructure

`ContextSwitcher.Infrastructure` implements macOS automation, filesystem persistence, AppleScript execution, global hotkeys, browser launching, process execution, and CLI routing.

Infrastructure rules:

- Every command execution must go through `IProcessRunner`.
- Every AppleScript execution must go through `IScriptRunner`.
- Script templates must be centralized and testable.
- Never concatenate untrusted user input directly into shell commands.
- Prefer passing process arguments as arrays over shell strings.

### 4.3 App

`ContextSwitcher.App` owns Avalonia application startup, menu bar icon behavior, dashboard UI, settings UI, data binding, and view models.

App rules:

- View models call services; views do not run automation.
- No business logic in `.axaml.cs` except UI lifecycle glue.
- The Dock icon must be hidden in normal menu bar mode.
- Dashboard must remain fast with large logs by consuming summaries, not raw log scans on every render.

## 5. Dependency Injection Registry

`Program.cs` must configure one root service provider and keep registrations explicit.

Expected registration shape:

```csharp
var services = new ServiceCollection();

services.AddSingleton<IClock, SystemClock>();
services.AddSingleton<ConfigPaths>();
services.AddSingleton<IJsonStore, JsonFileStore>();
services.AddSingleton<IProcessRunner, ProcessRunner>();
services.AddSingleton<IScriptRunner, AppleScriptRunner>();
services.AddSingleton<ILogger, LocalJsonLogger>();

services.AddSingleton<ConfigurationValidator>();
services.AddSingleton<IAnalyticsService, AnalyticsService>();
services.AddSingleton<IContextSwitchService, ContextSwitchService>();
services.AddSingleton<IHotkeyService, SharpHotkeyService>();

services.AddSingleton<BrowserLauncher>();
services.AddSingleton<MacThemeController>();
services.AddSingleton<WallpaperController>();
services.AddSingleton<FocusModeController>();
services.AddSingleton<CliCommandRouter>();

services.AddTransient<DashboardViewModel>();
services.AddTransient<SettingsViewModel>();
services.AddSingleton<MenuBarAppHost>();
```

Lifetime policy:

- Singleton for stateless services, process wrappers, stores, and app-wide state coordinators.
- Transient for view models unless a view model explicitly owns state that must survive window recreation.
- Avoid scoped services; there is no request lifecycle.

Startup sequence:

1. Create config directory if missing.
2. Load or create default `settings.json`.
3. Validate configuration.
4. Load `state.json`.
5. Start analytics session for current context if valid.
6. Register global hotkeys.
7. Start menu bar host.
8. If CLI arguments are present, route the command and exit without starting full UI unless the command needs a running UI.

## 6. Local Persistence Contract

Base directory:

```text
~/.config/ContextSwitcher/
```

Files:

```text
~/.config/ContextSwitcher/
  settings.json
  state.json
  analytics.jsonl
  app.log.jsonl
  license.json
  backups/
    settings.2026-07-12T10-30-00Z.json
```

Persistence requirements:

- Use UTF-8 JSON.
- Use atomic writes for full JSON files: write temp file, flush, rename.
- Use append-only writes for `.jsonl` logs.
- Keep backups before writing `settings.json` from the UI.
- Recover from malformed JSON by renaming bad files to `.corrupt.<timestamp>` and recreating defaults.
- Do not delete user data automatically.

### 6.1 `settings.json`

Schema versioned configuration:

```json
{
    "schemaVersion": 1,
    "activeContextId": "work",
    "defaultSwitchTimeoutSeconds": 45,
    "showDockIcon": false,
    "analytics": {
        "enabled": true,
        "retentionDays": 365
    },
    "hotkeys": [
        {
            "id": "switch-work",
            "contextId": "work",
            "accelerator": "Cmd+Alt+Ctrl+W",
            "enabled": true
        },
        {
            "id": "switch-personal",
            "contextId": "personal",
            "accelerator": "Cmd+Alt+Ctrl+P",
            "enabled": true
        }
    ],
    "contexts": [
        {
            "id": "work",
            "displayName": "Work",
            "menuBarLabel": "WORK",
            "accentColor": "#2F6FED",
            "icon": "briefcase",
            "closeApps": ["Spotify", "Discord", "Steam"],
            "launchApps": ["Slack", "Microsoft Teams", "Visual Studio Code"],
            "browser_management": {
                "mode": "groups",
                "browser": "Chrome",
                "urls": [
                    "https://mail.google.com/",
                    "https://github.com/notifications"
                ],
                "tab_groups": ["Work-Core", "Finance"],
                "profiles": [
                    {
                        "browser": "Chrome",
                        "profile_directory": "Profile 1",
                        "urls": [
                            "https://mail.google.com/",
                            "https://github.com/notifications"
                        ]
                    }
                ],
                "avoid_duplicate_tabs": true
            },
            "theme": {
                "mode": "light"
            },
            "wallpaper": {
                "path": "/Users/artem/Pictures/wallpapers/work.jpg",
                "allSpaces": true
            },
            "focus": {
                "enabled": true,
                "modeName": "Work"
            },
            "media": {
                "player": "AppleMusic",
                "playlist": "Deep Focus",
                "autoPlay": false
            },
            "docker": {
                "start": ["postgres-work", "redis-work"],
                "stop": ["postgres-hobby", "redis-hobby"]
            },
            "quickLinks": [
                {
                    "title": "Runbook",
                    "url": "https://example.com/runbook",
                    "icon": "book-open"
                }
            ],
            "notes": ["Check incident queue before opening IDE."],
            "switchPolicy": {
                "continueOnNonCriticalFailure": true,
                "criticalSteps": ["LaunchRequiredApps", "ManageBrowserContext"]
            }
        },
        {
            "id": "personal",
            "displayName": "Personal",
            "menuBarLabel": "HOME",
            "accentColor": "#20A67A",
            "icon": "home",
            "closeApps": ["Slack", "Microsoft Teams"],
            "launchApps": ["Obsidian"],
            "browser_management": {
                "mode": "urls",
                "browser": "Default",
                "urls": ["https://youtube.com/"],
                "tab_groups": [],
                "profiles": [],
                "avoid_duplicate_tabs": true
            },
            "theme": {
                "mode": "dark"
            },
            "wallpaper": {
                "path": "/Users/artem/Pictures/wallpapers/personal.jpg",
                "allSpaces": true
            },
            "focus": {
                "enabled": false,
                "modeName": ""
            },
            "media": {
                "player": "Spotify",
                "playlist": "Liked Songs",
                "autoPlay": true
            },
            "docker": {
                "start": [],
                "stop": ["postgres-work", "redis-work"]
            },
            "quickLinks": [
                {
                    "title": "YouTube",
                    "url": "https://youtube.com/",
                    "icon": "play"
                }
            ],
            "notes": ["Work apps should be closed before hobby time."],
            "switchPolicy": {
                "continueOnNonCriticalFailure": true,
                "criticalSteps": []
            }
        }
    ]
}
```

Validation rules:

- `schemaVersion` must be supported.
- `contexts[].id` must be unique, lowercase, URL-safe, and stable.
- `activeContextId` must match an existing context.
- `hotkeys[].contextId` must match an existing context.
- `accentColor` must be a valid 6-digit hex color.
- `browser_management.mode` allowed values: `urls`, `groups`, `profiles`, `none`.
- `browser_management.browser` allowed values: `Default`, `Chrome`, `Brave`, `Safari`.
- `browser_management.urls[]` must use `http` or `https`.
- `browser_management.tab_groups[]` must be non-empty names when `mode == "groups"`.
- `browser_management.profiles[]` is optional and is used only when `mode == "profiles"` or as a user-selected fallback.
- `browser_management.profiles[].browser` allowed values: `Chrome`, `Brave`.
- `browser_management.profiles[].profile_directory` must be non-empty when profile mode is used.
- `browser_management.avoid_duplicate_tabs` controls best-effort tab focusing before opening URLs.
- `theme.mode` allowed values: `light`, `dark`, `system`.
- `media.player` allowed values: `AppleMusic`, `Spotify`, `None`.
- Paths may be missing at config time, but switching must report missing assets as warnings.
- User-defined commands, when later supported, must be allowlisted and displayed for confirmation.

### 6.2 `state.json`

Runtime state:

```json
{
    "schemaVersion": 1,
    "currentContextId": "work",
    "previousContextId": "personal",
    "lastSwitchStartedAt": "2026-07-12T10:15:30Z",
    "lastSwitchCompletedAt": "2026-07-12T10:15:37Z",
    "lastSwitchStatus": "SucceededWithWarnings",
    "lastErrors": [
        {
            "stepId": "focus.work",
            "message": "Focus mode could not be changed. Check Shortcuts permissions.",
            "occurredAt": "2026-07-12T10:15:36Z"
        }
    ]
}
```

### 6.3 `analytics.jsonl`

Append one JSON object per context session:

```json
{
    "schemaVersion": 1,
    "sessionId": "018fd1a4-7b64-7c30-a1a8-f50c6f78d111",
    "contextId": "work",
    "startedAt": "2026-07-12T08:00:00Z",
    "endedAt": "2026-07-12T12:00:00Z",
    "durationSeconds": 14400,
    "endReason": "Switch"
}
```

Session rules:

- Start a session when the app launches and a valid context is active.
- Close the previous session before switching to a new context.
- If the app crashed, close the stale session on next launch with `endReason: "RecoveredAfterCrash"`.
- Do not track active window titles, URLs visited, keystrokes, screenshots, or application usage beyond selected context duration.

### 6.4 `app.log.jsonl`

Append structured logs:

```json
{
    "timestamp": "2026-07-12T10:15:31Z",
    "level": "Information",
    "category": "ContextSwitch",
    "eventId": "SwitchStarted",
    "message": "Switching from personal to work.",
    "contextId": "work",
    "correlationId": "018fd1a4-7b64-7c30-a1a8-f50c6f78d222",
    "data": { "source": "hotkey" }
}
```

Log levels:

- `Trace`: only in debug builds or when explicitly enabled.
- `Debug`: development diagnostics.
- `Information`: switch start/finish, app startup, config migration.
- `Warning`: recoverable failed step.
- `Error`: failed switch, corrupt config, permission denial.
- `Critical`: unrecoverable startup failure.

Redaction:

- Never log full command lines containing tokens.
- Never log donation/license key values.
- Log URLs only as configured quick links or browser startup URLs; do not observe browser state.

## 7. Domain API Contracts

### 7.1 Context Switch Request

```csharp
public sealed record ContextSwitchRequest(
    string TargetContextId,
    ContextSwitchSource Source,
    bool DryRun = false,
    bool Force = false,
    string? CorrelationId = null);

public enum ContextSwitchSource
{
    MenuBar,
    Dashboard,
    GlobalHotkey,
    Cli,
    Shortcut,
    StartupRecovery,
    Test
}
```

### 7.2 Context Switch Result

```csharp
public sealed record ContextSwitchResult(
    string TargetContextId,
    string? PreviousContextId,
    ContextSwitchStatus Status,
    IReadOnlyList<AutomationResult> StepResults,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string CorrelationId);

public enum ContextSwitchStatus
{
    Succeeded,
    SucceededWithWarnings,
    Failed,
    Cancelled,
    NoOp
}
```

### 7.3 Automation Step

```csharp
public sealed record AutomationStep(
    string Id,
    AutomationStepType Type,
    string DisplayName,
    bool IsCritical,
    TimeSpan Timeout,
    IReadOnlyDictionary<string, string> Arguments);

public enum AutomationStepType
{
    CloseApplications,
    LaunchApplications,
    ManageBrowserContext,
    SetTheme,
    SetWallpaper,
    SetFocusMode,
    ControlMedia,
    StartDockerResources,
    StopDockerResources,
    OpenUrls,
    WriteState,
    AnalyticsBoundary
}
```

### 7.4 Automation Result

```csharp
public sealed record AutomationResult(
    string StepId,
    AutomationStepType Type,
    AutomationResultStatus Status,
    string Message,
    int? ExitCode,
    string? StandardOutput,
    string? StandardError,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

public enum AutomationResultStatus
{
    Succeeded,
    Skipped,
    Warning,
    Failed,
    TimedOut
}
```

### 7.5 Process Runner

```csharp
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        ProcessStartOptions options,
        CancellationToken cancellationToken);
}

public sealed record ProcessStartOptions(
    string FileName,
    IReadOnlyList<string> Arguments,
    TimeSpan Timeout,
    IReadOnlyDictionary<string, string>? Environment = null,
    string? WorkingDirectory = null,
    bool CaptureOutput = true);
```

Process runner rules:

- Do not use shell interpolation for normal commands.
- Capture stdout and stderr.
- Kill process tree on timeout when possible.
- Return structured results; never throw for non-zero exit codes.
- Throw only for programmer errors such as missing executable path in internal code.

## 8. Context Switching Pipeline

The switch pipeline must execute in this order:

1. Validate target context.
2. Acquire an in-process switch lock to prevent concurrent switches.
3. Emit `SwitchStarted` log with correlation ID.
4. If target is already active and `Force == false`, return `NoOp`.
5. Close current analytics session.
6. Build automation plan from previous and target context.
7. Execute resource-freeing steps:
    - Stop Docker resources from previous context.
    - Close unwanted apps.
8. Execute environment steps:
    - Theme.
    - Wallpaper.
    - Focus mode.
9. Execute activation steps:
    - Launch apps.
    - Manage browser context using URL, tab group, or profile mode.
    - Start Docker resources for target context.
    - Media action.
10. Write `state.json`.
11. Start analytics session for target context.
12. Update menu bar icon and dashboard state.
13. Emit `SwitchCompleted` or `SwitchFailed` log.

Failure policy:

- Critical step failure makes the switch status `Failed`.
- Non-critical step failure makes the switch status `SucceededWithWarnings`.
- If state write fails, status must be `Failed`.
- If analytics write fails, status may be `SucceededWithWarnings`, but must be visible in UI.
- A failed switch must not claim the target context is active unless state write succeeded.

Concurrency:

- Only one switch may run at a time.
- A second request while switching should return `Cancelled` or queue only if explicit queueing is later implemented.
- UI must show an in-progress state and disable context switch buttons during execution.

Cancellation:

- User cancellation may stop pending steps but must not interrupt an already-running `osascript` in an unsafe way unless timeout is reached.
- After cancellation, write a log entry and leave `state.json` unchanged unless the context was already committed.

## 9. macOS Command Templates

All commands below must be implemented through `IProcessRunner` with argument arrays. AppleScript source must be generated through `AppleScriptBuilder` with escaped string literals.

### 9.1 Gracefully Quit Applications

Purpose: replicate a clean `Cmd+Q` style quit and allow apps to preserve session state.

Command:

```text
osascript -e 'tell application "Slack" to if it is running then quit'
```

Generated argument array:

```csharp
[
  "-e",
  "tell application \"Slack\" to if it is running then quit"
]
```

Follow-up verification:

```text
osascript -e 'application "Slack" is running'
```

Rules:

- Wait up to 10 seconds per app.
- If app remains running, report warning unless app is critical.
- Do not use `killall` by default.
- Add future optional force-quit only behind explicit user setting.

### 9.2 Launch Applications

Command:

```text
open -a "Visual Studio Code"
```

Argument array:

```csharp
["-a", "Visual Studio Code"]
```

Rules:

- Launch apps sequentially by default to avoid UI storm.
- Treat missing app as warning unless listed as critical.
- Detect failure via non-zero exit code and stderr.

### 9.3 Browser URL Mode

Purpose: support users who do not separate work and personal browsing with profiles.

Command:

```text
open "https://github.com/"
```

Argument array:

```csharp
["https://github.com/"]
```

Best-effort duplicate prevention:

- If `avoidDuplicateTabs == true`, run browser-specific AppleScript before opening.
- For Safari, inspect windows/tabs and focus a tab whose URL matches exactly.
- For Chrome and Brave, inspect windows/tabs through AppleScript where supported and focus matching URL.
- If inspection fails because of permissions or browser scripting limitations, log a warning and fall back to `open`.
- Never read or persist arbitrary browsing history; only compare configured startup URLs.

Rules:

- Use the default browser when `browser == "Default"`.
- Use `open -a "Google Chrome" URL`, `open -a "Brave Browser" URL`, or `open -a "Safari" URL` when a concrete browser is configured.
- Validate all URLs before switch execution.
- Opening URLs is non-critical unless the context switch policy marks it critical.

### 9.4 Browser Tab Groups Mode

Purpose: support users who organize browser state with named tab groups instead of profiles.

Supported browsers:

- Safari: use AppleScript where macOS exposes tab group selection/focusing.
- Chrome: use best-effort AppleScript against Chrome windows/tab groups where available.
- Brave: use best-effort AppleScript against Brave windows/tab groups where available.

Behavior:

- For every configured `tabGroups[]` name, attempt to focus or activate the existing group.
- If the group cannot be found or the browser does not expose tab group automation reliably, report a warning.
- If `urls[]` are also configured, use them as fallback startup URLs after group activation fails.
- Do not create new tab groups in MVP unless a later specification explicitly adds that behavior.

Rules:

- Group names are user-visible strings and must be AppleScript-escaped.
- Group activation failures are warnings by default.
- Browser scripting permission failures must produce actionable dashboard messages.

### 9.5 Browser Profiles Mode

Purpose: retain strict browser isolation for users who want separate cookies, extensions, and tabs.

Chrome command:

```text
open -na "Google Chrome" --args --profile-directory="Profile 1" "https://mail.google.com/"
```

Argument array:

```csharp
[
  "-na",
  "Google Chrome",
  "--args",
  "--profile-directory=Profile 1",
  "https://mail.google.com/"
]
```

Rules:

- Use `-n` to allow separate launch behavior where macOS permits it.
- Use configured profile directory exactly.
- Do not inspect or close existing browser tabs.
- Never mix work and personal URLs in one profile launch.

Brave command:

```text
open -na "Brave Browser" --args --profile-directory="Default" "https://youtube.com/"
```

Argument array:

```csharp
[
  "-na",
  "Brave Browser",
  "--args",
  "--profile-directory=Default",
  "https://youtube.com/"
]
```

Rules:

- Profiles mode is optional; users may choose URL or tab group workflows instead.
- `profiles[]` may contain more than one browser/profile target, but each target must be explicit.
- Safari does not support Chromium `--profile-directory`; reject Safari profile entries during validation.

### 9.6 Toggle macOS Theme

Dark mode:

```text
osascript -e 'tell application "System Events" to tell appearance preferences to set dark mode to true'
```

Light mode:

```text
osascript -e 'tell application "System Events" to tell appearance preferences to set dark mode to false'
```

Rules:

- `system` mode means skip and leave current OS setting untouched.
- Failure usually indicates Automation permission problems; show a clear dashboard warning.

### 9.7 Set Wallpaper

AppleScript:

```applescript
tell application "System Events"
  tell every desktop
    set picture to "/Users/artem/Pictures/wallpapers/work.jpg"
  end tell
end tell
```

Rules:

- Validate path exists before running.
- If `allSpaces == false`, target only the current desktop if supported by implementation.
- Missing wallpaper is a warning, not a failed switch.

### 9.8 Focus Mode / Do Not Disturb

Primary strategy: use macOS Shortcuts because Focus mode scripting support varies by macOS version.

Command:

```text
shortcuts run "ContextSwitcher - Focus Work"
```

Argument array:

```csharp
["run", "ContextSwitcher - Focus Work"]
```

Expected user-created Shortcuts:

- `ContextSwitcher - Focus Work`
- `ContextSwitcher - Focus Personal`
- `ContextSwitcher - Focus Off`

Rules:

- The app must document exactly how to create these shortcuts.
- If the shortcut does not exist or fails, report warning with remediation.
- Do not require private macOS APIs.

### 9.9 Apple Music Control

Play playlist:

```text
osascript -e 'tell application "Music" to play playlist "Deep Focus"'
```

Pause:

```text
osascript -e 'tell application "Music" to pause'
```

Rules:

- `autoPlay == false` means prepare/open only if feasible, otherwise skip.
- Missing playlist is warning.

### 9.10 Spotify Control

AppleScript:

```applescript
tell application "Spotify"
  activate
  play track "spotify:playlist:PLAYLIST_ID"
end tell
```

Rules:

- Prefer Spotify URI in config when available.
- Plain playlist names are best-effort only and may be unsupported.
- Spotify automation failures are non-critical.

### 9.11 Docker Stop / Start

Stop:

```text
docker stop postgres-work redis-work
```

Argument array:

```csharp
["stop", "postgres-work", "redis-work"]
```

Start:

```text
docker start postgres-work redis-work
```

Argument array:

```csharp
["start", "postgres-work", "redis-work"]
```

Rules:

- If Docker CLI is unavailable, report warning.
- Empty container lists are skipped.
- Do not run `docker compose down` without explicit future support because it has wider side effects.
- Capture stderr because Docker commonly reports useful state there.

### 9.12 Open URLs

Command:

```text
open "https://example.com/runbook"
```

Argument array:

```csharp
["https://example.com/runbook"]
```

Rules:

- Validate URL scheme is `https` or `http`.
- Use context browser management for browser startup URLs.
- Use `open` only for quick links or explicitly configured target URLs.

## 10. CLI and Shortcuts Interface

The app must expose a CLI-friendly execution path so macOS Shortcuts can trigger context switches.

Initial command contract:

```text
ContextSwitcher switch --context work
ContextSwitcher switch --context personal
ContextSwitcher status
ContextSwitcher list-contexts
ContextSwitcher validate-config
ContextSwitcher open-dashboard
```

Exit codes:

| Code | Meaning                           |
| ---- | --------------------------------- |
| 0    | Success                           |
| 1    | General failure                   |
| 2    | Invalid arguments                 |
| 3    | Unknown context                   |
| 4    | Configuration invalid             |
| 5    | Switch completed with warnings    |
| 6    | Another switch is already running |

Output format:

- Human-readable by default.
- Add `--json` for machine-readable output.
- `--json` output must be valid JSON and must not include extra text.

Example JSON:

```json
{
    "status": "SucceededWithWarnings",
    "contextId": "work",
    "warnings": [
        "Focus mode could not be changed. Run docs/automation-permissions.md."
    ]
}
```

Shortcuts integration:

- Create a macOS Shortcut that runs shell command:

```text
/Applications/ContextSwitcher.app/Contents/MacOS/ContextSwitcher switch --context work
```

- Siri phrase examples:
    - "Switch to work"
    - "Switch to personal"
    - "Start focus mode"

## 11. Avalonia UI/UX Standards

### 11.1 Interaction Model

Primary UI is a dropdown mini-window from the menu bar icon.

Dashboard contents:

- Current context header with name, icon, accent color, and elapsed time.
- Two or more context switch buttons.
- Switch progress state with current step.
- Warnings panel for last switch.
- Quick links for active context.
- Rapid notes for active context.
- Work-life balance chart.
- Settings button.
- Quit button behind a secondary menu or footer action.

Settings window contents:

- Context list.
- Per-context app closing/launching configuration.
- Browser profile configuration.
- Hotkey editor.
- Automation permissions status.
- Analytics retention toggle.
- Theme/icon donation cosmetic section.

### 11.2 Visual Design

Design language:

- Premium but quiet.
- Dense enough for daily utility use.
- No landing-page composition inside the app.
- No decorative cards nested inside other cards.
- Use compact panels, clear section labels, and direct controls.

Window dimensions:

- Dashboard default width: 360 px.
- Dashboard min width: 320 px.
- Dashboard max width: 420 px.
- Dashboard height: content-based up to 620 px, scroll if needed.
- Settings default: 860 x 620 px.

Spacing:

- Outer dashboard padding: 14 px.
- Section spacing: 12 px.
- Control spacing: 8 px.
- Border radius: 8 px maximum for cards/buttons unless native control style requires otherwise.

Typography:

- Use system font.
- Header: 18 px semibold.
- Section title: 12 px medium, uppercase only where useful.
- Body: 13 px.
- Caption: 11 px.
- Do not use viewport-scaled text.
- Letter spacing must be 0.

Colors:

- Respect macOS light/dark appearance.
- Use active context accent color sparingly: current status, selected context, chart highlight, menu icon variant.
- Avoid one-note palettes; neutral surfaces must remain neutral.
- Ensure WCAG AA contrast for text.

Controls:

- Use icon buttons for settings, quit, refresh, warning details, and link opening.
- Use text buttons for context switches because labels matter.
- Use toggles for binary settings.
- Use segmented controls for theme mode.
- Use text fields for app names, paths, profile directories, and URLs.
- Use list rows with add/remove buttons for app/browser/docker arrays.
- Use tooltips for icons.

States:

- Idle.
- Switching.
- Success.
- Warning.
- Failed.
- Config invalid.
- Permissions required.

The dashboard must never resize dramatically during a switch. Reserve stable space for progress and warnings.

### 11.3 Menu Bar Icon

Icon states:

- Neutral: no active context or config invalid.
- Work: work icon/accent.
- Personal: personal icon/accent.
- Switching: subtle progress indicator if supported.
- Warning: badge or alternate icon.

Rules:

- Menu bar label should be optional and short.
- Default should be icon-only to avoid menu bar clutter.
- Do not show Dock icon unless `showDockIcon == true` or a debug flag is active.

## 12. Error Handling Protocols

Every operation must produce structured errors that can be shown in the dashboard and written to logs.

Error classes:

- `ConfigurationError`: invalid config, unsupported schema, unknown context.
- `PermissionError`: macOS Automation, Accessibility, Shortcuts, file access.
- `ProcessExecutionError`: non-zero exit, timeout, missing executable.
- `ScriptError`: AppleScript compilation/runtime failure.
- `PersistenceError`: failed read/write, corrupt JSON.
- `ValidationWarning`: missing optional app/path/container.

User-facing error style:

- Plain language.
- Include the failed action.
- Include the likely permission or configuration fix.
- Avoid stack traces in UI.
- Offer docs link where available.

Developer log style:

- Include correlation ID.
- Include step ID.
- Include exit code.
- Include trimmed stderr/stdout.
- Include exception type and stack trace only in logs.

Retry behavior:

- User may retry the whole switch.
- Individual failed step retry can be added after MVP.
- Do not auto-repeat failed system automation in a loop.

Timeout defaults:

| Step                   | Timeout            |
| ---------------------- | ------------------ |
| Quit app               | 10 seconds per app |
| Launch app             | 15 seconds per app |
| Browser profile launch | 20 seconds         |
| Theme switch           | 5 seconds          |
| Wallpaper switch       | 10 seconds         |
| Focus shortcut         | 10 seconds         |
| Media control          | 8 seconds          |
| Docker stop/start      | 45 seconds         |
| State write            | 5 seconds          |

## 13. Security and Privacy

Privacy guarantees:

- No telemetry.
- No background network calls except user-configured URLs and optional donation/license validation if implemented.
- No collection of browser history, active windows, keystrokes, clipboard, screenshots, or file contents.
- Analytics track only selected context and time interval.

Command safety:

- Use allowlisted built-in automation commands for MVP.
- Treat user-supplied paths, app names, profile names, container names, and URLs as data.
- Validate URL schemes.
- Validate Docker resource names with conservative pattern: `^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}$`.
- Escape AppleScript string literals through a single tested function.
- Do not run arbitrary shell snippets from config in MVP.

Permissions:

- The app may require macOS Automation permission for System Events, Music, Spotify, and app control.
- Document Security & Privacy prompts clearly.
- Detect common permission failures and show remediation.

## 14. MVP Execution Roadmap

### Phase 0: Project Foundation

Deliverables:

- Solution and project structure.
- `.editorconfig`, `.gitignore`, `global.json`.
- Avalonia app booting as a menu bar utility.
- DI root configured.
- Local config directory creation.
- Basic structured logger.

Acceptance criteria:

- `dotnet build` succeeds on Apple Silicon.
- App launches without Dock icon.
- Dashboard opens from menu bar icon.
- Missing config creates valid default config.

### Phase 1: Configuration and State

Deliverables:

- `settings.json` models.
- `state.json` models.
- Atomic JSON store.
- Validation service.
- Corrupt JSON recovery.
- Unit tests for validation and persistence.

Acceptance criteria:

- Invalid context IDs are rejected.
- Corrupt config is preserved and default config is recreated.
- User receives readable config error in dashboard.

### Phase 2: Core Switch Engine

Deliverables:

- `ContextSwitchService`.
- Switch lock.
- Automation plan builder.
- Structured step results.
- Analytics session boundary.
- Dry run support.

Acceptance criteria:

- Switching to same context returns `NoOp`.
- Failed critical step returns `Failed`.
- Failed non-critical step returns `SucceededWithWarnings`.
- Tests cover pipeline ordering.

### Phase 3: AppleScript and Process Automation

Deliverables:

- `ProcessRunner`.
- `AppleScriptRunner`.
- Graceful app quit.
- App launch.
- Theme switching.
- Wallpaper switching.
- Unit tests for argument generation and AppleScript escaping.

Acceptance criteria:

- App can close and launch configured apps.
- Theme can toggle light/dark where permissions allow.
- Missing app produces warning, not crash.

### Phase 4: Browser Management

Deliverables:

- Default browser URL launcher.
- Chrome, Brave, and Safari URL/tab group automation.
- Optional Chrome and Brave profile-directory argument support.
- URL validation.
- UI fields for browser management mode, URLs, tab groups, and optional profiles.

Acceptance criteria:

- Work and personal contexts can use URL, tab group, or profile workflows independently.
- Invalid URL is rejected during validation.
- Browser launch failures are visible in dashboard.

### Phase 5: Global Hotkeys and CLI

Deliverables:

- `SharpHotkeyService`.
- Hotkey registration and conflict reporting.
- CLI command router.
- `switch`, `status`, `list-contexts`, `validate-config`.
- JSON output mode.

Acceptance criteria:

- Hotkey switches context from another app.
- CLI switches context from Terminal.
- Shortcuts can call CLI entrypoint.

### Phase 6: Dashboard V1

Deliverables:

- Context status header.
- Switch buttons.
- Progress area.
- Last warnings/errors.
- Quick links.
- Notes.
- Settings link.

Acceptance criteria:

- Dashboard remains responsive during switching.
- Buttons disabled while switch is in progress.
- Current context and elapsed time update correctly.

### Phase 7: Docker, Media, Focus

Deliverables:

- Docker start/stop.
- Apple Music control.
- Spotify best-effort control.
- Shortcuts-based Focus mode integration.
- Permissions documentation.

Acceptance criteria:

- Docker containers can stop when leaving work.
- Focus shortcut failure is actionable.
- Media failures do not break switching.

### Phase 8: Analytics and Charts

Deliverables:

- Append-only `analytics.jsonl`.
- Daily/weekly balance summaries.
- LiveCharts2 dashboard visualization.
- Retention cleanup.

Acceptance criteria:

- Chart renders local context time.
- Crash recovery closes stale session.
- Retention setting prunes old sessions only after successful backup or safe read.

### Phase 9: Packaging and Release

Deliverables:

- GitHub Actions CI.
- GitHub Actions release workflow.
- `.app` bundle publish for `osx-arm64`.
- `.dmg` packaging script.
- README installation docs including quarantine workaround.

Acceptance criteria:

- Tag `vX.Y.Z` creates GitHub Release.
- Release contains `.dmg`.
- Fresh install can launch after documented quarantine command.

### Phase 10: Cosmetic Donation Unlocks

Deliverables:

- Polar.sh donation link.
- Local `license.json`.
- Cosmetic-only theme/icon unlock.
- No functionality locked behind donation.

Acceptance criteria:

- App is fully functional without license.
- Invalid license never blocks context switching.
- Donation copy is honest and unobtrusive.

## 15. CI/CD Specification

### 15.1 CI Workflow

`.github/workflows/ci.yml`:

- Trigger on pull request and push to `main`.
- Runner: `macos-latest`.
- Steps:
    1. Checkout.
    2. Setup .NET 10.
    3. Restore.
    4. Build release.
    5. Run tests.
    6. Publish test results if configured.

### 15.2 Release Workflow

`.github/workflows/release.yml`:

- Trigger on tags matching `v*.*.*`.
- Runner: `macos-latest`.
- Steps:
    1. Checkout.
    2. Setup .NET 10.
    3. Restore.
    4. Test.
    5. Publish:

```text
dotnet publish src/ContextSwitcher.App/ContextSwitcher.App.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:UseAppHost=true
```

6. Build `.app` bundle.
7. Create `.dmg`.
8. Upload release asset.

DMG naming:

```text
ContextSwitcher-VERSION-osx-arm64.dmg
```

Quarantine documentation:

```text
xattr -cr /Applications/ContextSwitcher.app
```

README must include a prominent unsigned-app section:

1. Drag `ContextSwitcher.app` to `/Applications`.
2. Run `xattr -cr /Applications/ContextSwitcher.app`.
3. Open the app from Finder or Spotlight.

## 16. Testing Strategy

Required test layers:

- Unit tests for domain logic.
- Unit tests for config validation.
- Unit tests for command argument generation.
- Unit tests for AppleScript escaping.
- Unit tests for analytics session calculation.
- Integration-style tests for JSON store using temp directories.

Do not run real destructive system automation in automated tests.

Test doubles:

- `FakeClock`.
- `FakeProcessRunner`.
- `FakeScriptRunner`.
- `InMemoryJsonStore`.
- `TestLogger`.

Critical test cases:

- Switching to same context returns no-op.
- Concurrent switch is rejected.
- Critical step failure fails switch.
- Non-critical step failure produces warnings.
- State write failure fails switch.
- Analytics write failure warns.
- Browser profile arguments preserve spaces.
- AppleScript escaping handles quotes and backslashes.
- Corrupt JSON recovery preserves original file.
- Docker names reject shell metacharacters.

Manual QA checklist:

- Fresh launch with no config.
- Switch Work from dashboard.
- Switch Personal from dashboard.
- Switch Work from hotkey.
- Switch Personal from CLI.
- Close app and relaunch.
- Corrupt `settings.json`.
- Remove wallpaper file.
- Uninstall Docker or make Docker unavailable.
- Deny Automation permission.
- Run from unsigned `.dmg`.

## 17. Coding Standards

C#:

- Enable nullable reference types.
- Treat warnings as errors once foundation stabilizes.
- Prefer records for immutable domain data.
- Prefer small services with explicit dependencies.
- Use `CancellationToken` on async operations.
- Avoid static mutable state.
- Avoid service locator patterns outside startup composition.
- Avoid broad `catch` unless converting to structured result with logs.
- Add concise XML documentation comments for public types, public methods, and non-trivial service contracts.
- Use inline comments sparingly for non-obvious automation, validation, and persistence logic, and explain intent or invariants rather than restating the code.
- Keep comments accurate as the implementation evolves; update or remove stale documentation when behavior changes.

Serialization:

- Use `System.Text.Json`.
- Use explicit converters only when needed.
- Keep schema migrations deterministic and tested.

Async:

- UI calls must not block the Avalonia UI thread.
- Long-running switch operations must be awaited asynchronously.
- View models expose observable state for progress.

Naming:

- Context IDs: lowercase kebab-case or simple lowercase words.
- Step IDs: `{type}.{contextId}.{resourceName}` where practical.
- Logs: stable `eventId` values in PascalCase.

## 18. Documentation Requirements

README must contain:

- What the app does.
- Screenshot or short animated demo after UI exists.
- Installation steps.
- Unsigned app quarantine workaround.
- Example `settings.json`.
- Shortcuts/Siri setup.
- Automation permissions setup.
- Privacy statement.
- Donation explanation.
- Development setup.

`docs/configuration.md`:

- Full configuration schema.
- Examples for work/personal contexts.
- Browser profile notes.
- Docker notes.

`docs/automation-permissions.md`:

- macOS Automation permission explanation.
- System Events permission.
- Music/Spotify automation permission.
- Troubleshooting common failures.

`docs/shortcuts-integration.md`:

- How to create Focus shortcuts.
- How to create Siri phrases.
- CLI command examples.

`docs/release-process.md`:

- Tagging convention.
- GitHub Actions release flow.
- DMG creation.
- Quarantine note.

## 19. Monetization and Licensing

Business model:

- MIT open source.
- Optional "Buy me a beer" donation via Polar.sh.
- Polar acts as Merchant of Record.
- Donation can generate a cosmetic license key.

License file:

```json
{
    "schemaVersion": 1,
    "licenseKeyHash": "sha256:...",
    "status": "CosmeticUnlocked",
    "unlockedAt": "2026-07-12T10:00:00Z",
    "features": ["theme.midnight", "icon.gradient"]
}
```

Rules:

- Never block context switching behind donation.
- Never nag during switching.
- Network license validation must be optional and fail open for cosmetics already unlocked.
- Do not store raw license keys in logs.
- Do not build subscription logic into MVP.

## 20. Definition of Done

A feature is done only when:

- It is wired through DI.
- It has structured logging.
- It has user-visible error handling.
- It is covered by focused tests proportional to risk.
- It respects local-only privacy requirements.
- It does not block the UI thread.
- It works with spaces in paths, app names, and profile directories.
- It is documented if user setup is required.
- It includes inline documentation for public APIs and non-obvious implementation details.
- It does not introduce unrelated dependencies or architecture drift.

## 21. Agent Operating Rules

When implementing this project:

- Read existing code before editing.
- Preserve the architecture boundaries in this file.
- Prefer small, complete increments over broad partial rewrites.
- Keep platform-specific behavior isolated in `Infrastructure`.
- Add tests with behavior changes.
- Do not silently weaken privacy, local persistence, or open-source guarantees.
- Do not introduce arbitrary shell command execution in config for MVP.
- Update this file only when the product or architecture decision truly changes.

The first production milestone is not a full automation universe. It is a reliable, native-feeling macOS menu bar app that can safely switch between two configured contexts using C# core orchestration, AppleScript/process automation, flexible browser management, local state, and clear recovery when macOS permissions get in the way.
