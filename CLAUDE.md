# ContextSwitcher

A menu bar app that switches a Mac between profiles - work, personal, study - in one click: it opens
and quits apps, opens and closes browser pages, turns on Focus, plays music, starts and stops Docker
containers, and counts the time spent in each. Free, MIT, and everything stays on the computer.
.NET 10 and Avalonia 12, for macOS 13 and later on Apple silicon. A Windows version is being built a
stage at a time.

## Where things are

```text
src/ContextSwitcher.Core                    profiles, switching, time, logging, messages, the interfaces
src/ContextSwitcher.Infrastructure          shared: files, the log, GitHub, the command line, step handlers
src/ContextSwitcher.Infrastructure.MacOS    AppleScript, the Objective-C calls, open, sips, shortcuts
src/ContextSwitcher.Infrastructure.Windows  Windows' implementations, as they are written
src/ContextSwitcher.App                     the interface; the composition root is in Startup/
src/ContextSwitcher.Tests                   every test; fakes in TestDoubles/
src/ContextSwitcher.Tests.Windows           the Windows code's tests, which need its target; run only on Windows
scripts/                                    building the .app, the .dmg and the zip; screenshots; stats
```

| Doc | Read it for |
| --- | --- |
| [docs/architecture.md](docs/architecture.md) | How it is built and the contracts it keeps: the design rules, the files, a switch, each step, the command line, the interface. **Read before changing code.** |
| [docs/configuration.md](docs/configuration.md) | Every field of `settings.json` |
| [docs/user-guide.md](docs/user-guide.md) | What the person sees, page by page |
| [docs/release-process.md](docs/release-process.md) | CI, signing, releases, updates, the checks by hand, screenshots |
| [docs/roadmap.md](docs/roadmap.md) | What is next, later and not planned |
| [docs/windows-plan.md](docs/windows-plan.md), [docs/windows-port.md](docs/windows-port.md) | The Windows stages, and what Windows can and can't do |
| [docs/translating.md](docs/translating.md) | The app's text and adding a language |
| [docs/automation-permissions.md](docs/automation-permissions.md), [docs/shortcuts-integration.md](docs/shortcuts-integration.md) | macOS permissions; Focus Shortcuts, Siri and the command line |
| [docs/browser-control.md](docs/browser-control.md), [docs/performance.md](docs/performance.md), [docs/usage-statistics.md](docs/usage-statistics.md) | Research behind decisions: browsers, memory, counting users |

## Build and test

```bash
dotnet build ContextSwitcher.sln -c Release --no-incremental -warnaserror
dotnet test
dotnet run --project src/ContextSwitcher.App/ContextSwitcher.App.csproj --framework net10.0
```

- Warnings are errors. CI builds and tests on macOS and on Windows; the `.app` is built by
  `scripts/build-app.sh`, on a Mac only.
- `global.json` pins the SDK. Don't change it to suit one machine.
- Run from source, the app is a development build: its files are in `~/.config/ContextSwitcher-dev`,
  apart from an installed copy's. `CONTEXTSWITCHER_CONFIG_DIR` points it at another folder.

## Changing code

The full rules, and what only looks like a break of them, are in
[docs/architecture.md](docs/architecture.md#design-rules).

- Read the code around a change first, and write like it: its names, its idioms, its comments.
- Behaviour that varies by platform or policy sits behind an interface in `Core/Abstractions` and is
  given through constructors. Platform and policy are chosen only in the composition root -
  `AppServices`, and each platform project's `PlatformServices`.
- No `OperatingSystem.Is…()` in classes and no `#if` in shared code; what the interface needs to
  know, it asks `PlatformCapabilities`.
- No constructor parameter defaulting to a class or to `null`, no `new` of a service, no service
  locator outside `AppHost`, `App` and `Program`.
- Waits, limits and lists are data with named presets, not constants in a class.
- View models use abstractions only: nothing from Infrastructure, `File`, `Process` or `AppHost`.
- Core has no files, processes, UI or wall clock - time comes from `IClock`.
- Commands run through `IProcessRunner` with argument arrays, only those in the platform's
  `CommandAllowlist`; AppleScript is built by `AppleScriptBuilder`. Nothing from the settings is ever
  run as a shell string.
- No visible text in views or view models: it lives in `Resources/Strings.resx`, in English and
  Ukrainian. Messages from Core and Infrastructure are `MessageTemplate`s.
- A `catch` either logs or says why not in a comment starting `Not logged:`.
- Every change in behaviour comes with tests. Tests never drive the real system; a test of macOS or
  Windows itself is a `[MacOSFact]` or a `[WindowsFact]`.
- A change to `settings.json`'s schema is breaking: people edit it by hand. It needs a migration and
  a release note, and old files must still read.

## What the app promises

- Everything stays on the computer: no accounts, no telemetry. The only network calls are the pages
  a profile opens, site icons, and the daily update check. Adding any other is the owner's decision
  ([usage statistics](docs/usage-statistics.md)).
- Every feature is free. Support the developer is a link, never a gate.
- Nothing is forced to quit unless the person clicks Force quit, and no tab is closed that a profile
  doesn't list.
- Work on Windows doesn't change what the Mac app does. Windows features land in the order of
  [the Windows plan](docs/windows-plan.md).

## Docs

- Plain, short sentences in British English; " - " for a dash in docs (the README uses "—").
- Present tense: what is, not what was. No history, dates or "used to" in reference docs - plans go
  in the roadmap or the Windows plan, and the reasons for past changes in commits and pull requests.
- A change that makes a doc untrue updates it in the same pull request: `configuration.md` for a
  setting, the user guide for anything on screen, the README's Privacy section for anything that
  leaves the computer, `.github/release-notes.md` for the next release.

## Git and releases

- Work on a branch from `main`. Pull requests use `.github/pull_request_template.md` and are merged
  with a merge commit; **Build and test**, **App bundle** and **Workflows and scripts** must pass.
- Never delete the `stats` branch: the nightly Usage stats workflow writes to it.
- A release is a tag `vX.Y.Z` equal to `<Version>` in `ContextSwitcher.App.csproj`
  ([release process](docs/release-process.md)). One version number for both platforms.
- The Mac's updater, `install.sh` and the Homebrew cask read GitHub's latest release, so the latest
  release must always carry the Mac's files
  ([why](docs/windows-plan.md#keeping-the-macs-latest-release)).

## Done means

Wired through the container; logged; failures shown to the person in plain words; tested as far as
the risk asks; nothing leaving the computer; the UI thread never blocked; spaces in paths, app names
and profile directories working; docs updated; no new dependency the change doesn't need.
