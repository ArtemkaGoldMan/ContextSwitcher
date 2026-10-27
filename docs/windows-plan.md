# The Windows plan

The order to build the Windows version in, one release at a time: first apps, then the browser, then
the rest. What Windows can and can't do, and why, is in [A Windows version](windows-port.md); where
it sits among everything else, in the [roadmap](roadmap.md).

**Where it stands** (October 2026, at 0.6.8): stage 0, the groundwork on the Mac, is done - every
piece of macOS is behind an interface, and the Mac and Windows each have a project of their own
([stage 0](#stage-0---groundwork-done)). Stage 1 has begun: the app starts on Windows as a shell, with
its tray icon and dashboard, but switches nothing there yet ([stage 1](#stage-1---apps)).

## How it goes

- **Each stage ends in something a person can install and use.** A stage that doesn't have a
  feature yet hides it; it never shows a setting that does nothing.
- **The Mac app doesn't change** in what it does at any stage. Every change to shared code ships to
  Mac users first, through the usual releases.
- **One repository, one branch.** Core and nearly all of the interface are shared; each platform has
  its own Infrastructure project ([how](#one-repository-two-apps)).
- **Windows 10 (version 2004 and later) and Windows 11, on x64 and Arm** ([decision](#decisions-made)).

| Stage | Windows gets | Status |
| --- | --- | --- |
| [0. Groundwork](#stage-0---groundwork-done) | nothing yet: the seams, the projects, CI on Windows | done |
| [1. Apps](#stage-1---apps) | the tray, profiles, launching, closing and hiding apps, time | in progress: the shell done |
| [2. Browser](#stage-2---browser) | pages, a window per profile, browser profiles, reading tabs | |
| [3. The rest](#stage-3---the-rest) | Docker, Spotify, notifications, the command line, a hotkey | |
| [4. Windows 1.0](#stage-4---windows-10) | signed, in the Store or with an installer, documented, translated | |
| [Later](#later-if-wanted) | a browser extension, a browser profile of the app's own | |

## Where each feature lands

| Feature | Stage |
| --- | --- |
| Tray icon and its menu, the dashboard, the main window, the setup wizard | 1 |
| Profiles: editing, reordering, colours, icons, notes | 1 |
| Quick links on the dashboard (opened in the default browser) | 1 |
| Launching, closing and hiding apps; "still closing"; Force quit | 1 |
| Apps no profile has, in the ask-on-switch list; apps it never touches | 1 |
| Time in each profile, Stats, pausing for sleep and a locked screen | 1 |
| Settings: language, theme, open at login, history, export and import, diagnostics, detailed log, contact | 1 |
| Updates (a preview, unsigned) | 1 |
| Only one copy running | 1 |
| Opening pages, not opening them twice, closing them on leaving | 2 |
| Chrome, Edge and Brave profiles; closing a whole browser profile | 2 |
| Tabs no profile has, in the ask-on-switch list; "From open tabs" | 2 |
| Firefox: pages and profiles; reading its tabs | 2 |
| Site icons | 2 |
| Docker | 3 |
| Spotify | 3, in part |
| Notifications | 3 |
| The command line | 3 |
| A global hotkey to switch (instead of Siri and Shortcuts) | 3 |
| Focus | 3, only if Microsoft grants the token; otherwise not on Windows |
| Signed releases, the Microsoft Store, Windows docs | 4 |
| Every tab's address and closing it anywhere; tab groups | later, with an extension |
| Apple Music, Safari | not on Windows |
| The Dock icon setting, the Automation permission | not needed on Windows |

## One repository, two apps

### The projects

```text
src/
  ContextSwitcher.Core                     shared: profiles, switching, time, the interfaces   net10.0
  ContextSwitcher.Infrastructure           shared: files, the log, GitHub, the step handlers   net10.0
  ContextSwitcher.Infrastructure.MacOS     the Mac's: AppleScript, Objective-C calls           net10.0
  ContextSwitcher.Infrastructure.Windows   Windows': the shell so far, stand-ins for the rest     net10.0-windows10.0.19041.0
  ContextSwitcher.App                      the interface, built once for each                  both of them
  ContextSwitcher.Tests                    every test; [MacOSFact] and [WindowsFact] for one   net10.0
  ContextSwitcher.Tests.Windows            the Windows code's tests, run only on Windows       net10.0-windows10.0.19041.0
```

To come: `ContextSwitcher.Cli`, Windows' command line (stage 3).

- **The app is built twice from one project.** It lists both targets, and each brings in only its
  own platform project.
- **No `#if` in shared code.** Each platform project has a `PlatformServices.Register` of the same
  name in the same namespace, and `AppServices` calls it; which one runs is settled by the build. What
  the interface needs to know about the platform, it asks `PlatformCapabilities` - "is there Focus?",
  never "is this a Mac?".
- **The compiler guards the line.** The platform projects are marked `[assembly:
  SupportedOSPlatform("macos")]` and `("windows")`, so the platform analyzer (CA1416) flags shared
  code that reaches into one, and CI builds with warnings as errors.
- **Either computer compiles everything.** `EnableWindowsTargeting` in `Directory.Build.props` lets a
  Mac build the Windows projects, so a mistake in the Windows code shows on either machine.
- **One version**, in `ContextSwitcher.App.csproj`, shared by both builds.

The rules all of it follows are in [the design rules](architecture.md#design-rules).

### What each download holds

- **The Mac app** is published with `-f net10.0 -r osx-arm64`. The Windows projects aren't compiled
  into it, and native libraries come only for macOS, so the Windows version adds nothing to it but its
  wording in `Strings.resx`.
- **The Windows app** will be published with `-f net10.0-windows10.0.19041.0 -r win-x64` (and
  `win-arm64`) and holds no macOS code. Its target brings in the Windows SDK projection
  (`Microsoft.Windows.SDK.NET.dll`), which is large until trimmed - a question for the Windows
  download only.

### CI

Every pull request runs **Build and test** (macOS), **Build and test (Windows)**, **App bundle** and
**Workflows and scripts**, and CodeQL reads the Windows code as well as the Mac's. `main` requires the
first macOS one, the bundle and the lint ([release process](release-process.md#what-is-checked-before-a-change-reaches-main));
the Windows job runs on every pull request, but merging doesn't wait for it yet.

Stage 1 adds:

- **Build and test (Windows) as a required check** (Settings → Branches), so a change that breaks
  Windows can't merge.
- **Windows package**: builds the installer, installs it and starts the app.
- **PowerShell scripts** in `scripts/windows/`, linted with PSScriptAnalyzer next to the shell
  scripts.

Both build-and-test jobs always run: skipping the Windows one when only Mac files change is
tempting, but a required check that a path filter skipped blocks the merge, and Actions minutes are
free for a public repository.

### Versions and releases

Decided: **one version number for both**, one tag and one GitHub release per version, and **a
release carries the platforms that changed** - a change to shared code goes to both, a change to one
platform's code to that one. A platform's numbers may skip: if 0.8.1 fixed something on the Mac
only, Windows goes from 0.8.0 to 0.8.2. Release notes say what is new since a platform's previous
version, with a section for each platform.

How others do it, and why not that:

| How | Who | What it costs |
| --- | --- | --- |
| One number, and every release for every platform | VS Code, Slack, Telegram Desktop | the simplest; but a release with nothing new for one platform still asks its people to update |
| One number, and a platform's build only when something changed for it | Chrome: one version, and a stable update can give Windows and Mac different last numbers | the one chosen: each platform updates only when there's a reason |
| Separate products, separate numbers | Microsoft Office for Mac and for Windows | fits apps with separate code; with shared code, "Windows 0.3 or Mac 1.2?" only confuses |

With the Windows job, in stage 1, the release workflow becomes three jobs:

```text
Release (started by hand: the version, and a tick for each platform)
  ├─ macOS     macos-15         build, test, sign, .dmg and .zip          if ticked
  ├─ Windows   windows-latest   build, test, installer and update feed    if ticked
  └─ Publish   ubuntu-latest    the tag, one release with what was built; "latest" only with Mac files
```

### Keeping the Mac's latest release

`install.sh` and the Homebrew cask download `releases/latest/download/ContextSwitcher.zip`. If a
release without Mac files became the latest, both would download a 404. So:

1. **"Latest" is always a release with Mac files.** The release workflow marks what it publishes the
   latest outright; a release without them is published with `--latest=false`. Done for the Mac in
   [#79](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/79); the publishing job keeps it in
   stage 1.
2. **The Mac updater doesn't rely on it.** `GitHubReleaseSource` takes the highest version among the
   newest releases that carry the Mac's files - the zip or a disk image
   ([#79](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/79)).
3. **The Windows updater never uses "latest"** either: it takes the newest release with its own
   files. Stage 1 checks that Velopack's feed passes over a release without Windows files.
4. **File names never overlap.** `ContextSwitcher.zip` and `ContextSwitcher-x.y.z.dmg` stay the
   Mac's; every Windows file has `win` in its name.
5. **Download counts tell the platforms apart.** [`collect-stats.py`](../scripts/collect-stats.py)
   counts every `.zip` as a Mac update; stage 1 teaches it the Windows files.

## Stage 0 - Groundwork (done)

Every macOS-only piece behind an interface, the solution building and its tests passing on macOS and
on Windows, and Mac users seeing no difference:

| What | Pull request |
| --- | --- |
| The tests pass on Windows, and CI builds and tests there | [#76](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/76) |
| Saving survives a file held open on Windows (`IFileReplacer`); Windows keeps everything in `%APPDATA%\ContextSwitcher` | [#78](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/78) |
| The Mac updater finds its own release; "latest" stays the Mac's | [#79](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/79) |
| A leak fixed, the switch lock and image conversion behind interfaces, one logger and one clock | [#81](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/81) |
| The app's state as a service; the update checker; `AppHost` split | [#82](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/82) |
| macOS behind interfaces: links, apps, media, Focus, browsers, the app's helpers | [#83](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/83) |
| What a platform can do (`PlatformCapabilities`), its waits and commands as data; the `apps` map for Windows | [#84](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/84) |
| The macOS code in a project of its own, and a Windows one | [#85](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/85) |

Still to check: the first Mac release built from all of it ([roadmap](roadmap.md#next)).

## Stage 1 - Apps

**The aim.** Someone on Windows installs it, sets up Work and Personal, and switches: apps open,
close or minimise, and time is counted. Released as a preview.

It comes in four pull requests, each leaving the Mac app as it was:

| | Brings | Status |
| --- | --- | --- |
| 1a | The shell: `WinExe`, the tray icon in its two drawings, the popover by the taskbar, one copy, the font, everything else hidden. What the later ones bring is a stand-in, in `Infrastructure.Windows/NotYet/`, and `ContextSwitcher.Tests.Windows` tests the Windows code | done |
| 1b | Apps: the picker, launching, closing, "still closing", Force quit, minimising, the ask-on-switch list | in progress: the list, opening, closing and Force quit done |
| 1c | The rest: time with sleep and lock, open at sign-in, the Windows wording, and the wizard saying how to pin the icon | |
| 1d | CI and releases: the Windows job required, the installer, the release workflow | |

The shell of the app:

- Built as `WinExe`, so no console window opens with it.
- The tray icon, in a light and a dark version that follow the taskbar's theme. A left click opens
  the dashboard, a right click the menu. The setup wizard says how to pin the icon, which Windows 11
  hides at first.
- The dashboard as a borderless, topmost window by the tray, on whichever edge the taskbar is, that
  hides when it loses focus - Windows' `IPopoverWindow`.
- One copy only: a named mutex, and a pipe that tells the running copy to show its window.
- `Segoe UI Variable` first among the fonts; Windows 10 has no such font and falls back to `Segoe UI`.
- Everything not in this stage - the browser, Focus, music, Docker - hidden through
  `PlatformCapabilities`.

Apps:

- **The picker**: the Start menu's apps, desktop and Store alike, listed by PowerShell's
  `Get-StartApps` (with a fixed script, its output in Base64 so no code page garbles a name) and kept
  for five minutes. What isn't an app is left out - uninstallers, help files and web links, `.msc`
  consoles - and of two apps with one name, the one with an AppUserModelID is kept. Their icons (`IShellItemImageFactory`) are still to come, and so is writing what
  was picked into the `apps` map: until then the name in the Start menu is the key, matched without
  regard to case. The wizard suggests the usual Windows ones: Slack, Teams, Outlook,
  Visual Studio Code, Discord, Telegram, Steam, Spotify. What was picked goes into the `apps` map
  ([configuration](configuration.md#top-level)).
- **Launching** through `explorer shell:AppsFolder\<id>`, which starts a desktop and a Store app
  alike and brings a running one forward. The id is the AppUserModelID, or the path of the executable
  for an app that has none.
- **Telling an app's processes**: a profile names an app as the Start menu does, and Windows names a
  process by its executable, so `AppIdentities` goes from one to the other. A Store app's id has a
  `!`, and what is before it is the package family, which every process of the package carries
  (`GetPackageFamilyName`). A path is the executable, its first part a known folder's GUID written out
  (`SHGetKnownFolderPath`). Any other id takes the target of the Start menu's shortcut of that name,
  read in the same PowerShell call. A process is the app's if its executable is that one, or has the
  same file name below its folder - an app that updates itself keeps each version in a folder of its
  own. An app that none of this tells, such as File Explorer, can't be told and isn't touched.
- **Closing**: `WM_CLOSE` to each of the app's main windows - shown, unowned, not a palette - and of
  the processes it started. A dialog stays for the app to answer. The window is checked to be one
  first: a null handle or `HWND_BROADCAST` would otherwise post to this app, or to every window.
- **"Still closing", a window on screen, Force quit**: `EnumWindows` over the app and its child
  processes, leaving out cloaked windows, palettes and anything under 100 pixels; a minimised window
  counts, as it is on the taskbar. `TerminateProcess` ends the app and what it started when the person
  clicks Force quit, other than this app's own process and the ones Windows can't run without
  (`explorer.exe` and the like). A process is taken for a child only if it started after its parent,
  as Windows reuses the ids of ended processes. Still to come: `IVirtualDesktopManager`, to tell a
  window on another desktop from a hidden one.
- **Hiding** minimises the app's windows; entering the profile again restores them.
- **The ask-on-switch list** with the apps no profile has, from the visible top-level windows.

The rest of this stage:

- **Time**: `SessionSwitch` (a locked screen, another user) and `PowerModeChanged` (sleep and wake)
  pause and resume it, as on macOS.
- **Settings**: the language from `CultureInfo.CurrentUICulture`; the theme follows Windows; open at
  login through the `Run` key (and `StartupApproved`); export and import; diagnostics with the
  Windows version; Contact through a `mailto:` link; "show the log" with `explorer /select,`.
- **Quick links** open in the default browser.
- **Text**: every string this stage shows gets its Windows wording - the taskbar instead of the menu
  bar, Ctrl instead of ⌘, the PC instead of the Mac - in English and Ukrainian.
- **CI and releases**: the Windows job required, the Windows package job, and the release workflow in
  three jobs ([above](#ci)); an installer that updates itself (Velopack -
  [decided](#decisions-made)), for `win-x64` and `win-arm64`, unsigned and marked preview, in the
  same GitHub releases as the Mac app whenever something changed for Windows. Windows warns once
  before the first run, and the README says so.

What the app says, because Windows works that way: an app that goes to the tray when its window
closes - Slack, Teams, Discord, Telegram, Spotify - keeps running, and the card offers Force quit;
an app running as administrator can't be closed.

**Done when**, on a real Windows 11 PC, on Windows 10 in a virtual machine, and on Windows 11 on Arm
in a virtual machine: switching
closes and opens Visual Studio Code, Slack, Teams and Outlook; the card names an app that stayed and
Force quit ends it; time is counted and stops for sleep and a locked screen; opening the app twice
leaves one tray icon; it installs, and updates to the next preview.

## Stage 2 - Browser

**The aim.** A profile's web pages open and close with it in Chrome, Edge and Brave, and open in any
other browser.

- **Pages** open through the shell in the default browser, found from the `UserChoice` ProgId for
  `https`.
- **A window per profile.** In Chrome, Edge and Brave a profile opens its pages in a new window named
  after it (`--new-window --window-name=Work`). The window is found again by its title, its window
  class and the browser's AppUserModelID; leaving the profile closes it with `WM_CLOSE`, and entering
  again brings it forward rather than opening the pages twice. The name survives a browser restart
  with the last session restored.
- **Browser profiles**: `Local State` under `%LOCALAPPDATA%` for Chrome, Edge and Brave, opened with
  `--profile-directory`, the browser found through `App Paths`. Leaving a profile can close all of a
  browser profile's windows, found by their AppUserModelID.
- **Reading tabs with UI Automation**: the titles of the tabs outside the profile window for the
  ask-on-switch list, and closing one with its Close button when the person picks it. "From open
  tabs" in Profile Setup walks through the tabs one by one to read their addresses - only when the
  person asks, and the picker says the tabs will flicker.
- **Firefox**: pages, and its profiles with `-P`; the addresses of its tabs read from
  `recovery.jsonlz4` for the picker and the ask-on-switch list.
- **Site icons** made by a SkiaSharp `IImageConverter` instead of `sips`.
- **Settings from a Mac**: Safari becomes the default browser, and pages that closed on leaving now
  live in the profile window. Importing says so.
- **Profile Setup** explains the profile window in a line, as it explains Focus filters for Safari
  on macOS.

Not in this stage: closing a profile's page that was moved to another window, and finding a
duplicate in another window by its address.

**Done when**, in Chrome and Edge: Work opens its pages in a "Work" window, switching to Personal
closes it, and coming back brings it forward; the ask-on-switch list shows the other tabs by title
and closes the one picked; all of it still works after the browser restarts.

## Stage 3 - The rest

- **Docker** through `docker.exe`, as on macOS.
- **Spotify**: the `spotify:` link opens the playlist, and Windows' media session controls
  (`GlobalSystemMediaTransportControlsSessionManager`) press Play. Profile Setup says that it plays
  what Spotify has open, which is not always that playlist.
- **Notifications** as toasts. On the Mac only a new version is announced that way; until this
  stage Windows shows it in the tray menu and in Settings alone.
- **The command line**: a small console program next to the app, with the same commands, named so it
  differs from `ContextSwitcher.exe` by more than case.
- **A global hotkey** to switch profiles (`RegisterHotKey`), set in Settings - what Shortcuts and
  Siri do on the Mac.
- **Focus**: ask Microsoft for the Limited Access Feature token for `FocusSessionManager`. If it
  comes, a profile turns Windows Focus on and off; if it doesn't, Focus stays out.

**Done when** each of these works on a real PC, and the command line switches profiles from a
script and from the hotkey.

## Stage 4 - Windows 1.0

- **Signing and the channel**: the Microsoft Store (free for individuals; it signs and updates the
  package, and the app and the command line go in the same MSIX), a signed installer, or both.
- **The release workflow** signs the Windows files, or sends the package to the Store, and the
  release notes stop calling Windows a preview.
- **Docs**: a Windows section in the README and the user guide - installing, the tray icon,
  uninstalling - with screenshots from Windows.
- **Text**: every string in English and Ukrainian for Windows.
- **Measured** as on the Mac ([performance](performance.md)), with Defender and the browser running.

**Done when** someone who has never seen the app installs it from the Store or the installer with
no warning, and finds their way with the README.

## Later, if wanted

- **A browser extension** with a native messaging host: every tab's id and address in the person's
  own browser, closing it wherever it went, tab groups - on macOS too - and Firefox. It costs a
  separate JavaScript project, a Chrome Web Store registration, Edge Add-ons, and people installing
  it ([browser tabs](windows-port.md#browser-tabs)).
- **A browser profile of the app's own**, driven through the DevTools protocol: exact control of
  tabs without an extension, for people who want it.

## Decisions made

On 26 October 2026:

- **Which Windows**: Windows 10 from version 2004 (build 19041, what the project already targets)
  and Windows 11. Microsoft's support for Windows 10 has ended for most people, but many still run
  it, and nothing in the plan needs 11 alone. What differs: no `Segoe UI Variable`, and the words
  for pinning the tray icon, which the wizard gives for each.

On 19 October 2026:

- **How a profile names an app on Windows**: the `apps` map from a name to its AppUserModelID and
  executable, done in [#84](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/84). Rejected:
  turning the lists of names into lists of objects, a breaking change to `settings.json`; and the
  name alone, looked up in the Start menu at every switch, which is slow and fails when names repeat
  or change.
- **Where Windows keeps everything**: `%APPDATA%\ContextSwitcher`, where Windows apps keep their
  settings. Not `%LOCALAPPDATA%`, which Velopack replaces on each update, nor `~/.config` as on the
  Mac, where nobody on Windows looks.
- **Versions**: one number for both platforms, each release carrying the platforms that changed
  ([versions and releases](#versions-and-releases)).
- **How the preview ships**: Velopack on GitHub releases - quick and free, unsigned, with a warning
  on the first run. The Microsoft Store can come at [Windows 1.0](#stage-4---windows-10).

## Decisions to make

Before stage 2:

- **The profile window's name**: the profile's name ("Work"), as the person sees it in the taskbar,
  or a longer one that no other window could have.

Before "later":

- **Whether to build the extension**, and whether it comes before Windows 1.0 or after.
