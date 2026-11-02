# How ContextSwitcher is built

The contracts the code keeps: how it is put together, what it writes to disk, how a switch runs, what
each step does, and the rules every change follows. Comments in the code point here by section name.
What each setting means is in [Configuration](configuration.md); what Windows changes, in
[the Windows plan](windows-plan.md).

"Profile" in the interface and "context" in the code are the same thing: a `ContextDefinition`.

## Projects

```text
src/
  ContextSwitcher.Core                    profiles, switching, time, logging, messages, the interfaces
  ContextSwitcher.Infrastructure          shared: files, the log, GitHub, the command line, the step handlers
  ContextSwitcher.Infrastructure.MacOS    the Mac's: AppleScript, the Objective-C calls, open, sips, shortcuts
  ContextSwitcher.Infrastructure.Windows  Windows': the shell so far; stand-ins in NotYet/ for the rest
  ContextSwitcher.App                     the Avalonia interface, the composition root, view models
  ContextSwitcher.Tests                   every test
  ContextSwitcher.Tests.Windows           the Windows code's tests, which need its target
```

- **Core** references nothing of the app's: no Avalonia, no platform API, no files, processes or wall
  clock. Every side effect goes through an interface in `Core/Abstractions`. Records in an interface's
  signature (`ProcessResult`, `ProcessStartOptions`) live in Core too, so the interface compiles on its
  own. Every model serialises with `System.Text.Json`, and every switch operation returns a structured
  result, not a bare boolean.
- **Infrastructure** references Core. The platform projects reference both and are marked for their
  system (`[assembly: SupportedOSPlatform]`), so the compiler flags shared code that reaches into them.
  Neither checks which system it runs on: only that system's app registers it.
- **App** targets `net10.0` (macOS) and `net10.0-windows10.0.19041.0`, and references one platform
  project in each. `Directory.Build.props` sets `EnableWindowsTargeting`, so any machine builds both.
- **Tests** run on both systems; a test of macOS itself is a `[MacOSFact]`, of Windows a
  `[WindowsFact]`. The Windows project's own code is tested in `ContextSwitcher.Tests.Windows`, which
  needs its target: built everywhere, run only on Windows.

## Design rules

The code is meant to stay easy to change without rewriting half of it or moving logic that should
stay put.

1. Behaviour that varies - by platform or by policy - sits behind an interface in
   `Core/Abstractions`, is implemented in Infrastructure or a platform project, and is given through
   constructors.
2. No `OperatingSystem.Is…()` checks inside classes. Platform and policy are chosen once, in the
   composition root.
3. No constructor parameter that defaults to a concrete implementation, or an interface to `null`.
4. No `new` of services inside classes, and no service locator outside the composition root.
5. Policies are data with named presets - waits, limits and lists, as `FileReplacer.Once` and
   `FileReplacer.RetryingWhileHeldOpen`, `StepBudgets.Default` and `MacPlatform.Capabilities` are.
6. One responsibility per class.
7. View models depend on abstractions, not Infrastructure; no processes, files or system calls in
   them.
8. No static state or static helpers that make testing or change harder; no duplication; clear names.

These look like breaks of the rules and are not:

- **`App` and `Program` read `AppHost.Services`.** Avalonia creates `App`, and `Program` is the entry
  that builds the container.
- **Views give view models what belongs to their window**, such as the file picker and the clipboard,
  which need the window's `TopLevel`.
- **Parent view models build their children** with explicit dependencies; rows hold no services.
- **`new DispatcherTimer()` in a view model** is interface glue, not a service.
- **`SwitchCoordinator` builds the ask-on-switch view model** and hands it to an
  `IAskOnSwitchPresenter`, which is the seam.
- **`AppLog`** where no `ILogger` can be given: callbacks from macOS, static helpers, the crash
  handlers.
- **Pure static helpers** - `PageMatch`, `SpotifyLink`, `ReleaseVersion`, `AppleScriptBuilder`, the
  message templates, `ContextSwitcherJson`, `Localized`, `Plurals` - hold no state and need no seam.
- **`AppLanguages.Current`**: the language is the process's culture, chosen once before any window.
- **Values given for tests**, such as a home folder or a path, are data, not services.

Before a pull request:

- Does a new class that varies by platform or policy have an interface in `Core/Abstractions`, with
  the choice made in the registrations?
- Does any constructor default an interface to `null` or to a class?
- Does a view model use anything from Infrastructure, `File`, `Process` or a static of `AppHost`?
- Is a wait, a limit or a list a value with a named preset rather than a constant in the class?
- Does the change copy something that already exists?

## The composition root

- **`AppHost`** (`App/Startup`) builds the container, runs the start-up sequence and starts what runs
  while the app is up. Its only public state is `Services`.
- **`AppServices.Register`** lists every service and its implementation. What differs between the
  systems is each platform project's `PlatformServices.Register` - the same name in the same namespace
  in both - which `AppServices` calls; the build decides which one exists. No `#if` in shared code.
- **What the platform can do** is `PlatformCapabilities` (pages, music, Docker, Focus, the Dock, the
  Automation permission, which browsers…), registered by the platform. Profile Setup and Settings
  hide what it lacks and `AutomationPlanBuilder` plans no step for it. The commands each platform may
  run are its `CommandAllowlist`. What a platform will have but doesn't yet is a stand-in that says
  so: Windows' are in `Infrastructure.Windows/NotYet/`, each gone with the stage that brings its part.
- **`AppStartup`** runs before the first window, or before a command: tidies the log, loads the
  settings - or writes a default, moving a file that can't be read aside - validates them, loads the
  state and publishes both in `IAppState`. The app, not the command line, then recovers time history a
  crash left, prunes what is past keeping, and starts counting time for the active profile.
- **`IHousekeeping`** services are what keeps running - timers and watchers. `AppHost` starts them
  once Avalonia is set up, since making a `DispatcherTimer` earlier claims the UI dispatcher for the
  wrong thread. The command line starts none.
- **`IWarmUp`** is something a platform reads once as the app starts, so that a question asked later
  that can't wait for a slow answer already has it: Windows lists the Start menu and draws the apps' icons,
  which takes a few seconds, for the synchronous questions of which apps are open and how to hide one,
  and for a picker that opens at once. `WarmUpRunner`
  (a housekeeping service) runs them in the background, each apart, and logs one that fails. The Mac
  registers none.
- **`IAppsMap` and `IAppIdentityCatalog`** are the `apps` map of the settings, from the two sides. A platform reads it (`IAppsMap`,
  from the settings as they are now) where it looks an app up by name, and says what it knows of an app to be
  written into it (`IAppIdentityCatalog`). `AppsMapRecorder` (a housekeeping service) adds the entries that are
  missing, as the app starts and when the settings change, through `ConfigurationStore`, and never replaces
  one; a catalog with nothing to say - the Mac's - writes nothing. `AppsMap` is the pure part: which apps the
  profiles name, and merging what was found.
- **`IAppState`** holds the settings, their validation and the state, and raises
  `ConfigurationChanged` and `StateChanged`. Long-lived view models listen, so a change made in one
  window, or a switch from anywhere, shows in every window without a restart.
- **`ConfigurationStore`** is the one way the windows save `settings.json`: validate, back up the old
  file, write atomically, publish in `IAppState`.
- **`CliCommandRouter.IsHeadless(args)`** decides at start whether a command runs without the
  interface.
- **One copy** is `ISingleInstance`, which `Program` asks before anything starts. macOS keeps to one
  copy itself and sends a Reopen; on Windows a second copy hands its arguments to the first through a
  pipe and ends - one copy for each settings folder, so a development build runs beside an installed
  one.

Lifetimes: singletons for services, stores and app-wide state; view models transient unless they own
state that must outlive their window. `DashboardViewModel` and `MainAppViewModel` are the only view
models in the container; `MainAppViewModel` builds its pages. No scoped services.

## Files

Everything is kept in one folder:

- macOS: `~/.config/ContextSwitcher/`
- Windows: `%APPDATA%\ContextSwitcher\`
- A Debug build (`IAppInfo.IsDevelopment`) uses `ContextSwitcher-dev` beside it, so working on the
  app never touches the installed copy's data, and calls itself "Context Switcher DEV".
- `CONTEXTSWITCHER_CONFIG_DIR` overrides all of them (`ConfigPaths.ResolveBaseDirectory`).

```text
settings.json           the profiles and settings
state.json              which profile is on, and how the last switch went
state.json.lock         held during a switch, so two processes never switch at once
opened-tabs.json        the tabs a profile opened, to close on leaving it
analytics.jsonl         time history, one line per session
analytics.current.json  the session in progress
analytics.alive         the last sign of life, for crash recovery
app.log.jsonl           the log, and app.log.jsonl.1 before it
icon-cache/             app and site icons
backups/                settings.json before each save from the app, the newest ten
```

- UTF-8 JSON. Whole files are written atomically: a temporary file, then a move over the old one,
  through `IFileReplacer` (on Windows it tries again while another process holds the file).
- `.jsonl` files are only appended to.
- A file that can't be read is renamed `<name>.corrupt.<timestamp>` and a default written; nothing
  the person made is deleted.

### settings.json

Every field is in [Configuration](configuration.md). What the code relies on:

- `schemaVersion` must be one the app reads. A change to the schema is a breaking change: people edit
  this file by hand, so it needs a migration and a release note.
- `contexts[].id` is unique, lowercase, URL-safe and never changes; `activeContextId` names one.
- `launchApps` are opened when the profile is **entered**; `closeApps` quit and `hideApps` hidden when
  it is **left**. All three belong to the profile that owns the apps - Profile Setup shows them as one
  row per app with its toggles. The optional `apps` map ties a name to how the system knows the app
  (on Windows, its AppUserModelID and executable); old files without it read unchanged.
- `switchPolicy.criticalSteps[]` are `AutomationStepType` names, exactly; criticality is per kind of
  step, not per app.
- `browser_management.mode` is `urls`, `profiles` or `none`; a file saying `groups` is read as `urls`
  when it has `urls` or `close_urls`, otherwise `none`. Profiles are Chrome and Brave only. Addresses
  are `http` or `https`.
- A missing app, path or container is not an error in the file; the switch reports it as a warning.

### state.json

Which profile is on (`currentContextId`, `previousContextId`), when the last switch started and
finished, its status, and its problems. Written by every switch, from whichever process ran it; the
app watches the file (`StateFileWatcher`) to notice switches made from the command line.

### Time history

`analytics.jsonl` has one line per finished session: its profile, start, end, duration and why it
ended (`Switch`, `AppShutdown`, `RecoveredAfterCrash`, `Sleep`, `Away`).

- A session starts when the app starts with a profile on, and at each switch; the previous one is
  closed first.
- No time counts while the computer sleeps, the screen is locked or another user is in front.
- The session in progress is in `analytics.current.json`, written when it starts and deleted when it
  ends. Still there at the next start means a crash: the session is appended with
  `RecoveredAfterCrash`, ending at the last sign of life (`analytics.alive`, every couple of minutes)
  rather than at the next launch. The history itself is never rewritten.
- Nothing else is tracked: no window titles, addresses, keystrokes or use of apps.

### The log

`app.log.jsonl`, one JSON object a line: time, level, category, event id, message, profile,
correlation id, data. What reaches it (`TrailLogger`):

- The log is quiet. Below `Warning`, entries are remembered in memory, the last 300, and not written.
- A `Warning` or above is written with the trail that led to it: the run's header (version, system,
  architecture), every entry of its own switch (by `correlationId`), and up to 20 recent entries that
  belong to no switch, marked `"trail": "true"`. A switch with a problem is written to its end.
- The same category, event and message is written once an hour; the next says how many it stood in
  for (`"repeated"`).
- An entry with `Keep` is written whatever its level, for a rare event that is no problem.
  Settings → Detailed log, or `CONTEXTSWITCHER_LOG_LEVEL` (`information`, `debug`, `trace`), writes
  everything from a level on.
- The file starts again after a week, keeping the old one as `app.log.jsonl.1` for another, and rolls
  at 1 MB.
- `LogRedactor` runs on every entry before it is remembered: web addresses are cut to their site,
  email addresses become `<email>`, the home folder becomes `~`, and no command line with a token is
  logged.
- A crash on any thread, and a task nobody awaited, are written with the trail before the process
  ends.
- No exception is swallowed without a trace: a `catch` that does nothing or only returns either logs,
  or has a comment starting `Not logged:` with the reason. `SilentCatchTests` fails on one that does
  neither.
- Levels: `Information` for a switch's start, plan and finish, step results and start-up; `Warning`
  for a step that failed and was recovered from; `Error` for a failed switch, a broken settings file or
  a refused permission; `Critical` for a crash.

## Switching

`ContextSwitchService` (Core) runs a switch: a `ContextSwitchRequest` in (target, source, dry run,
force, correlation id, and what else to close), a `ContextSwitchResult` out (status, each step's
`AutomationResult`, times). Every switch started in the app goes through `SwitchCoordinator`, which
asks about everything else first and publishes the state after; the command line calls the service
directly.

### The order of a switch

1. Check the target exists and the settings are valid.
2. Take the switch lock - in the process, and `state.json.lock` across processes. A second switch
   while one runs is turned away (`Cancelled`; exit code 6 from the command line).
3. Log `SwitchStarted` with a correlation id.
4. A switch to the profile already on, without `Force`, returns `NoOp`.
5. Close the time session.
6. Build the plan (`AutomationPlanBuilder`) from the profile being left and the one entered.
7. Free what the old profile held: stop its Docker containers (not ones the new one starts), close
   its pages' tabs, close the tabs and apps chosen when asked, quit its `closeApps`, hide its
   `hideApps`. On the first switch there is nothing to leave.
8. Focus: turn on the new profile's, or off the old one's.
9. Bring in the new profile: launch its apps, open its pages or browser profiles, start its
   containers, play its music.
10. Write `state.json`.
11. Start the time session for the new profile.
12. Log `SwitchCompleted` or `SwitchFailed`; the windows and the menu bar icon follow `IAppState`.

Turning a profile off is the first half alone. Apps in `ignoredApps` are left out of every step.

### When a step fails

- A critical step that fails makes the switch `Failed`; another makes it `SucceededWithWarnings`.
- Failing to write `state.json` makes it `Failed`, and a switch never claims the new profile is on
  unless that write worked. Failing to write the time history is a warning, shown like any other.
- Each step has a time budget (`StepBudgets`): 10 s for each app quit, 15 s for each app launched, 5 s
  to hide, 20 s for the browser, 10 s to close tabs, 10 s for Focus, 8 s for music, 45 s for Docker.
- Cancelling stops the steps not yet started; a running `osascript` is not cut off before its
  budget. `state.json` is left as it was unless the switch had already committed.
- Nothing is retried in a loop. The person can run the switch again.
- An app still closing when the switch ends is watched for a couple of minutes, and its warning
  follows what it does (`ClosingAppsWatcher`).

### Asking about everything else

With `askOnSwitch` on, `SwitchCoordinator`:

- finds the apps (from `IRunningApplications`, not Finder or ContextSwitcher itself) and tabs
  (`ISystemCatalog`, no incognito windows) that neither profile opens, closes, hides or lists, and
  that aren't in `ignoredApps` (`UnclaimedItems`);
- asks in `AskOnSwitchWindow`: keep, close, or add to either profile. Over 13 tabs become one
  keep-or-close line; Skip and closing the window touch nothing. Site icons come from the site
  (`SiteIcons`: `/favicon.ico`, else the page's `<link rel="icon">`; 2 s and 100 KB a request, no
  cookies; cached as a 32×32 PNG, a site with none asked again after a week), loaded after the window
  opens;
- saves what was added before the switch reads the settings, and passes what to close as
  `ContextSwitchRequest.Extras`.

## Steps on macOS

`AutomationStepExecutor` hands each step to the `IStepHandler` for its `AutomationStepType`. A
handler reads its arguments through the record for its type (`AppsArguments`,
`BrowserContextArguments`, `FocusArguments`…, written by the plan builder) and asks the system through
Core interfaces - `IAppLauncher`, `IAppQuitter`, `IApplicationHider`, `IMediaPlayer`,
`IShortcutRunner`, `IBrowserLauncher` over `IBrowserControl`. A new kind of step is a new handler and
a registration. On macOS:

- Every command goes through `IProcessRunner` with an argument array, never a shell string; every
  AppleScript through `IScriptRunner`, built by `AppleScriptBuilder` with escaped literals.
- `IProcessRunner` captures stdout and stderr, kills the process tree on timeout, and returns a
  result rather than throwing for a non-zero exit.

### Quitting apps

`tell application "Slack" to if it is running then quit` - as ⌘Q, so apps keep their state - then
checks it went, within the step's 10 s an app. One still running is a warning, never forced:
**Force quit** is the person's click. No `killall`.

### Launching apps

`open -a "Visual Studio Code"`, one at a time. A missing app is a warning unless the step is
critical; failure is told from the exit code and stderr.

### Browser pages

- `open URL` for the default browser; `open -a "Google Chrome" URL` (or Brave, or Safari) for a chosen
  one. Addresses are checked before the switch.
- With `avoid_duplicate_tabs`, the browser's tabs are listed once and a tab already showing the page
  (`PageMatch`: same site, at the page's path or below) is brought forward instead. If the browser
  can't be asked - permission, scripting - that is a warning, and the page opens with `open`.
- Leaving a profile closes its `close_urls` pages, as their own step before apps are quit, so a
  browser that is quit too doesn't bring them back. Only with Chrome, Brave or Safari (or the default
  browser when it is one of them), and only when it is running: scripting a browser starts it. The
  tabs are chosen in C# and closed exactly - in Chrome and Brave by tab id, in Safari by address. In
  Chrome and Brave the tabs the profile opened are remembered by id in `opened-tabs.json`, with the
  browser run they belong to, and closed wherever they have navigated since. A page the profile
  being entered opens in the same browser is spared. No other tab is ever closed.
- Only configured addresses are compared; no history is read or kept.
- Tab groups can't be scripted in any browser; [browser control](browser-control.md) has what each
  allows. For Safari, Profile Setup points to a Focus filter.

### Browser profiles

`open -na "Google Chrome" --args --profile-directory="Profile 1" URL…` (or `"Brave Browser"`), with
the profile directory exactly as configured. Chrome and Brave only - Safari has no such switch -
and every row uses the profile's chosen browser. Existing tabs are not looked at.

### Focus

`shortcuts run "ContextSwitcher - Focus Work"`, one Shortcut a mode, which the app can install;
[Focus and Siri](shortcuts-integration.md) explains them. The step is planned only when Focus is in
play - the profile entered turns one on, or the one left had one to turn off - so someone who never
set Focus up never sees a warning. A missing or failing Shortcut is a warning that says what to do.
No private API.

### Apple Music

`tell application "Music" to play playlist "Deep Focus"`. A missing playlist is a warning; with
`autoPlay` off, nothing is played.

### Spotify

`play track "spotify:playlist:…"` through AppleScript, from a link or URI (`SpotifyLink`); a plain
name is best effort. Spotify failing is never critical.

### Docker

`docker stop a b` and `docker start a b`, with stderr kept - Docker tells its state there. No Docker
is a warning; an empty list is no step. Never `docker compose down`. Names must match
`^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}$`.

### Opening links

Quick links open with `open URL`, `http` and `https` only. A profile's own pages go through its
browser settings instead.

## The command line

```text
ContextSwitcher switch --context work [--dry-run] [--json]
ContextSwitcher off
ContextSwitcher status
ContextSwitcher list-contexts
ContextSwitcher validate-config
ContextSwitcher open-dashboard
```

Text by default; `--json` prints only valid JSON. Each switch runs in its own process, which is why
the switch lock and the log guard across processes. Exit codes (`CliExitCode`):

| Code | Meaning |
| --- | --- |
| 0 | Success |
| 1 | General failure |
| 2 | Invalid arguments |
| 3 | Unknown profile |
| 4 | Settings invalid |
| 5 | Switched, with warnings |
| 6 | Another switch is running |

On macOS the command line is the app's own executable, which Shortcuts and Siri can run
([Focus and Siri](shortcuts-integration.md)). On Windows it will be a console program of its own.

## The interface

### The popover

Opens from the menu bar icon, under it: the profile on, with its colour and time; a button to switch
to each of the others; a switch's progress and the last one's problems; the profile's quick links and
notes; the last seven days; **Open App** and Support the developer. Esc or a click elsewhere closes
it. `IPopoverWindow` places it - under the menu bar on a Mac, beside the taskbar on Windows, growing
upwards from one along the bottom (`PopoverPlacement`). On macOS it also makes it a non-activating
panel on the Space in front, and tells it when to close; on Windows it is an ordinary topmost window
that closes when it loses the focus. It never jumps in size during a switch: progress and warnings
have their room.

### The main window

Opened from the popover, the menu bar menu, or by opening the app again while it runs (macOS hides
menu bar icons that don't fit beside the notch, so this is the way in that always works). A page list
on the left:

- **Profiles** - every profile; a click switches to it (on the profile already on, turns it off), and
  Edit opens Profile Setup, never the same click. Add, duplicate, delete. Problems from the last switch,
  from anywhere, sit in a card at the bottom until closed or the next switch.
- **Profile Setup** - every field of a profile: name, label, icon and colour; apps, one row each with
  Launch, Close and Hide; the browser and its pages or profiles; Focus, music, Docker; quick links and
  notes; which steps must work. Each part is a view model of its own.
- **Settings** - language, theme, the Dock icon and open at login; the Automation permission;
  asking about everything else and the apps never touched; time tracking; updates; export and import;
  the detailed log and diagnostics; contact; about.
- **Stats** - the balance chart over a week or a month, by profile.

Every setting has a place here; nobody needs to edit `settings.json` by hand. Besides these, the setup
wizard runs on first launch, and the ask-on-switch window during a switch.

### Look and feel

- **Semi.Avalonia** draws the standard controls; `Styles/` only adds the app's tokens and classes.
  `Colors.axaml` is the whole palette, each colour with a light and a dark value: neutrals for
  surfaces, text and lines, one accent (`AccentBrush`, the system blue), and three status colours
  with soft tints. Anything new picks from these; profile colours are the person's own.
- Light and dark follow the system unless Settings → Theme pins one.
- Cards on a page, rows in a card separated by a line - no cards inside cards. Label left, control
  right. Primary actions are filled with the accent; text actions look like links.
- Icons are [Lucide](https://lucide.dev) geometry in `Icons.axaml`, drawn as strokes - no icon font
  and no package.
- The system font, no letter spacing, fixed sizes.
- No blur behind windows: asked for `Blur` on the borderless popover, macOS gives `Transparent`
  (checked with `ActualTransparencyLevel` under Avalonia 12). Depth comes from surfaces and the
  window's shadow.
- Toggles for on and off, combo boxes for choices, rows with add and remove for lists, tooltips on
  icon buttons, text buttons where the label matters - as for switching.

### The menu bar icon

On a Mac, one template image, which macOS recolours for light and dark. Windows doesn't recolour
tray icons, so there `ITrayIconLook` picks a dark or a light drawing for the taskbar, and changes it
when the taskbar does. Its menu has Open Dashboard, Open App, the profiles with a tick by the one on,
an update when there is one, and Quit. The Dock icon is optional (`showDockIcon`).

## Errors

- Every operation ends in a structured result that the windows can show and the log can keep.
- The person sees plain language: what failed, and the likely fix in permissions or settings, with a
  link where there is one. No stack traces. Messages from Core and Infrastructure are
  `MessageTemplate`s, so they are translated (below).
- The log has the correlation id, step id, exit code, trimmed stdout and stderr, and the exception's
  type and stack.

## Security and privacy

- No telemetry. The only network calls are the pages the person configured, site icons when asking on
  a switch, and the update check: GitHub's list of releases, about 30 s after launch and daily, off in
  Settings. `GitHubReleaseSource` takes the newest release that carries this platform's files, not
  GitHub's "latest". Updates install only on a click, and only when the download is signed by the same
  certificate as the running copy.
- No browsing history, window titles, keystrokes, clipboard, screenshots or file contents are read.
  Time history is the profile and its times, nothing else.
- Only the commands in the platform's `CommandAllowlist` run. Nothing from the settings is run as a
  shell snippet: app names, profile names, containers and addresses are data, checked and escaped
  (AppleScript literals by one tested function).
- macOS asks for Automation per app it scripts. `IPermissionsChecker` probes it with a harmless script
  (macOS has no API to ask), and Settings shows the result with a way to fix it
  ([permissions](automation-permissions.md)).
- Free and MIT: every feature for everyone. Support the developer is a plain link, never a gate.

## Tests

- Unit tests for the domain, validation, command arguments, AppleScript escaping and time history;
  the JSON store against temporary folders; view models and pages in Avalonia's headless host.
- No test runs real system automation. Fakes and in-memory versions of every interface are in
  `Tests/TestDoubles`, with `FakeClock` for time.
- A test of macOS itself is a `[MacOSFact]`, of Windows a `[WindowsFact]`; the other system skips it.
- What can only be checked by hand is in the [release checklist](release-process.md#by-hand-on-an-installed-build).

## Text and translation

- No visible text in views or view models: it is in `Resources/Strings.resx` and used as
  `{x:Static r:Strings.Key}` or `Strings.Key`. Keys are `Area_What`, with a comment for translators.
  `ResourceTests` fails on text written into a view.
- Placeholders through `Localized.Format`; counted phrases through `Plurals.Format` (CLDR one, few,
  many, other).
- Messages made in Core and Infrastructure are `MessageTemplate`s in `Core/Messages`, producing a
  `UserMessage`; the app shows them through `MessageText.Of`, from `Msg_<key>`.
- The language is chosen once at start by `AppLanguages`: the `language` setting, or the first of
  the system's preferred languages with a translation. A language in `AppLanguages.Supported` is
  translated in full. The log, the command line and identifiers stay English.
  [Translating](translating.md) has the steps.

## Code style

- Nullable reference types on; warnings are errors.
- Records for immutable data; small services with explicit dependencies; `CancellationToken` on async
  work; nothing blocks the UI thread.
- XML comments on public types and members, and on anything not obvious: why, not what. Keep them true
  when the code changes.
- `System.Text.Json`, with converters only where needed; schema changes deterministic and tested.
- Profile ids lowercase; step ids `{type}.{profile}`; log event ids stable PascalCase.
