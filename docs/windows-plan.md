# The Windows plan

The order to build a Windows version in, one release at a time: first apps, then the browser, then
the rest. What Windows can and can't do, and why, is in [A Windows version](windows-port.md).

Written in October 2026 against 0.6.7. Stage 0 has begun: its first two items are done
([#76](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/76)). Nothing for Windows users is
built yet.

## How it goes

- **Each stage ends in something a person can install and use.** A stage that doesn't have a
  feature yet hides it; it never shows a setting that does nothing.
- **The Mac app doesn't change** in what it does at any stage. Every change to shared code ships to
  Mac users first, through the usual releases.
- **One repository, one branch.** Core and nearly all of the interface are shared; each platform has
  its own Infrastructure project ([how](#the-repository-ci-and-releases)).
- **Windows 11 on x64 and Arm** to begin with ([decisions](#decisions-to-make)).

| Stage | Windows gets | Rough size |
| --- | --- | --- |
| [0. Groundwork](#stage-0---groundwork-on-the-mac) | nothing yet: seams, the app field, capabilities, CI on Windows, the release workflow | 1,000 lines changed |
| [1. Apps](#stage-1---apps) | the tray, profiles, launching, closing and hiding apps, time | 2,000 lines new |
| [2. Browser](#stage-2---browser) | pages, a window per profile, browser profiles, reading tabs | 1,200 lines new |
| [3. The rest](#stage-3---the-rest) | Docker, Spotify, notifications, the command line, a hotkey | 700 lines new |
| [4. Windows 1.0](#stage-4---windows-10) | signed, in the Store or with an installer, documented, translated | little code |
| [Later](#later-if-wanted) | a browser extension, a browser profile of the app's own | - |

The sizes are guesses for the code, not counting its tests.

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

## The repository, CI and releases

How the Mac and the Windows app live in one repository, what each download holds, how both are
checked, and how they are released.

### The projects

```text
src/
  ContextSwitcher.Core                     shared: profiles, switching, time     net10.0
  ContextSwitcher.Infrastructure           shared: files, the log, GitHub        net10.0
  ContextSwitcher.Infrastructure.MacOS     AppleScript, Objective-C calls        net10.0
  ContextSwitcher.Infrastructure.Windows   Win32, UI Automation                  net10.0-windows10.0.19041.0
  ContextSwitcher.App                      the interface, built once for each    both of them
  ContextSwitcher.Cli                      Windows' command line (stage 3)       net10.0-windows10.0.19041.0
  ContextSwitcher.Tests                    shared tests, and the macOS ones      net10.0
  ContextSwitcher.Tests.Windows            Windows tests                         net10.0-windows10.0.19041.0
```

- **The app is built twice from one project.** It lists both targets, and each brings in only its
  own platform:

  ```xml
  <TargetFrameworks>net10.0;net10.0-windows10.0.19041.0</TargetFrameworks>

  <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
    <ProjectReference Include="..\ContextSwitcher.Infrastructure.MacOS\ContextSwitcher.Infrastructure.MacOS.csproj" />
  </ItemGroup>
  <ItemGroup Condition="'$(TargetFramework)' != 'net10.0'">
    <ProjectReference Include="..\ContextSwitcher.Infrastructure.Windows\ContextSwitcher.Infrastructure.Windows.csproj" />
  </ItemGroup>
  ```

- **No `#if` in shared code.** Each platform project has a `PlatformServices.Register` of the same
  name in the same namespace, and `AppServices` calls it; which one runs is settled by the build. What
  the interface needs to know about the platform it asks the capabilities from stage 0 - "is there
  Focus?", never "is this a Mac?".
- **The compiler guards the line between them.** The platform projects are marked
  `[assembly: SupportedOSPlatform("macos")]` and `("windows")`. The platform compatibility analyzer
  (CA1416) then flags shared code that calls into one without checking the platform first, and with
  warnings as errors CI stops it.
- **Either computer compiles everything.** `EnableWindowsTargeting` in a `Directory.Build.props` lets
  a Mac build the Windows projects; without it the solution stops there (NETSDK1100). A mistake in
  the Windows code then shows on either machine, though its tests only run on Windows.
- **One version**, in `ContextSwitcher.App.csproj` as now, shared by both builds.

### What each download holds

- **The Mac app** is published with `-f net10.0 -r osx-arm64`. The Windows projects aren't compiled
  into it, and native libraries come only for macOS, so a Windows version adds nothing to it but its
  wording in `Strings.resx` - kilobytes. The bundle already holds `Avalonia.Win32.dll` and
  `Avalonia.X11.dll`, which `Avalonia.Desktop` brings along; they are small and stay as they are.
- **The Windows app** is published with `-f net10.0-windows10.0.19041.0 -r win-x64` (and `win-arm64`)
  and holds no macOS code. Its target brings in the Windows SDK projection
  (`Microsoft.Windows.SDK.NET.dll`), which is large until trimmed - a question for the Windows
  download only.
- [`build-app.sh`](../scripts/build-app.sh) names `--framework net10.0`: publishing a project with two
  targets needs one named (NETSDK1129).

### CI

| Job | Runner | What it does |
| --- | --- | --- |
| Build and test (macOS) | `macos-15` | builds the solution with warnings as errors; runs `ContextSwitcher.Tests` |
| Build and test (Windows) | `windows-latest` | the same, and runs `ContextSwitcher.Tests.Windows` too |
| App bundle | `macos-15` | as now |
| Windows package | `windows-latest` | from stage 1: builds the installer, installs it and starts the app |
| Workflows and scripts | `ubuntu-latest` | as now, and the PowerShell scripts through PSScriptAnalyzer |
| CodeQL | `macos-15` | as now; with `EnableWindowsTargeting` it reads the Windows code too |

- **Both build-and-test jobs are required** before a pull request merges. A change to shared code
  is then always tried on both systems, and a Windows change can't break the Mac.
- **Both always run.** Skipping the Windows job when only Mac files change is tempting, but a
  required check that a path filter skipped blocks the merge, and working round that costs more
  than it saves. Actions minutes are free for a public repository.
- **Dependabot** needs no change: it already watches the whole solution.
- **Scripts per platform**: `scripts/` keeps the macOS shell scripts; the Windows ones are
  PowerShell, in `scripts/windows/`.

### Versions and releases

How apps on more than one platform are numbered elsewhere:

| How | Who | What it costs |
| --- | --- | --- |
| One number, and every release for every platform | VS Code, Slack, Telegram Desktop | the simplest; but a release with nothing new for one platform still asks its people to update |
| One number, and a platform's build only when something changed for it | Chrome: one version, and a stable update can give Windows and Mac different last numbers (`.85/.86`) | each platform updates only when there's a reason; its numbers skip some |
| Separate products, separate numbers | Microsoft Office for Mac and for Windows; Telegram for macOS and Telegram Desktop | fits apps with separate code; with shared code, "Windows 0.3 or Mac 1.2?" only confuses |

ContextSwitcher takes the middle way:

- **One version number for both**, from the project, and one tag and one GitHub release per version.
- **A release carries the platforms that changed.** A change to shared code - Core, the interface,
  shared Infrastructure - goes to both; a change to one platform's code goes to that one. Whoever
  releases ticks the platforms when starting the release; both are ticked by default.
- **A platform's numbers may skip.** If 0.8.1 fixed something on the Mac only, Windows goes from 0.8.0
  to 0.8.2. Release notes say what is new since a platform's previous version, as "Coming from an
  older version?" already does, with a section for each platform.

The release workflow becomes three jobs:

```text
Release (started by hand: the version, and a tick for each platform)
  ├─ macOS     macos-15         build, test, sign, .dmg and .zip         if ticked
  ├─ Windows   windows-latest   build, test, installer and update feed    if ticked
  └─ Publish   ubuntu-latest    the tag, one release with what was built; "latest" only with Mac files
```

The checklist in [the release process](release-process.md) changes with it.

### Keeping the Mac's latest release

Three things on the Mac take GitHub's **latest** release and look in it for `ContextSwitcher.zip`:
the updater in the app ([`GitHubReleaseSource`](../src/ContextSwitcher.Infrastructure/Updates/GitHubReleaseSource.cs)),
[`install.sh`](../install.sh), and the Homebrew cask. If a release without Mac files became the
latest, all three would be left with nothing: the updater would find no zip, and `install.sh` and
Homebrew would download a 404. So:

1. **"Latest" is always the newest release with Mac files.** The publishing job marks a release latest
   only when it carries them (`gh release create --latest=false` otherwise). Without that, GitHub
   picks the newest release by itself.
2. **The Mac updater stops relying on it**, as a second line: it lists the newest releases and takes
   the highest version with the Mac's files in it - `ContextSwitcher.zip`, or a disk image.
   `install.sh` and the cask keep using "latest", which the first rule protects.
3. **The Windows updater never uses "latest"**: it takes the newest release with its own files.
   Velopack keeps a feed of its own for each platform; stage 1 checks that it passes over a release
   without Windows files.
4. **File names never overlap.** `ContextSwitcher.zip` and `ContextSwitcher-x.y.z.dmg` stay the
   Mac's; every Windows file has `win` in its name.
5. **Download counts tell the platforms apart.** [`collect-stats.py`](../scripts/collect-stats.py)
   counts every `.zip` as a Mac update; a Windows file ending in `.zip` would be counted with them.

## Stage 0 - Groundwork, on the Mac

**The aim.** Every macOS-only piece sits behind an interface, the solution builds and its tests pass
on macOS and on Windows, and Mac users see no difference. Worth doing even if Windows never comes.

1. **The tests on Windows** - done in
   [#76](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/76). The 17 that failed there
   ([what was tried](windows-port.md#tried-on-windows)) now pass or are skipped. `ProcessRunner`'s
   tests run `/bin/sh`, or PowerShell on Windows, through a runner with an allowlist of their own;
   the start-up smoke tests find `ContextSwitcher.exe` and, on Windows, name the app's folder
   outright; the path tests build what they expect with `Path.Combine`; and the four tests of macOS
   itself carry `[MacOSFact]`, which skips them anywhere else. When it merged, 2,004 passed on
   Windows with those 4 skipped, and all 2,008 ran on macOS.
2. **CI on Windows** - done in [#76](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/76).
   **Build and test (Windows)** runs on `windows-latest` next to **Build and test** on macOS: a
   Release build with warnings as errors, then the tests. Both summarise with
   `scripts/summarise-tests.py`. Making it a required check is a setting on GitHub (Settings →
   Branches) ([CI](#ci)).
3. **Seams** - done in [#83](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/83). Interfaces in Core for opening, quitting and
   looking for apps, browser tabs, media, Focus Shortcuts, and opening a link, a file or a system
   page; each step type has a handler of its own, and `BrowserLauncher` keeps only its decisions. The
   six view models that ran `open` use `IShell`. The macOS helpers the app called straight - sleep and
   lock, the Dock icon, the preferred languages, waiting for a display, the system's version, the
   popover's window - are behind interfaces too. The macOS versions are the code that was there, moved
   and not changed, and registered with the Mac's services - in the macOS project since item 6.
4. **How a profile names an app** ([decided](#decisions-made)) - done in
   [#84](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/84). Profiles keep listing apps by name,
   so every settings file reads as before, and the settings gain one map from a name to how Windows
   finds that app, filled in when it is picked:

   ```json
   "apps": {
     "Slack": { "windows_app_id": "com.squirrel.slack.slack", "windows_exe": "slack.exe" }
   }
   ```

   macOS ignores it. Settings brought over from a Mac have no such entries yet: Windows looks each
   name up among the Start menu's apps and says which it couldn't find.
5. **What each platform can do** - done in [#84](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/84). One place,
   `PlatformCapabilities`, says whether this machine has Focus (through Shortcuts), Apple Music, a
   Dock icon, and which browsers - so Safari, browser profiles and closing tabs by id. Profile Setup
   and Settings hide what it lacks, `AutomationPlanBuilder` plans no step for it, and importing
   settings says what this machine can't use. The Mac's is `MacPlatform.Capabilities`, which the
   macOS project's registrations choose with its allowed commands, `MacPlatform.AllowedCommands`.
6. **Projects** ([the projects](#the-projects)) - done in
   [#85](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/85). `ContextSwitcher.Infrastructure.MacOS`
   has `MacOS/`, `AppleScript/`, the updater and the rest of the macOS code, each in its folder, and
   the Mac's data, `MacPlatform`; `ContextSwitcher.Infrastructure.Windows` has only Windows' two
   choices so far, targeting `net10.0-windows10.0.19041.0`. The app targets `net10.0` and
   `net10.0-windows10.0.19041.0`, and registers the services of the platform it is built for through
   that project's `PlatformServices.Register`. The platform projects are marked for their system, so
   the compiler flags shared code that reaches into them, and `EnableWindowsTargeting`, in
   `Directory.Build.props`, lets a Mac compile it all. The macOS classes no longer check for macOS.
7. **Files** - done in [#78](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/78). Moving a
   new file over the old one - the settings, time, the log - goes through an `IFileReplacer` each
   class is given. `AppHost` gives Windows `FileReplacer.RetryingWhileHeldOpen`, which tries again
   for under a second while another process has either file open, and macOS `FileReplacer.Once`, as
   before. It also gives `ConfigPaths` Windows' settings root, so everything lives in
   `%APPDATA%\ContextSwitcher`, or `ContextSwitcher-dev` for a development build - not
   `%LOCALAPPDATA%`, where Velopack puts the app and which it replaces on each update. Since item 6
   both choices are each platform's registrations', the folder as a `SettingsRoot`.
8. **Releases** - done in [#79](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/79) ([why](#keeping-the-macs-latest-release)). The Mac
   updater no longer trusts "latest": `GitHubReleaseSource` takes the highest version among the
   newest releases that carry this platform's files, which it is given as data -
   `AppBundleUpdater.Assets`, the zip and the disk image, handed over in `AppHost`. The release
   workflow marks what it publishes the latest outright, since it always has the Mac's files.
   Starting the workflow by hand with a tick for each platform, and splitting it into a job per
   platform and a publishing job, wait for the Windows job in stage 1: with one platform there is
   nothing to choose.

**Done when** a Mac release built from it behaves exactly as before, and CI is green on both. All
eight items are merged, with CI green on both; the first Mac release built from them is the check
still to come.

## Stage 1 - Apps

**The aim.** Someone on Windows installs it, sets up Work and Personal, and switches: apps open,
close or minimise, and time is counted. Released as a preview.

The shell of the app:

- Built as `WinExe`, so no console window opens with it.
- The tray icon, in a light and a dark version that follow the taskbar's theme. A left click opens
  the dashboard, a right click the menu. The setup wizard says how to pin the icon, which Windows 11
  hides at first.
- The dashboard as a borderless, topmost window by the tray, on whichever edge the taskbar is, that
  hides when it loses focus.
- One copy only: a named mutex, and a pipe that tells the running copy to show its window.
- `Segoe UI Variable` first among the fonts.
- Everything not in this stage - the browser, Focus, music, Docker - hidden through the
  capabilities from stage 0.

Apps:

- **The picker**: apps from `shell:AppsFolder`, desktop and Store alike, with their icons
  (`IShellItemImageFactory`). The wizard suggests the usual Windows ones: Slack, Teams, Outlook,
  Visual Studio Code, Discord, Telegram, Steam, Spotify.
- **Launching** by AppUserModelID, or by the executable for an app that has none.
- **Closing**: `WM_CLOSE` to each of the app's top-level windows, then the same checking as on
  macOS.
- **"Still closing", a window on screen, Force quit**: `EnumWindows` over the app and its child
  processes, leaving out cloaked windows; `IVirtualDesktopManager` to tell a window on another
  desktop from a hidden one; `TerminateProcess` when the person clicks Force quit.
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
- **Releases**: an installer that updates itself (Velopack - [decided](#decisions-made)), for
  `win-x64` and `win-arm64`, unsigned and marked preview. It goes into the same GitHub releases as
  the Mac app whenever something changed for Windows
  ([versions and releases](#versions-and-releases)). The release workflow is now started by hand,
  with the version and a tick for each platform, and becomes a job per platform and a publishing job
  that gives "latest" only to a release with Mac files. Windows warns once before the first run, and
  the README says so. Download counts start telling Mac and Windows files apart.

What the app says, because Windows works that way: an app that goes to the tray when its window
closes - Slack, Teams, Discord, Telegram, Spotify - keeps running, and the card offers Force quit;
an app running as administrator can't be closed.

**Done when**, on a real Windows 11 PC (and on Windows 11 on Arm in a virtual machine): switching
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
- **Site icons** made with SkiaSharp instead of `sips`.
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

On 19 October 2026:

- **How a profile names an app on Windows**: the map from a name to its AppUserModelID and
  executable, [in stage 0](#stage-0---groundwork-on-the-mac). Rejected: turning the lists of names
  into lists of objects, a breaking change to `settings.json`; and the name alone, looked up in the
  Start menu at every switch, which is slow and fails when names repeat or change.
- **Where Windows keeps everything**: `%APPDATA%\ContextSwitcher`, where Windows apps keep their
  settings. Not `%LOCALAPPDATA%`, which Velopack replaces on each update, nor `~/.config` as on the
  Mac, where nobody on Windows looks.
- **Versions**: one number for both platforms, each release carrying the platforms that changed
  ([versions and releases](#versions-and-releases)).
- **How the preview ships**: Velopack on GitHub releases - quick and free, unsigned, with a warning
  on the first run. The Microsoft Store can come at [Windows 1.0](#stage-4---windows-10).

## Decisions to make

Before stage 1:

- **Windows 10**: Windows 11 only, or Windows 10 too. Windows 10's support has ended for most
  people.

Before stage 2:

- **The profile window's name**: the profile's name ("Work"), as the person sees it in the taskbar,
  or a longer one that no other window could have.

Before "later":

- **Whether to build the extension**, and whether it comes before Windows 1.0 or after.
