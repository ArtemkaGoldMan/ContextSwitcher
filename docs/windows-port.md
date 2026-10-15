# A Windows version

A plan, not work in progress: nothing here is built yet. It was written against 0.6.3 by reading
the code and checking what Windows offers; none of it has been tried on a Windows machine.

## In short

It can be done without starting over. Avalonia runs natively on Windows, and about four fifths of
the code carries over: all of the profile, switching, time and logging logic, and nearly all of the
interface. What has to be written again is the layer that talks to macOS - AppleScript, the
Objective-C calls, `open`, `sips`, `codesign` and the updater.

The real loss is browser tabs. Closing a profile's tabs, finding a page that is already open, and
listing tabs to ask about all go through AppleScript, and Windows has nothing like it. Without a
browser extension of our own, a Windows version opens pages but cannot close them. Focus and Apple
Music have no Windows equivalent and are left out.

## What carries over

| Part | Size | On Windows |
| --- | --- | --- |
| `ContextSwitcher.Core` - profiles, the switch plan, validation, time tracking, logging, messages | 4,000 lines | unchanged |
| `ContextSwitcher.App` - every page, view model, style, the en/uk text, stats, reordering | 8,400 lines | about 90% unchanged |
| `ContextSwitcher.Infrastructure`, the shared part - JSON files, the log, GitHub releases, the command line, Docker, running processes | about 2,200 lines | unchanged, apart from file moves (below) |
| `ContextSwitcher.Infrastructure`, the macOS part - `MacOS/`, `AppleScript/`, most of `Browser/`, the updater, app discovery | about 4,000 lines | written again |

About 4,000 of the 18,600 lines outside the tests - a little over a fifth - are tied to macOS.

## The interface on Windows

Windows is Avalonia's first platform: windows, the Semi theme, the colour picker, the tray icon
with its menu and the headless test host all work there. What changes:

- **Font.** [`App.axaml`](../src/ContextSwitcher.App/App.axaml) asks for `.AppleSystemUIFont`;
  `Segoe UI Variable` goes in front of it on Windows.
- **Tray icon.** The menu bar icon is a template image macOS recolours for light and dark. Windows
  does not recolour tray icons, so it needs two `.ico` files and a switch when the taskbar's theme
  changes.
- **Clicks.** On Windows a left click on a tray icon is expected to open something and a right click
  the menu. Avalonia raises `TrayIcon.Clicked` for the left button, so the left click opens the
  dashboard and the right click the menu.
- **Hidden icons.** Windows 11 puts a new app's tray icon in the overflow, out of sight. Setup has
  to tell people to pin it; there is no supported way for an app to do it.
- **The dashboard.** [`DashboardWindow.axaml.cs`](../src/ContextSwitcher.App/Views/DashboardWindow.axaml.cs)
  turns the popover into a non-activating panel, makes it follow the active Space, and watches the
  frontmost app and the mouse to know when to close (`MenuBarPanel`, `WindowSpaces`,
  `FrontmostApplication`, `OutsideClick`). None of that is needed on Windows: a borderless,
  topmost window with no taskbar button that hides when it is deactivated behaves as a tray flyout
  should.
- **Where the dashboard opens.** `PositionNearMenuBar` in
  [`App.axaml.cs`](../src/ContextSwitcher.App/App.axaml.cs) always places it under the top right
  corner. On Windows it belongs next to the tray, on whichever edge the taskbar is - the side where
  the screen's working area is smaller than its bounds.
- **Only one copy.** Opening the app again on macOS brings the running copy forward, and a click on
  the Dock icon reaches it as a Reopen. Windows starts as many copies as are launched, each with its
  own tray icon. The app needs a named mutex, and a pipe to tell the running copy to show
  its window.
- **Text.** 65 of the 424 strings name something only macOS has: the Dock, Shortcuts, System
  Settings, Automation, Safari, ⌘. Each needs a Windows wording, in English and Ukrainian.
- **Build.** `RuntimeIdentifiers` adds `win-x64` and `win-arm64`. `MacOSPlatformOptions` is ignored
  on Windows; the software-rendering switch goes through `Win32PlatformOptions` there.

## Feature by feature

| Feature | macOS today | Windows | Verdict |
| --- | --- | --- | --- |
| Launching apps | `open -a` | the Start menu shortcut, or the app's AppUserModelID through `shell:AppsFolder` for Store apps | write again |
| The app picker and its icons | bundles in `/Applications`, icons through `sips` | `shell:AppsFolder`, which lists desktop and Store apps alike; icons from the Shell (`IShellItemImageFactory`) | write again |
| Closing apps | AppleScript `quit`, then checking it went | `WM_CLOSE` to each of the process's top-level windows, then the same checking | write again; weaker (below) |
| Hiding apps | `NSRunningApplication.hide` | minimising the process's windows; entering a profile restores them rather than launching again | write again; small |
| Running apps (asking on a switch) | `NSWorkspace.runningApplications` | visible, unowned, uncloaked top-level windows and their processes | write again |
| Opening pages | `open`, `open -na … --args --profile-directory=` | the shell for the default browser; `chrome.exe --profile-directory=…`, found through `App Paths` | nearly as is |
| Chrome and Brave profiles | `Local State` under `~/Library/Application Support` | the same file under `%LOCALAPPDATA%` | path only; add **Edge** |
| Safari | AppleScript | not on Windows | leave out |
| Closing tabs, focusing an open tab, avoiding duplicates, tabs in the ask-on-switch list | AppleScript | only through a browser extension (below) | leave out at first |
| Focus | a Shortcut per mode | no supported way to turn Do Not Disturb on or off | leave out |
| Apple Music | AppleScript | nothing to drive | leave out |
| Spotify | AppleScript `play` | open the `spotify:` link, then Play through Windows' media session controls | partly: the playlist opens; playing exactly it needs Spotify's Web API (sign-in and Premium) |
| Docker | `docker` | `docker.exe` | as is |
| Time away (sleep, wake, lock, another user) | NSWorkspace and distributed notifications | `SystemEvents.PowerModeChanged` and `SessionSwitch` | small |
| Notifications | `UNUserNotificationCenter` | Windows toast notifications, which need an AppUserModelID - given by an MSIX package, or by a Start menu shortcut | write again |
| Open at login | `SMAppService` | the `Run` registry key (and `StartupApproved` for one turned off in Task Manager), or a `StartupTask` in an MSIX package | small |
| The default browser | `NSWorkspace.URLForApplicationToOpenURL:` | the `UserChoice` ProgId for `https` | small |
| The system language | `NSLocale.preferredLanguages` | `CultureInfo.CurrentUICulture` | small |
| Automation permission, the Dock icon, waiting for a display | macOS only | not needed | take out of the interface |
| Site icons | downloaded, converted by `sips` | SkiaSharp, which Avalonia already ships - on macOS too | small |
| Updates | `ditto`, `plutil`, `codesign` and a shell script | Velopack, or the Microsoft Store | write again |
| Command line, Siri and Shortcuts | the app itself; the Shortcuts app | a separate console program; a hotkey, PowerToys or AutoHotkey run it | small |

Two limits of Windows to know before promising anything:

- **Closing an app means closing its windows.** Slack, Teams, Discord, Telegram and Spotify go to the
  tray when their window closes and keep running. Ending the process instead loses unsaved work, so
  it is never the default.
- **An app running as administrator cannot be closed** by one that isn't. Windows blocks the message.

## Browser tabs

On macOS every tab feature comes from AppleScript
([`AppleScriptBuilder`](../src/ContextSwitcher.Infrastructure/AppleScript/AppleScriptBuilder.cs)):
listing tabs with their ids, closing by id, bringing one forward. On Windows there are three ways
to get at tabs:

- **Chrome's DevTools protocol.** From Chrome 136 the remote-debugging switches are ignored for the
  default user data directory, so it no longer reaches a person's real profile.
  ([Chrome's announcement](https://developer.chrome.com/blog/remote-debugging-port))
- **UI Automation.** It sees tab titles, but the address of the active tab only, and depends on the
  browser's version and language. Not dependable enough to close the right tabs.
- **An extension of our own, with a native messaging host.** The extension (Chrome, Edge and Brave,
  and Firefox if wanted) gets full access to tabs, their ids and **tab groups**; the host is a small
  program the app registers so the extension and the app can talk. It costs a separate JavaScript
  project, publishing in the Chrome Web Store (a one-time registration fee) and Edge Add-ons, and
  people installing it.

The extension is the only real option. It also works on macOS, where `chrome.tabGroups` would bring
back the tab groups AppleScript cannot reach ([browser control](browser-control.md)).

## What the code needs first

These come before any Windows code, and none of them changes what the Mac app does.

1. **Seams where AppleScript is wired in.**
   [`AutomationStepExecutor`](../src/ContextSwitcher.Infrastructure/Automation/AutomationStepExecutor.cs)
   quits apps through AppleScript, launches them with `open -a`, plays Music and Spotify through
   AppleScript and runs Focus Shortcuts with `shortcuts run`, and defaults to `MacApplicationHider`.
   [`BrowserLauncher`](../src/ContextSwitcher.Infrastructure/Browser/BrowserLauncher.cs) builds its
   AppleScript directly. They need interfaces of their own: launching and closing apps, browser
   tabs, media, Focus. Six view models also run `open` themselves - to open a link, show the log in
   Finder, open Shortcuts - and need one for opening a link or showing a file.

   The rest is already behind interfaces in `Core/Abstractions` and only needs a Windows
   registration in [`AppHost`](../src/ContextSwitcher.App/Startup/AppHost.cs): `IApplicationHider`,
   `IRunningApplications`, `ILoginItem`, `ISystemNotifications`, `IDefaultBrowser`, `IAppUpdater`,
   `ISystemCatalog`, `IInstalledAppsService`, `IPermissionsChecker`.

2. **How a profile names an app.** A profile stores the app's bundle name - "Visual Studio Code" -
   which both `open -a` and AppleScript understand. On Windows the Start menu name is not the
   process (`Code.exe`), and Store apps are known by an AppUserModelID. Next to the name, a profile
   needs what the app was picked as: its AppUserModelID, and its executable to recognise it running.
   A new optional field reads old files unchanged. **This is the decision to make before anything
   else.**

3. **What each platform can do.** Profile Setup hides what the platform lacks - Focus, Apple Music,
   Safari, closing tabs - and `AutomationPlanBuilder` plans no step for it. A profile imported from
   a Mac says which of its apps and settings this machine cannot use, rather than failing at switch
   time.

4. **Project layout.** `ContextSwitcher.Infrastructure` keeps what is shared;
   `ContextSwitcher.Infrastructure.MacOS` and `ContextSwitcher.Infrastructure.Windows` hold the rest.
   The Windows project targets `net10.0-windows10.0.19041.0`, which toast notifications and the media
   session controls need. One repository, one branch.

5. **The command line.** A Windows program with a window has no console, so `ContextSwitcher.exe
   switch work` would print nothing. The command line becomes a small console program of its own -
   [`CliCommandRouter`](../src/ContextSwitcher.Infrastructure/Cli/CliCommandRouter.cs) already lives in
   Infrastructure, so it is a thin `Main`. It cannot be called `contextswitcher.exe` next to
   `ContextSwitcher.exe`: Windows file names ignore case.

6. **Files.** Settings move to `%APPDATA%\ContextSwitcher` instead of `~/.config/ContextSwitcher`.
   More important: on Windows `File.Move(…, overwrite: true)` fails while another process has the
   file open - the app and the command line at once, an antivirus, the search indexer. That is how
   [`JsonFileStore`](../src/ContextSwitcher.Infrastructure/Files/JsonFileStore.cs) and
   `AnalyticsSessionStore` save, and how
   [`LocalJsonLogger`](../src/ContextSwitcher.Infrastructure/Logging/LocalJsonLogger.cs) rolls its log;
   each needs a short retry.

## Tests and CI

- There are 641 test methods, 1,981 cases. About four fifths - Core, view models, the interface,
  logging, time tracking, configuration, the invariants - should pass on Windows with no change to
  what they check. About 100-130 (AppleScript, `MacOS/`, parts of browser, updates and the catalog)
  are macOS only and get marked so.
- CI gains a `windows-latest` job that builds and runs the tests, and the release workflow a Windows
  build.
- CI cannot tell whether closing Slack actually works. That needs a Windows PC, or Windows 11 on Arm
  in Parallels or UTM on a Mac, running the `win-arm64` build.

## Shipping it

- **Unsigned**, Windows shows "Windows protected your PC" before the first run, and antivirus is
  wary of unsigned programs that list processes and close windows.
- **The Microsoft Store** is the best first channel. Since September 2025 individual developers
  register for free, and the Store signs the package and updates it
  ([announcement](https://blogs.windows.com/windowsdeveloper/2025/09/10/free-developer-registration-for-individual-developers-on-microsoft-store/)).
  An MSIX package redirects writes to `AppData`, so the app and the command line must be in the same
  package.
- **Azure Artifact Signing** (about $9.99 a month) is open to individuals only in the USA and Canada
  ([code signing options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)).
- **GitHub releases** can use [Velopack](https://docs.velopack.io/packaging/operating-systems/windows),
  which builds the installer and does updates. It installs into `%LocalAppData%` and replaces the whole
  app folder on update, which is fine: settings live elsewhere. It supports macOS too, so it could
  later replace [`AppBundleUpdater`](../src/ContextSwitcher.Infrastructure/Updates/AppBundleUpdater.cs).

## The plan

1. **On the Mac, with no change in behaviour.** The seams, the app field in profiles, the
   platform's capabilities, the split of Infrastructure. Worth doing even if Windows never comes.
2. **A first Windows version.** Tray and dashboard, profiles, launching, closing and minimising
   apps, opening pages in Chrome, Edge and Brave with a profile, Docker, time tracking, open at
   login, the command line, one copy only.
3. **Shipping.** Notifications, the Store package or Velopack, CI, the Windows wording of the text.
4. **Later, if wanted.** Spotify, the browser extension (tabs on Windows, tab groups on both), a
   global hotkey.

Roughly 3,000-4,000 lines of new platform code and their tests. Core does not change; the interface
gets small, local edits.

## Left out on Windows

Focus, Apple Music, Safari, the Dock icon setting and the Automation permission section - and,
until there is an extension, closing tabs, finding an open tab, avoiding duplicate tabs and tabs in
the ask-on-switch list.

Added on Windows: Edge, and possibly a global hotkey.

## Decisions to make

- How a profile stores an app: the AppUserModelID and executable alongside the name, as above, or
  something else.
- Store first, or GitHub releases with Velopack first, or both.
- Whether to build the browser extension, and whether it comes before or after the first Windows
  version.
- Firefox: opening pages is easy; tabs would need the extension's Firefox build.
