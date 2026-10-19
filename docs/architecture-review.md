# Architecture review

How well the code follows the rules below, what gets in the way of changing it, and the order to
clean it up in - one pull request at a time. Nothing here changes what the app does.

Written in October 2026 against `main` at `5d4cd25`: 0.6.7 with items 1, 2 and 7 of
[stage 0](windows-plan.md#stage-0---groundwork-on-the-mac) merged. Every finding was checked in that
code; line numbers are from it.

## In short

Core and most of Infrastructure already work the way the rules ask. There are 23 interfaces in
[`Core/Abstractions`](../src/ContextSwitcher.Core/Abstractions), Core references nothing else, the
services take what they need through their constructors, and the tests give them fakes of their own.
[#78](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/78) added the pattern for policies:
[`FileReplacer`](../src/ContextSwitcher.Infrastructure/Files/FileReplacer.cs) with named presets,
chosen in `AppHost`.

What makes the app hard to change is in four places:

1. **`AppHost` is a global.** The settings, the current state and their change events are static
   properties of the composition root. View models and services read and write them 85 times, and 25
   of the 95 test files have to run one at a time because of it. Two view models never let go of its
   events, so every closed main window stays in memory ([F1](#f1-the-apps-state-lives-in-apphosts-statics),
   [F3](#f3-two-view-models-are-never-disposed)).
2. **macOS is wired into shared classes.** `AutomationStepExecutor` and `BrowserLauncher` build
   AppleScript and run `open` themselves, and the executor falls back to the Mac classes when it is
   not given others. Six view models run `open`. Stage 0 item 3 already plans this
   ([F5](#f5-automationstepexecutor-does-nine-jobs-and-defaults-to-macos),
   [F6](#f6-browserlauncher-has-no-interface-and-does-five-jobs),
   [F7](#f7-six-view-models-run-open)).
3. **macOS helpers the plan doesn't list.** Sleep and lock events, the Dock icon, the preferred
   languages, waiting for a display and the popover's window tricks are static macOS classes called
   straight from the app. Windows needs each of them in stage 1
   ([F9](#f9-macos-helpers-called-straight-from-the-app)).
4. **A few classes do too much**: `AppHost`, `UpdatesViewModel`, `ProfileSetupViewModel`, and the
   two above ([F14](#f14-updatesviewmodel-is-a-service-and-a-view-model)-[F16](#f16-profile-setup-is-871-lines)).

The rest is smaller: policies written as constants, optional services, four ways of telling the
time, duplicated helpers and a few names.

## The pieces, in order

Each piece is one pull request that changes no behaviour, ranked by what it makes easier to change
against what it costs. Sizes are lines changed with their tests: **XS** under 50, **S** under 150,
**M** under 400, **L** more.

| # | Piece | Findings | Size | Stage 0 |
| --- | --- | --- | --- | --- |
| 1 | [Dispose the Settings page](#1-dispose-the-settings-page) | F3 | XS | new |
| 2 | [One place publishes the state after a switch](#2-one-place-publishes-the-state-after-a-switch) | F4 | S | new |
| 3 | [A seam for links, files and system pages](#3-a-seam-for-links-files-and-system-pages) | F7 | S | item 3 |
| 4 | [The app's state as a service](#4-the-apps-state-as-a-service) | F1, F2, F8 | L, two PRs | new |
| 5 | [Seams for apps, media and Focus](#5-seams-for-apps-media-and-focus) | F5, F13 | M | item 3 |
| 6 | [A seam for browser tabs](#6-a-seam-for-browser-tabs) | F6 | M | item 3 |
| 7 | [The macOS helpers behind interfaces](#7-the-macos-helpers-behind-interfaces) | F9, F10 | M | new, next to item 3 |
| 8 | [Policies as data](#8-policies-as-data) | F11, F12 | S + S | new; the allowlist feeds item 6 |
| 9 | [No optional services](#9-no-optional-services) | F13 | S | new; meets item 5 |
| 10 | [The app's identity and the update checker](#10-the-apps-identity-and-the-update-checker) | F14 | M | new |
| 11 | [AppHost split in three](#11-apphost-split-in-three) | F15 | M | item 6 takes the platform half |
| 12 | [Profile Setup in sections](#12-profile-setup-in-sections) | F16 | M | new; its browser rules come from item 5 |
| 13 | [The switch lock out of Core](#13-the-switch-lock-out-of-core) | F17 | S | new |
| 14 | [One image converter](#14-one-image-converter) | F19 | S | new; stage 2 swaps in SkiaSharp |
| 15 | [Small fixes](#15-small-fixes) | F18-F21 | S each | new |
| 16 | [Typed step arguments](#16-typed-step-arguments) | F22 | M | new |

Pieces 1-3 are cheap and can go in any order. Piece 4 is the one that pays most; do it before 11.
Pieces 5-7 are what Windows needs next, and 5 and 6 are item 3 of stage 0 - do them before item 6
moves the macOS code into its own project, so the move only moves files. Piece 9 and piece 12 go
with item 5 (capabilities). The rest can wait.

### 1. Dispose the Settings page

`SettingsViewModel` becomes `IDisposable`: it unsubscribes from `AppHost.ConfigurationChanged` and
disposes `IgnoredApps`. `MainAppViewModel.Dispose` disposes `Settings` as it does `Profiles` and
`Stats`. **Done when** a test opens and closes the main window twice and only one Settings page
still reacts to a saved change.

### 2. One place publishes the state after a switch

`SwitchCoordinator`, which every switch in the app goes through, reads `state.json` after the switch
and publishes it. The menu bar, the popover and the Profiles page stop doing it themselves, so they
stop needing `IJsonStore` and `ConfigPaths`; the setup wizard gets the same behaviour. **Done when**
no view model reads `state.json`, and the wizard's switch shows at once rather than through the file
watcher.

### 3. A seam for links, files and system pages

An `IShell` (or `ILinkOpener`) in Core with `OpenLink`, `ShowFile` and `OpenSystemPage`, the last
taking a name such as "automation" or "focus" rather than an `x-apple.systempreferences:` address.
`OpenLink` says whether it worked, which the mail link needs. The macOS version is today's `open`
call. The six view models take it instead of `IProcessRunner`, which they use for nothing else.
**Done when** no view model mentions `open`, `ProcessStartOptions` or `x-apple`.

### 4. The app's state as a service

An `IAppState` in the app - the configuration, its validation, the current state, `Update…` and the
two events - registered as a singleton and given to whatever reads it. It is the app's own, not
something a platform varies, so it needs no Infrastructure version. `AppHost`'s properties forward
to it until nothing uses them.

- **First PR:** the service, and the classes that are not view models move to it: `ConfigurationStore`,
  `SwitchCoordinator`, `StateFileWatcher`, `App.axaml.cs`, the CLI's start in `Program`.
- **Second PR:** the view models, and their tests build their own `IAppState` instead of setting
  `AppHost`'s. `AppHostTestCollection` goes.

**Done when** `AppHost` has no public state but `Services`, and no test is in a collection for it.

### 5. Seams for apps, media and Focus

Interfaces for launching, quitting and checking apps, for playing media, and for running a Focus
Shortcut, with today's AppleScript, `open -a` and `shortcuts run` as their macOS versions.
`AutomationStepExecutor` takes them through its constructor with no defaults, and is split so each
step type has a handler of its own (`IStepHandler`, chosen by `AutomationStepType`); what is left is a
dispatcher. **Done when** the executor has no `using …AppleScript` or `…MacOS`, no `?? new`, and
running a new step type takes a new handler and a line in `AppHost`, not an edit to the executor.

### 6. A seam for browser tabs

An interface for what a browser is asked - list its tabs, focus one, close some, open a page, open a
profile - with today's AppleScript and `open` as the macOS version. `BrowserLauncher` keeps the
decisions (which tabs to close, what was opened, what to remember) and gets an interface of its own,
so the executor no longer takes the concrete class. The browser's application name lives in one
place. **Done when** `BrowserLauncher` has no AppleScript and its tests need no fake script runner.

### 7. The macOS helpers behind interfaces

Interfaces for: the Mac being away and back (`AwayEvents`), the Dock icon (`DockIcon`), the preferred
languages (`PreferredLanguages`), waiting for a display (`DisplayAvailability`), the system's version
(`RunInfo`'s), and how the popover behaves as a window (`MenuBarPanel`, `WindowSpaces`,
`FrontmostApplication`, `OutsideClick`). Each macOS version is today's class. `AppHost` registers
them; `App`, `Program` and `DashboardWindow` are given them. **Done when** nothing in
`ContextSwitcher.App` but `AppHost` names a class in `Infrastructure.MacOS`.

### 8. Policies as data

- **Allowed commands:** `CommandAllowlist` becomes a value with a named preset, `CommandAllowlist.MacOS`,
  that `ProcessRunner` is given; `AppHost` chooses it. Windows adds its own in stage 1.
- **Timings:** the waits and budgets that are constants or test-only parameters today become small
  records with a `Default` and, for tests, an `Immediate` preset: the step budgets, quitting, tab
  probing, the closing follow-up.

**Done when** no constructor takes a `TimeSpan?` for tests, and `ProcessRunner` has no internal
constructor.

### 9. No optional services

Every service a class needs is a required parameter. Where a platform has no such thing, it gets an
implementation that does nothing and says so (`ILoginItem.Status` is already `Unavailable` for that),
or the capabilities from item 5 hide the feature. **Done when** no constructor takes an interface
with `= null`.

### 10. The app's identity and the update checker

An `IAppInfo` - the version, whether it is a development build, the display name - replaces
`UpdatesViewModel.RunningVersion`, `AppBuild` and the three calls to `RunInfo.Current`. An update
checker service takes the daily timer, the notification and the checks out of `UpdatesViewModel`,
which keeps what Settings shows. **Done when** `AppHost` reads no static of a view model.

### 11. AppHost split in three

After piece 4: the registrations (one method per platform, which item 6 moves into each platform's
project), the start-up sequence (load, validate, recover the time history - a class of its own that
can be tested), and the housekeeping that runs while the app is up (pruning, the sign of life, the
away pause, the closing follow-up) as services started in one place. **Done when** `AppHost` only
builds the container and starts the app.

### 12. Profile Setup in sections

`ProfileSetupViewModel` gets one child view model per section, as Focus and media already have: the
browser, Docker, quick links, and the look (colour and icon). What a browser can do - profiles in
Chrome and Brave only, nothing for a default browser that can't be scripted - comes from the
capabilities (item 5), not from the view model. `MainAppViewModel` is given a factory for Profile
Setup instead of keeping its six services to pass on. **Done when** no section's rules are in the
parent, and `MainAppViewModel` holds nothing only to hand it to Profile Setup.

### 13. The switch lock out of Core

The lock that keeps two processes from switching at once moves behind an `ISwitchLock` in Core,
implemented with today's file in Infrastructure. **Done when** `ContextSwitchService` touches no file.

### 14. One image converter

An `IImageConverter` ("make this a 32-pixel PNG") with `sips` as the macOS version, used by
`InstalledAppsService` and `SiteIcons`. Stage 2 gives Windows a SkiaSharp one. **Done when** `sips`
appears once.

### 15. Small fixes

Each of [F18](#f18-applog-where-a-logger-is-at-hand) to [F21](#f21-names-that-mislead) is a PR of a
few files: give a logger where one is at hand, the duplicates in one place, one clock, the names.

### 16. Typed step arguments

Each step type's arguments become a record that the plan builder writes and the handler reads,
serialized as today. **Done when** no string key is written in one class and read in another.

## The rules

What the code was measured against: clean, easy to follow, and easy to change without rewriting
half of it or moving logic that should stay put.

1. Behaviour that varies - by platform or by policy - sits behind an interface in
   `Core/Abstractions`, is implemented in Infrastructure, and is given through constructors.
2. No `OperatingSystem.Is…()` checks inside classes. Platform and policy are chosen once, in the
   composition root.
3. No constructor parameter that defaults to a concrete implementation.
4. No `new` of services inside classes, and no service locator outside the composition root.
5. Policies are data with named presets, as `FileReplacer.Once` and
   `FileReplacer.RetryingWhileHeldOpen` are.
6. One responsibility per class.
7. View models depend on abstractions, not Infrastructure; no processes, files or system calls in them.
8. No static state or static helpers that make testing or change harder; no duplication; clear names.

## Findings

Each finding says where it is, what it is, what change it makes hard today, the fix, its size, and
whether stage 0 already covers it.

### F1. The app's state lives in AppHost's statics

Rules 4 and 8.

- **Where:** [`AppHost.cs:45`](../src/ContextSwitcher.App/Startup/AppHost.cs#L45) `Configuration`,
  [`:50`](../src/ContextSwitcher.App/Startup/AppHost.cs#L50) `ConfigurationValidation`,
  [`:65`](../src/ContextSwitcher.App/Startup/AppHost.cs#L65) `State`, and the events at
  [`:71`](../src/ContextSwitcher.App/Startup/AppHost.cs#L71) and
  [`:77`](../src/ContextSwitcher.App/Startup/AppHost.cs#L77). Outside `AppHost`, `Program` and
  `App`, they are used 85 times, in 11 view models and 3 services: `ProfilesViewModel` 21,
  `DashboardViewModel` 13, `SettingsViewModel` 13, `ProfileSetupViewModel` 6, `SwitchCoordinator` 6,
  `SwitchNoticeViewModel` 6, `IgnoredAppsViewModel` 5, `SettingsDiagnosticsViewModel` 5, and up to 3
  in each of six more.
- **Why it matters:** a view model's real inputs are not in its constructor, so a reader can't see
  them and a test can't give them. [`AppHostTestCollection`](../src/ContextSwitcher.Tests/App/AppHostTestCollection.cs#L8)
  makes 25 of the 95 test files run one after another, and a test that forgets the collection races
  the others. A second window with settings of its own - a preview of an imported file, say - can't
  be built, because there is only one `Configuration`. Static events are also how F3's leak happens.
- **Fix:** [piece 4](#4-the-apps-state-as-a-service).
- **Size:** L, in two PRs. **Stage 0:** new.

### F2. The container is asked for services outside the composition root

Rule 4. Most of these are boundaries to keep, not problems.

- **Accepted:** [`App.axaml.cs`](../src/ContextSwitcher.App/App.axaml.cs#L165) (lines 165, 177, 228,
  233, 234, 241, 242, 271, 284, 301). Avalonia creates `App` itself, so it can't be given anything.
  It can shrink to one `GetRequiredService` of a shell service that holds the rest.
- **Accepted:** [`Program.cs:54`](../src/ContextSwitcher.App/Program.cs#L54), `:127` and `:129` - the
  process's entry, which calls the composition root.
- **Accepted, could be better:** [`AppHost.cs:167`](../src/ContextSwitcher.App/Startup/AppHost.cs#L167),
  a callback that finds `ClosingAppsFollowUp` when "Force quit" is clicked, because the notice is
  built before the container. A factory registration resolved in `StartRuntimeServices` would do.
- **Real, small:** [`DashboardWindow.axaml.cs:138`](../src/ContextSwitcher.App/Views/DashboardWindow.axaml.cs#L138)
  looks up an `ILogger`. `App` builds this window with `new`, so it can pass one in.
- **Fix:** pieces [4](#4-the-apps-state-as-a-service) and [7](#7-the-macos-helpers-behind-interfaces)
  take most of `App.axaml.cs`'s lookups with them. **Size:** XS for the window. **Stage 0:** new.

### F3. Two view models are never disposed

Rules 4 and 8. A bug, found by this review.

- **Where:** [`SettingsViewModel.cs:68`](../src/ContextSwitcher.App/ViewModels/SettingsViewModel.cs#L68)
  subscribes a lambda to the static `AppHost.ConfigurationChanged` and never removes it;
  `SettingsViewModel` is not `IDisposable`. Its child
  [`IgnoredAppsViewModel.cs:39`](../src/ContextSwitcher.App/ViewModels/IgnoredAppsViewModel.cs#L39)
  subscribes too and has a `Dispose` at [`:68`](../src/ContextSwitcher.App/ViewModels/IgnoredAppsViewModel.cs#L68)
  that nothing calls. [`MainAppViewModel.cs:98`](../src/ContextSwitcher.App/ViewModels/MainAppViewModel.cs#L98)
  disposes `Profiles` and `Stats` only.
- **Why it matters:** every time the main window is closed, its Settings page - with the ignored
  apps, diagnostics and export pages it holds - stays reachable from the static event, and the old
  Settings and ignored-apps pages still run their handlers on every save. If Settings was shown, its
  file picker ([`StorageProviderSettingsFilePicker`](../src/ContextSwitcher.App/Services/StorageProviderSettingsFilePicker.cs#L8))
  holds the window itself. A week of opening the window from the menu bar is that many dead windows.
- **Fix:** [piece 1](#1-dispose-the-settings-page). **Size:** XS. **Stage 0:** new.

### F4. Three places switch and then publish the state

Rules 6 and 8.

- **Where:** [`App.axaml.cs:228`](../src/ContextSwitcher.App/App.axaml.cs#L228)-237,
  [`DashboardViewModel.cs:315`](../src/ContextSwitcher.App/ViewModels/DashboardViewModel.cs#L315)-322 and
  [`ProfilesViewModel.cs:126`](../src/ContextSwitcher.App/ViewModels/ProfilesViewModel.cs#L126)-133
  each run the switch, read `state.json` and call `AppHost.UpdateState`. The setup wizard,
  [`OnboardingViewModel.cs:207`](../src/ContextSwitcher.App/ViewModels/OnboardingViewModel.cs#L207),
  doesn't, and waits for the file watcher instead.
- **Why it matters:** two view models depend on `IJsonStore` and `ConfigPaths` - an Infrastructure
  class - only for this. Changing where the state comes from after a switch, or adding a fourth place
  that switches, means finding all of them; the wizard already behaves differently.
- **Fix:** [piece 2](#2-one-place-publishes-the-state-after-a-switch). **Size:** S. **Stage 0:** new.

### F5. AutomationStepExecutor does nine jobs and defaults to macOS

Rules 1, 3 and 6.

- **Where:** [`AutomationStepExecutor.cs:69`](../src/ContextSwitcher.Infrastructure/Automation/AutomationStepExecutor.cs#L69)
  and `:70` - `hider ?? new MacApplicationHider()` and `processes ?? new MacAppProcesses()`. It takes
  the concrete `BrowserLauncher` at [`:53`](../src/ContextSwitcher.Infrastructure/Automation/AutomationStepExecutor.cs#L53).
  It quits apps with AppleScript ([`:123`](../src/ContextSwitcher.Infrastructure/Automation/AutomationStepExecutor.cs#L123),
  `:176`), launches them with `open -a` ([`:224`](../src/ContextSwitcher.Infrastructure/Automation/AutomationStepExecutor.cs#L224)),
  plays music with AppleScript (`:362`, `:377`) and runs Focus Shortcuts with `shortcuts run`
  ([`:419`](../src/ContextSwitcher.Infrastructure/Automation/AutomationStepExecutor.cs#L419)). One
  class, 545 lines, handles all nine step types.
- **Why it matters:** the Windows version of any step means editing this class and the Mac code in
  it; the defaults mean a forgotten registration quietly runs Mac code. A tenth step type grows the
  switch and the class again.
- **Fix:** [piece 5](#5-seams-for-apps-media-and-focus). **Size:** M. **Stage 0:** item 3 names the
  seams; the handler per step type is new.

### F6. BrowserLauncher has no interface and does five jobs

Rules 1 and 6.

- **Where:** [`BrowserLauncher.cs:16`](../src/ContextSwitcher.Infrastructure/Browser/BrowserLauncher.cs#L16)
  is a concrete class the executor depends on. Two of its services are optional, at
  [`:45`](../src/ContextSwitcher.Infrastructure/Browser/BrowserLauncher.cs#L45)-46. It builds
  AppleScript at [`:291`](../src/ContextSwitcher.Infrastructure/Browser/BrowserLauncher.cs#L291),
  `:400` and `:489`, and runs `open` at [`:475`](../src/ContextSwitcher.Infrastructure/Browser/BrowserLauncher.cs#L475)
  and `:513`. In 543 lines it opens pages, avoids duplicates, opens browser profiles, closes tabs, and
  remembers which tabs it opened.
- **Why it matters:** stage 2 keeps the decisions - which tabs to close, which are this profile's -
  and replaces how the browser is asked. Today the two are in the same methods, so Windows would
  copy the decisions or wrap Mac code in conditions.
- **Fix:** [piece 6](#6-a-seam-for-browser-tabs). **Size:** M. **Stage 0:** item 3.

### F7. Six view models run `open`

Rule 7.

- **Where:** [`DashboardViewModel.cs:391`](../src/ContextSwitcher.App/ViewModels/DashboardViewModel.cs#L391),
  [`FocusSettingsViewModel.cs:277`](../src/ContextSwitcher.App/ViewModels/FocusSettingsViewModel.cs#L277)
  (the Shortcuts app), [`ProfileSetupViewModel.cs:199`](../src/ContextSwitcher.App/ViewModels/ProfileSetupViewModel.cs#L199),
  [`SettingsDiagnosticsViewModel.cs:167`](../src/ContextSwitcher.App/ViewModels/SettingsDiagnosticsViewModel.cs#L167),
  `:189` and `:230` (the log in Finder, with `-R`), [`SettingsViewModel.cs:404`](../src/ContextSwitcher.App/ViewModels/SettingsViewModel.cs#L404)
  and [`UpdatesViewModel.cs:339`](../src/ContextSwitcher.App/ViewModels/UpdatesViewModel.cs#L339).
  Two hold System Settings addresses: [`SettingsViewModel.cs:21`](../src/ContextSwitcher.App/ViewModels/SettingsViewModel.cs#L21)
  and [`ProfileSetupViewModel.cs:24`](../src/ContextSwitcher.App/ViewModels/ProfileSetupViewModel.cs#L24).
- **Why it matters:** opening a link on Windows means changing six view models, and their tests
  check the arguments of a macOS command rather than "the link was opened".
- **Fix:** [piece 3](#3-a-seam-for-links-files-and-system-pages). **Size:** S. **Stage 0:** item 3.

### F8. View models reach into Infrastructure and the file system

Rule 7.

- **Where:** `ConfigPaths` in [`DashboardViewModel.cs:27`](../src/ContextSwitcher.App/ViewModels/DashboardViewModel.cs#L27),
  [`ProfilesViewModel.cs:21`](../src/ContextSwitcher.App/ViewModels/ProfilesViewModel.cs#L21) and
  [`MainAppViewModel.cs:30`](../src/ContextSwitcher.App/ViewModels/MainAppViewModel.cs#L30);
  `RunInfo` and `AppBuild` in [`SettingsDiagnosticsViewModel.cs:192`](../src/ContextSwitcher.App/ViewModels/SettingsDiagnosticsViewModel.cs#L192);
  `PreferredLanguages` in [`SettingsViewModel.cs:57`](../src/ContextSwitcher.App/ViewModels/SettingsViewModel.cs#L57).
  `SettingsDiagnosticsViewModel` reads the log file itself at
  [`:201`](../src/ContextSwitcher.App/ViewModels/SettingsDiagnosticsViewModel.cs#L201)-203. Three view
  models load icons from paths: [`AppPickerViewModel.cs:144`](../src/ContextSwitcher.App/ViewModels/AppPickerViewModel.cs#L144),
  [`AskOnSwitchViewModel.cs:152`](../src/ContextSwitcher.App/ViewModels/AskOnSwitchViewModel.cs#L152)
  and [`IgnoredAppsViewModel.cs:120`](../src/ContextSwitcher.App/ViewModels/IgnoredAppsViewModel.cs#L120).
- **Why it matters:** when item 6 splits Infrastructure, every `using ContextSwitcher.Infrastructure`
  in a view model has to be looked at again. The report's log lines can't be tested without a file.
  The three icon loaders already catch different exceptions (F19).
- **Fix:** `ConfigPaths` goes with [piece 2](#2-one-place-publishes-the-state-after-a-switch);
  `RunInfo` and `AppBuild` with [piece 10](#10-the-apps-identity-and-the-update-checker);
  `PreferredLanguages` with [piece 7](#7-the-macos-helpers-behind-interfaces). The log lines come
  from `DiagnosticsSources` like the rest of the report, and one icon loader serves the three.
- **Size:** S beyond those pieces. **Stage 0:** new.

### F9. macOS helpers called straight from the app

Rules 1 and 2.

- **Where:** `DisplayAvailability` in [`Program.cs:43`](../src/ContextSwitcher.App/Program.cs#L43),
  `:71` and `:111`; `PreferredLanguages` in [`Program.cs:64`](../src/ContextSwitcher.App/Program.cs#L64);
  `DockIcon` in [`App.axaml.cs:137`](../src/ContextSwitcher.App/App.axaml.cs#L137) and `:142`;
  `new AwayEvents()` in [`AppHost.cs:437`](../src/ContextSwitcher.App/Startup/AppHost.cs#L437);
  `WindowSpaces`, `MenuBarPanel`, `FrontmostApplication` and `OutsideClick` in
  [`DashboardWindow.axaml.cs:93`](../src/ContextSwitcher.App/Views/DashboardWindow.axaml.cs#L93)-203.
  [`RunInfo.cs:26`](../src/ContextSwitcher.Infrastructure/Logging/RunInfo.cs#L26), in shared
  Infrastructure, reads `/System/Library/CoreServices/SystemVersion.plist`.
- **Why it matters:** these are static classes, so the app can't be given a Windows version. Stage 1
  needs every one of them: sleep and a locked screen pause time, the language comes from Windows,
  the tray flyout behaves differently, and diagnostics name the Windows version. Item 3 lists the
  seams for switching and for links, not these, so they would turn up one by one in stage 1.
- **Fix:** [piece 7](#7-the-macos-helpers-behind-interfaces). **Size:** M. **Stage 0:** new; item 5
  covers whether a Dock icon exists, not how to show it.

### F10. macOS classes check for macOS themselves

Rule 2.

- **Where:** 15 `OperatingSystem.IsMacOS()` checks in 13 classes in
  [`Infrastructure/MacOS`](../src/ContextSwitcher.Infrastructure/MacOS), for example
  [`MacApplicationHider.cs:25`](../src/ContextSwitcher.Infrastructure/MacOS/MacApplicationHider.cs#L25)
  and [`MenuBarPanel.cs:37`](../src/ContextSwitcher.Infrastructure/MacOS/MenuBarPanel.cs#L37). The
  only check outside them is in the composition root,
  [`AppHost.cs:214`](../src/ContextSwitcher.App/Startup/AppHost.cs#L214), where it belongs.
- **Why it matters:** today they keep the tests passing on Windows, where these classes are built
  and do nothing. Once item 6 registers them only on macOS and marks their project for it, the
  checks are dead code that hides a wrong registration.
- **Fix:** remove them in item 6, with the tests of these classes marked `[MacOSFact]`. Keep
  [`MacLoginItem.cs:107`](../src/ContextSwitcher.Infrastructure/MacOS/MacLoginItem.cs#L107): it asks
  for a macOS version, which is a question within the platform. **Size:** S. **Stage 0:** item 6.

### F11. The allowed commands are a static macOS list

Rules 5 and 8.

- **Where:** [`CommandAllowlist.cs:8`](../src/ContextSwitcher.Core/Security/CommandAllowlist.cs#L8) -
  `osascript`, `open`, `sips`, `ditto`, `codesign` and the rest, in Core.
  [`ProcessRunner.cs:21`](../src/ContextSwitcher.Infrastructure/ProcessExecution/ProcessRunner.cs#L21)'s
  public constructor is fixed to it, and the tests use an internal one at
  [`:30`](../src/ContextSwitcher.Infrastructure/ProcessExecution/ProcessRunner.cs#L30).
- **Why it matters:** Windows runs other programs. With one static list, it either allows the Mac's
  tools on Windows and its own on the Mac, or grows a condition.
- **Fix:** [piece 8](#8-policies-as-data). **Size:** S. **Stage 0:** new; item 6 then puts each
  preset in its platform's project.

### F12. Timings are constants and test-only parameters

Rule 5.

- **Where:** the step budgets in [`AutomationPlanBuilder.cs:15`](../src/ContextSwitcher.Core/Automation/AutomationPlanBuilder.cs#L15)-22;
  quitting in [`AutomationStepExecutor.cs:30`](../src/ContextSwitcher.Infrastructure/Automation/AutomationStepExecutor.cs#L30)-36,
  `:108`-110 and `:468`, with `quitConfirmInterval` for tests at `:55`; tab probing in
  [`BrowserLauncher.cs:25`](../src/ContextSwitcher.Infrastructure/Browser/BrowserLauncher.cs#L25),
  `:28` and `:47`; the follow-up in [`ClosingAppsFollowUp.cs:20`](../src/ContextSwitcher.Core/Contexts/ClosingAppsFollowUp.cs#L20)-28
  and `:45`-47; the catalog in [`SystemCatalog.cs:23`](../src/ContextSwitcher.Infrastructure/Catalog/SystemCatalog.cs#L23)
  and `:29`.
- **Why it matters:** closing an app on Windows (`WM_CLOSE`, then watching its windows) will want other
  waits than AppleScript. Today a new value means a new constant and a new optional parameter, and
  the budgets of a step are split between the plan builder (the whole) and the executor (its parts),
  which have to agree.
- **Fix:** [piece 8](#8-policies-as-data). **Size:** S. **Stage 0:** new.

### F13. Optional services and defaults

Rule 3.

- **Where:** interfaces taken with `= null`:
  [`MainAppViewModel.cs:39`](../src/ContextSwitcher.App/ViewModels/MainAppViewModel.cs#L39)-41,
  [`SettingsViewModel.cs:51`](../src/ContextSwitcher.App/ViewModels/SettingsViewModel.cs#L51)-53,
  [`OnboardingViewModel.cs:46`](../src/ContextSwitcher.App/ViewModels/OnboardingViewModel.cs#L46),
  [`ProfileSetupViewModel.cs:75`](../src/ContextSwitcher.App/ViewModels/ProfileSetupViewModel.cs#L75),
  [`SettingsDiagnosticsViewModel.cs:36`](../src/ContextSwitcher.App/ViewModels/SettingsDiagnosticsViewModel.cs#L36),
  [`AutomationPlanBuilder.cs:30`](../src/ContextSwitcher.Core/Automation/AutomationPlanBuilder.cs#L30),
  and F5's and F6's; one that may be null,
  [`IgnoredAppsViewModel.cs:27`](../src/ContextSwitcher.App/ViewModels/IgnoredAppsViewModel.cs#L27).
  Defaults to a concrete thing:
  [`SettingsViewModel.cs:57`](../src/ContextSwitcher.App/ViewModels/SettingsViewModel.cs#L57)
  (`PreferredLanguages.Get`) and [`ProfilesViewModel.cs:35`](../src/ContextSwitcher.App/ViewModels/ProfilesViewModel.cs#L35)
  (a notice of its own).
- **Why it matters:** the container fills these in, so the app works - but a registration dropped
  in a refactor, or missing on Windows, raises no error: "open at login" or the default browser just
  disappear. A reader can't tell which features a class really needs.
- **Fix:** [piece 9](#9-no-optional-services), with F5's and F6's in pieces 5 and 6. **Size:** S.
  **Stage 0:** new; item 5 decides what a platform lacks.

### F14. UpdatesViewModel is a service and a view model

Rules 6 and 8.

- **Where:** [`UpdatesViewModel.cs:69`](../src/ContextSwitcher.App/ViewModels/UpdatesViewModel.cs#L69)
  owns the app's version, which `AppHost` reads at
  [`:100`](../src/ContextSwitcher.App/Startup/AppHost.cs#L100), `:108`, `:173` and `:182`. It runs
  the daily check on a timer ([`:152`](../src/ContextSwitcher.App/ViewModels/UpdatesViewModel.cs#L152)-168),
  sends the notification, logs, and is what Settings shows.
- **Why it matters:** stage 1 brings an updater for Windows (Velopack) and an update shown in the
  tray, not a notification. The checking and the showing change for different reasons, and the
  version - needed by the log and the CLI - should not come from a view model.
- **Fix:** [piece 10](#10-the-apps-identity-and-the-update-checker). **Size:** M. **Stage 0:** new.

### F15. AppHost does four jobs

Rule 6.

- **Where:** [`AppHost.cs`](../src/ContextSwitcher.App/Startup/AppHost.cs), 499 lines: the
  registrations (`Initialize`, from [`:83`](../src/ContextSwitcher.App/Startup/AppHost.cs#L83)); the
  start-up sequence, which reads and writes the settings and checks the file itself
  ([`BootstrapAsync`, `:262`](../src/ContextSwitcher.App/Startup/AppHost.cs#L262)); the housekeeping
  timers, the away pause and the follow-up ([`StartRuntimeServices`, `:369`](../src/ContextSwitcher.App/Startup/AppHost.cs#L369));
  and the app's state (F1).
- **Why it matters:** item 6 has to take the platform registrations out of a method that also starts
  timers and loads files. The start-up sequence - quarantined settings, crash recovery - can only be
  tested by starting the whole app.
- **Fix:** [piece 11](#11-apphost-split-in-three), after piece 4. **Size:** M. **Stage 0:** item 6
  takes the registrations; the rest is new.

### F16. Profile Setup is 871 lines

Rule 6.

- **Where:** [`ProfileSetupViewModel.cs`](../src/ContextSwitcher.App/ViewModels/ProfileSetupViewModel.cs)
  edits the name, colour, icon, apps, browser pages and profiles, Docker, quick links, notes and the
  switch policy, and saves. What browsers can do is decided here:
  [`:403`](../src/ContextSwitcher.App/ViewModels/ProfileSetupViewModel.cs#L403) (profiles only in
  Chrome and Brave), `:482` and `:505` (nothing for an unscriptable default), and the list of
  browsers with their names at [`:658`](../src/ContextSwitcher.App/ViewModels/ProfileSetupViewModel.cs#L658).
  To build it, [`MainAppViewModel.cs:26`](../src/ContextSwitcher.App/ViewModels/MainAppViewModel.cs#L26)
  takes 15 parameters, most only to pass on. `SettingsViewModel` (451 lines) is the same on a
  smaller scale: settings, login item, the Automation permission and the language.
- **Why it matters:** stage 2 changes the browser section (Edge, Firefox, a window per profile) and
  item 5 hides what a machine lacks. Both mean editing one class that also holds Docker and quick
  links, and the browser rules there are not where `ConfigurationValidator` keeps its own copy (F19).
- **Fix:** [piece 12](#12-profile-setup-in-sections). `FocusSettingsViewModel` and
  `MediaSettingsViewModel` already show the shape. **Size:** M. **Stage 0:** new; the rules come from
  item 5.

### F17. Core opens files for the switch lock

Rule 1, and agent.md's rule that Core makes no file system calls.

- **Where:** [`ContextSwitchService.cs:176`](../src/ContextSwitcher.Core/Contexts/ContextSwitchService.cs#L176)-214
  creates a directory and holds a `FileStream` beside `state.json`, with a temporary file as a
  stand-in.
- **Why it matters:** how one process keeps another from switching is a platform question - its own
  comment relies on Unix's advisory lock - and it is the only part of the switch that can't be
  replaced in a test.
- **Fix:** [piece 13](#13-the-switch-lock-out-of-core). **Size:** S. **Stage 0:** new.

### F18. AppLog where a logger is at hand

Rule 8.

- **Where:** [`ContextSwitchService.cs:161`](../src/ContextSwitcher.Core/Contexts/ContextSwitchService.cs#L161)
  calls `AppLog.Warn` while holding an `ILogger`. `UpdatesViewModel` (7 calls), `ClosingAppsFollowUp`
  (3), `ConfigurationStore` (2), `JsonFileStore` (2) and `BrowserLauncher` (2) are built by the
  container and could be given one.
- **Why it matters:** a test that wants to see what was logged has to know which of the two ways the
  class used, and `AppLog` writes nothing unless it was set up. `AppLog` is right for what can't be
  given anything: macOS callbacks, static helpers, the crash handlers in `Program`.
- **Fix:** an `ILogger` in their constructors. **Size:** S. **Stage 0:** new.

### F19. The same thing in several places

Rule 8.

- **Browser application names**, three times: [`BrowserLauncher.cs:533`](../src/ContextSwitcher.Infrastructure/Browser/BrowserLauncher.cs#L533),
  [`ProcessBrowserRuns.cs:17`](../src/ContextSwitcher.Infrastructure/Browser/ProcessBrowserRuns.cs#L17)
  and [`SystemCatalog.cs:274`](../src/ContextSwitcher.Infrastructure/Catalog/SystemCatalog.cs#L274);
  shown names twice more, in [`ProfileSetupViewModel.cs:658`](../src/ContextSwitcher.App/ViewModels/ProfileSetupViewModel.cs#L658)
  and [`BrowserProfileRowViewModel.cs:104`](../src/ContextSwitcher.App/ViewModels/BrowserProfileRowViewModel.cs#L104).
- **"Chrome or Brave"** four times: [`BrowserLauncher.cs:531`](../src/ContextSwitcher.Infrastructure/Browser/BrowserLauncher.cs#L531),
  [`ProfileSetupViewModel.cs:403`](../src/ContextSwitcher.App/ViewModels/ProfileSetupViewModel.cs#L403),
  [`BrowserProfileRowViewModel.cs:48`](../src/ContextSwitcher.App/ViewModels/BrowserProfileRowViewModel.cs#L48)
  and [`ConfigurationValidator.cs:221`](../src/ContextSwitcher.Core/Configuration/Validation/ConfigurationValidator.cs#L221).
  Adding Edge means finding all four - a capability for item 5.
- **`sips`** twice: [`SiteIcons.cs:186`](../src/ContextSwitcher.Infrastructure/Browser/SiteIcons.cs#L186)
  and [`InstalledAppsService.cs:145`](../src/ContextSwitcher.Infrastructure/Applications/InstalledAppsService.cs#L145)
  ([piece 14](#14-one-image-converter)).
- **Loading an icon** three times, catching different exceptions: `AppPickerViewModel` catches
  `NotSupportedException` and not `InvalidOperationException`; `AskOnSwitchViewModel` and
  `IgnoredAppsViewModel` the other way round (F8).
- **The log line for invalid settings**, word for word, in [`AppHost.cs:313`](../src/ContextSwitcher.App/Startup/AppHost.cs#L313)
  and [`ConfigurationStore.cs:58`](../src/ContextSwitcher.App/Services/ConfigurationStore.cs#L58).
- **"Is this the command line?"** four times: [`Program.cs:42`](../src/ContextSwitcher.App/Program.cs#L42)
  and `:52`, [`AppHost.cs:194`](../src/ContextSwitcher.App/Startup/AppHost.cs#L194) and `:201`.
- **`RunInfo.Current(…)`** with the same arguments three times (F14).
- **A new profile's id** by three rules in two classes: [`ProfilesViewModel.cs:240`](../src/ContextSwitcher.App/ViewModels/ProfilesViewModel.cs#L240),
  [`ProfileSetupViewModel.cs:833`](../src/ContextSwitcher.App/ViewModels/ProfileSetupViewModel.cs#L833)
  and `:851`. One `ProfileIds` in Core can be tested once.
- **Size:** S each. **Stage 0:** new.

### F20. Four ways to tell the time

Rule 8.

- **Where:** `IClock` in Core; a `TimeProvider` in [`LocalJsonLogger.cs:42`](../src/ContextSwitcher.Infrastructure/Logging/LocalJsonLogger.cs#L42);
  a `Func<DateTimeOffset>` in [`UpdatesViewModel.cs:51`](../src/ContextSwitcher.App/ViewModels/UpdatesViewModel.cs#L51)
  and [`DiagnosticsSources.cs:12`](../src/ContextSwitcher.App/Services/DiagnosticsSources.cs#L12);
  and `DateTimeOffset.UtcNow` straight in [`SwitchCoordinator.cs:57`](../src/ContextSwitcher.App/Services/SwitchCoordinator.cs#L57),
  [`AutomationStepExecutor.cs:484`](../src/ContextSwitcher.Infrastructure/Automation/AutomationStepExecutor.cs#L484)
  (beside an `IClock` it holds), [`ProfilesViewModel.cs:35`](../src/ContextSwitcher.App/ViewModels/ProfilesViewModel.cs#L35),
  [`AppHost.cs:103`](../src/ContextSwitcher.App/Startup/AppHost.cs#L103) and `:166`, and
  [`SettingsDiagnosticsViewModel.cs:127`](../src/ContextSwitcher.App/ViewModels/SettingsDiagnosticsViewModel.cs#L127).
- **Why it matters:** a test that fixes the time has to fix it four ways, and misses the direct
  calls.
- **Fix:** `IClock` everywhere, or .NET's `TimeProvider` everywhere - one of them. **Size:** S.
  **Stage 0:** new.

### F21. Names that mislead

Rule 8.

- [`RunInfo.cs:10`](../src/ContextSwitcher.Infrastructure/Logging/RunInfo.cs#L10) calls the system's
  version `MacOs`; the log ([`TrailLogger.cs:208`](../src/ContextSwitcher.Infrastructure/Logging/TrailLogger.cs#L208))
  and the report write it as "macOS". It becomes `SystemVersion` with piece 7.
- [`ContextSwitchSource.cs:9`](../src/ContextSwitcher.Core/Contexts/ContextSwitchSource.cs#L9):
  the Profiles page and the setup wizard switch as `Dashboard`
  ([`ProfilesViewModel.cs:121`](../src/ContextSwitcher.App/ViewModels/ProfilesViewModel.cs#L121),
  [`OnboardingViewModel.cs:208`](../src/ContextSwitcher.App/ViewModels/OnboardingViewModel.cs#L208)),
  so the log can't tell them apart; `StartupRecovery` is never used; `MenuBar` is the tray on Windows.
- [`AppHost.cs:358`](../src/ContextSwitcher.App/Startup/AppHost.cs#L358)-361 is a summary left from
  a method that moved, stacked on `StartRuntimeServices`'s own.
- [`BrowserLauncher.cs:151`](../src/ContextSwitcher.Infrastructure/Browser/BrowserLauncher.cs#L151)
  drops what `TryFocusExistingTabAsync` returns, so its `TabProbeOutcome` enum says nothing to anyone.
- **Size:** XS each. **Stage 0:** new.

### F22. Step arguments are a dictionary of strings

Rules 1 and 8.

- **Where:** [`AutomationPlanBuilder.cs`](../src/ContextSwitcher.Core/Automation/AutomationPlanBuilder.cs#L114)
  writes keys such as `"apps"`, `"profilesJson"` and `"avoidDuplicateTabs"` (as `"True"`), and
  [`AutomationStepExecutor.cs:264`](../src/ContextSwitcher.Infrastructure/Automation/AutomationStepExecutor.cs#L264)-268
  and its other handlers read them back by the same strings.
- **Why it matters:** a new field for a step is a string written in Core and read in Infrastructure,
  with only a test to say they match. agent.md asks that the arguments carry everything the executor
  needs; typed records do that too.
- **Fix:** [piece 16](#16-typed-step-arguments). **Size:** M. **Stage 0:** new; lowest priority,
  since it works and is tested.

## Boundaries to keep

These look like breaks of the rules and are not, or not yet:

- **`App` and `Program` use `AppHost.Services`.** Avalonia creates `App`, and `Program` is the entry
  that builds the container (F2).
- **Views give view models what belongs to their window.**
  [`SettingsPage.axaml.cs:26`](../src/ContextSwitcher.App/Views/Pages/SettingsPage.axaml.cs#L26) sets
  the file picker and the clipboard, which need the page's `TopLevel`.
- **Parent view models build their children.** agent.md asks for it, and children such as rows hold
  no services. Profile Setup is the exception (F16).
- **`new DispatcherTimer()` in view models**, such as [`DashboardViewModel.cs:63`](../src/ContextSwitcher.App/ViewModels/DashboardViewModel.cs#L63):
  user-interface glue, not a service.
- **`SwitchCoordinator` builds the ask-on-switch view model**
  ([`SwitchCoordinator.cs:83`](../src/ContextSwitcher.App/Services/SwitchCoordinator.cs#L83)) and
  hands it to an `IAskOnSwitchPresenter`, which is the seam.
- **`AppLog`** in macOS callbacks, static helpers and the crash handlers (F18).
- **Pure static helpers** - `PageMatch`, `SpotifyLink`, `ReleaseVersion`, `AppleScriptBuilder`, the
  message templates, `ContextSwitcherJson`, `Localized`, `Plurals` - hold no state and need no seam.
- **`AppLanguages.Current`**: the language is the process's culture, chosen once before any window.
- **Values given for tests** - a home folder in `SystemCatalog`, paths in `AppBundleUpdater` - are
  data, not services. Timings are the exception (F12).
- **The `OperatingSystem.IsMacOS()` checks** stay until item 6 (F10).

## Checking a pull request

For the pieces above and the stages after them:

- Does a new class that varies by platform or policy have an interface in `Core/Abstractions`, and is
  the choice made in `AppHost` (later, each platform's registrations)?
- Does any constructor default an interface to `null` or to a class?
- Does a view model use anything from `ContextSwitcher.Infrastructure`, `File`, `Process` or a static
  of `AppHost`?
- Is a wait, a limit or a list a value with a named preset rather than a constant in the class?
- Does the change copy something that already exists?
