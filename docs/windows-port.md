# A Windows version

What each feature becomes on Windows, what Windows can't do, and why. The order to build it in, one
release at a time, and how far it has got, are in [the Windows plan](windows-plan.md).

Checked in October 2026 against 0.6.7. The solution was built and its tests run on Windows 11, the
app was started there, and the browser parts were tried on Chrome 155 and Edge 155 with throwaway
profiles.

## In short

It can be done without starting over. All of the profile, switching, time and logging logic carries
over unchanged, and nearly all of the interface. What has to be written again is the layer that talks
to macOS - AppleScript, the Objective-C calls, `open`, `sips`, `shortcuts`, `codesign` and the
updater - and that layer now sits behind interfaces, in a project of its own
([stage 0](windows-plan.md#stage-0---groundwork-done)). The solution builds and its tests pass on
Windows in CI.

Browser tabs are the hard part, but they are not lost. Windows browsers have nothing like
AppleScript, so a tab can't be listed with its address and closed by its id. What does work, tried
on Chrome and Edge: a profile opens its pages in a browser window of its own, named after it, and
closes that window when it is left. UI Automation reads the titles of the other tabs. Focus and Apple
Music can't be driven on Windows and are left out; Spotify works in part.

## The interface on Windows

Windows is Avalonia's first platform: windows, the Semi theme, the colour picker, the tray icon
with its menu and the headless test host all work there. Started on Windows 11, the setup wizard
opens and draws correctly, in Segoe UI with Windows' own title bar. What changes:

- **Font.** [`App.axaml`](../src/ContextSwitcher.App/App.axaml) asks for `.AppleSystemUIFont` and
  Helvetica; Windows falls back to Segoe UI, which looks right. `Segoe UI Variable` goes first on
  Windows.
- **A window program.** The app is built with `OutputType` `Exe`, so on Windows a console window
  opens with it. There it becomes `WinExe`, and the command line a program of its own: a Windows
  program with a window has no console to print to. It can't be called `contextswitcher.exe` next
  to `ContextSwitcher.exe`, since Windows file names ignore case.
- **Tray icon.** The menu bar icon is a template image macOS recolours for light and dark. Windows
  does not recolour tray icons, so it needs two `.ico` files and a switch when the taskbar's theme
  changes.
- **Clicks.** On Windows a left click on a tray icon is expected to open something and a right click
  the menu. Avalonia raises `TrayIcon.Clicked` for the left button, so the left click opens the
  dashboard and the right click the menu.
- **Hidden icons.** Windows 11 puts a new app's tray icon in the overflow, out of sight. Setup has
  to tell people to pin it; there is no supported way for an app to do it.
- **The dashboard.** On macOS
  [`MacPopoverWindow`](../src/ContextSwitcher.Infrastructure.MacOS/MacPopoverWindow.cs), behind
  `IPopoverWindow`, turns the popover into a non-activating panel, makes it follow the active Space,
  and tells the dashboard about the frontmost app and the mouse so it knows when to close. Windows
  needs a simpler one: a borderless, topmost window with no taskbar button that hides when it is
  deactivated behaves as a tray flyout should.
- **Where the dashboard opens.** `PositionNearMenuBar` in
  [`App.axaml.cs`](../src/ContextSwitcher.App/App.axaml.cs) always places it under the top right
  corner. On Windows it belongs next to the tray, on whichever edge the taskbar is - the side where
  the screen's working area is smaller than its bounds.
- **Only one copy.** Opening the app again on macOS brings the running copy forward, and a click on
  the Dock icon reaches it as a Reopen. Windows starts as many copies as are launched, each with its
  own tray icon. The app needs a named mutex, and a pipe to tell the running copy to show its window.
- **Text.** About 68 of the 443 strings name something only macOS has: the Mac, the Dock, the menu
  bar, Shortcuts, System Settings, Automation, Safari, Focus, ⌘ and Option. Each needs a Windows
  wording, in English and Ukrainian. The setup wizard, for one, says "your Mac".
- **Build.** `RuntimeIdentifiers` adds `win-x64` and `win-arm64` to `osx-arm64`.
  `MacOSPlatformOptions` is ignored on Windows; the software-rendering switch goes through
  `Win32PlatformOptions` there.
- **Testing by hand.** CI can't tell whether closing Slack actually works. That needs a Windows PC,
  or Windows 11 on Arm in Parallels or UTM on a Mac, running the `win-arm64` build.

## Feature by feature

| Feature | macOS today | Windows | Verdict |
| --- | --- | --- | --- |
| Launching apps | `open -a` | the Start menu shortcut, or the app's AppUserModelID through `shell:AppsFolder` for Store apps | write again |
| The app picker and its icons | bundles in `/Applications`, icons through `sips` | `shell:AppsFolder`, which lists desktop and Store apps alike; icons from the Shell (`IShellItemImageFactory`) | write again |
| Closing apps | AppleScript `quit`, then checking it went | `WM_CLOSE` to each of the process's top-level windows, then the same checking | write again; weaker (below) |
| Still closing, a window on screen, Force quit | `CGWindowListCopyWindowInfo` over the app and its child processes; `forceTerminate` | `EnumWindows`, visible and not cloaked; child processes from a Toolhelp snapshot; `TerminateProcess` | write again; better - `IVirtualDesktopManager` tells a window on another desktop from a hidden one, which macOS can't |
| Hiding apps | `NSRunningApplication.hide` | minimising the process's windows; entering a profile restores them rather than launching again | write again; small |
| Running apps (asking on a switch) | `NSWorkspace.runningApplications` | visible, unowned, uncloaked top-level windows and their processes | write again |
| Opening pages | `open`, `open -na … --args --profile-directory=` | the shell for the default browser; `chrome.exe --profile-directory=…`, found through `App Paths` | nearly as is |
| Chrome and Brave profiles | `Local State` under `~/Library/Application Support` | the same file under `%LOCALAPPDATA%` | path only; add **Edge** |
| Closing a whole browser profile | - | each profile's windows have their own AppUserModelID, so all of one profile's windows can be found and closed | new on Windows |
| Safari | AppleScript | not on Windows | leave out |
| Closing tabs, focusing an open tab, avoiding duplicates, tabs in the ask-on-switch list | AppleScript | a window per profile, and UI Automation to read the rest (below) | written differently; some precision lost |
| Focus | a Shortcut per mode | Do Not Disturb has no API. Focus sessions have one (`FocusSessionManager`), but turning them on and off is a Limited Access Feature: an app needs a token from Microsoft | leave out, unless Microsoft grants the token |
| Apple Music | AppleScript | Apple Music for Windows exists, but can't be driven or asked for its playlists | leave out |
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
| Command line, Siri and Shortcuts | the app itself; the Shortcuts app | a separate console program; a global hotkey in the app itself (`RegisterHotKey`) | small |

Two limits of Windows to know before promising anything:

- **Closing an app means closing its windows.** Slack, Teams, Discord, Telegram and Spotify go to the
  tray when their window closes and keep running. Ending the process instead loses unsaved work, so
  it is never the default.
- **An app running as administrator cannot be closed** by one that isn't. Windows blocks the message.

## Browser tabs

On macOS every tab feature comes from AppleScript
([`AppleScriptBuilder`](../src/ContextSwitcher.Infrastructure.MacOS/AppleScript/AppleScriptBuilder.cs)):
listing tabs with their ids and addresses, closing by id, bringing one forward. On Windows no browser
can be scripted like that - no AppleScript, no COM. Below is every way found to get at tabs without
an extension, what was tried, and what to build.

### What was tried

On Windows 11, Chrome 155 and Edge 155, each on a throwaway profile in a temporary folder, with
three or four test pages open:

| | Chrome 155 | Edge 155 |
| --- | --- | --- |
| `--new-window --window-name="CS Work"` gives the window a fixed title, whatever page it shows | ✅ | ✅ |
| The name survives a restart with the last session restored | ✅ | not tried |
| `WM_CLOSE` to that window closes it and its tabs, and no other window | ✅ | ✅ |
| The window's AppUserModelID says which browser profile it belongs to (`….Default`, `….Profile2`) | ✅ | ✅ |
| UI Automation lists a window's tabs | ✅ 3 tabs in 21 ms - titles only | ✅ titles only |
| UI Automation reads the address | of the active tab only, shortened (`example.com`) | of the active tab only, in full |
| Addresses of every tab through UI Automation | only by switching to each tab on screen, about 170 ms a tab | the same |
| Closing one tab with its Close button through UI Automation | ✅ | ✅ |
| UI Automation with the window minimised | ✅ | ❌ sees no tabs |
| Reading the session files (`Sessions\Tabs_…`) while the browser runs | ❌ locked for every kind of sharing, and can't be copied | ❌ the same |
| The DevTools protocol on a profile in a folder of its own | ✅ every tab with its id and full address in 53 ms; closing by id | not tried; the same engine |

### Every way in, without an extension

- **A window per profile.** A profile opens its pages in a new window named after it
  (`--new-window --window-name=Work`). The window is found again by its title and AppUserModelID.
  Leaving the profile closes it with `WM_CLOSE`; entering again brings it forward instead of
  opening the pages a second time. No permission, nothing flickers, and little to break when the
  browser updates: these are ordinary Windows windows. What it costs: the whole window closes,
  including tabs the person opened in it, and a tab dragged out to another window is no longer the
  profile's. A page asking "Leave site?" stops the close, as on macOS.
- **UI Automation** - the interface screen readers use. It gives every tab's title at once, each
  tab's Close button, selecting a tab, and the address of the active one. The addresses of the
  others are only reached by switching to each on screen, which wakes tabs the browser put to sleep
  (Memory Saver) and makes them load again. Titles are not addresses: two "Example Domain" tabs can't
  be told apart, and Chrome adds "- Memory usage - …" to the active tab's name. Element names are in
  the browser's language, Edge shows nothing while minimised, and a browser update can change the
  tree. Good for reading and offering, not for exact work.
- **The DevTools protocol, on a browser profile of the app's own** (`--user-data-dir` in the app's
  folder, with `--remote-debugging-pipe` or `--remote-debugging-port`). Everything AppleScript gives
  on macOS: ids, addresses, closing, bringing a tab forward. But it works only for a browser profile
  kept in a folder of the app's: since Chrome 136 the switches are ignored for the person's usual
  one. The person signs in to it once, and sync brings back bookmarks and passwords; it shows as a
  separate browser in the taskbar. While a port is open any program on the computer can drive that
  browser, cookies and all; a pipe leaves only the app that started the browser able to, and only
  while it runs.
- **The DevTools protocol, on the person's own Chrome.** From Chrome 144 a person can turn on remote
  debugging at `chrome://inspect/#remote-debugging`. Chrome then asks "Allow remote debugging?" on
  every connection - by design - and shows "Chrome is being controlled by automated test software"
  while one is open. Whether the setting survives a restart isn't documented; Edge and Brave are
  unknown. A dialog on every switch rules it out for an app that works in the background.
- **The browser's session files.** Chrome and Edge keep them locked the whole time they run (tried).
  Firefox's `sessionstore-backups/recovery.jsonlz4` can be read while it runs and is rewritten about
  every 15 seconds: every tab with its address. Nothing can be closed through it.
- **Pressing keys** (Ctrl+L and Ctrl+C to read an address, Ctrl+W to close, Ctrl+Tab to move on).
  It takes the focus and the clipboard, happens on screen, and closes the wrong tab as easily as the
  right one. The worst of them.
- **An extension the app installs itself.** For completeness, since it is an extension: an installer
  can register one in `HKLM\Software\Google\Chrome\Extensions`, and Chrome asks once whether to turn
  it on. The extension has to be in the Chrome Web Store, and the installer needs administrator
  rights.

### What to build

1. **The window per profile** for Chrome, Edge and Brave: it covers opening, not duplicating and
   closing a profile's pages, simply and reliably.
2. **UI Automation for reading only**: the titles of tabs outside the profile window for the
   ask-on-switch list, closing one of them by its Close button when the person picks it, and
   "From open tabs" walking the tabs when the person asks for it.
3. **Optionally, a browser profile of the app's own** driven through DevTools, for people who want
   exact control.
4. **Later, if wanted, an extension of our own** with a native messaging host (Chrome, Edge and
   Brave, and Firefox if wanted). It is the only way to get every tab's id and address in the
   person's own browser, and it also brings tab groups, on macOS too
   ([browser control](browser-control.md)). It costs a separate JavaScript project, publishing in the
   Chrome Web Store (a one-time registration fee) and Edge Add-ons, and people installing it.

What that means for each feature, without an extension:

| Feature on macOS | On Windows |
| --- | --- |
| Opening a profile's pages | ✅ |
| Not opening a page twice | ✅ in the profile window; ⚠️ in other windows only by title |
| Closing a profile's pages on leaving | ✅ the profile window closes; ⚠️ single tabs in other windows only by title |
| Bringing an open tab forward | ⚠️ by title, through UI Automation |
| "From open tabs" in Profile Setup | ⚠️ titles at once; addresses by switching through the tabs |
| Tabs in the ask-on-switch list | ⚠️ titles, closed with their Close button; no addresses |
| Closing a whole browser profile | ✅ its windows, by AppUserModelID |
| Exact control, as with AppleScript | ✅ only in a browser profile of the app's own |

## Shipping it

- **Unsigned**, Windows shows "Windows protected your PC" before the first run, and antivirus is
  wary of unsigned programs that list processes and close windows.
- **The Microsoft Store** is the best channel for a finished version. Since September 2025
  individual developers register for free, and the Store signs the package and updates it
  ([announcement](https://blogs.windows.com/windowsdeveloper/2025/09/10/free-developer-registration-for-individual-developers-on-microsoft-store/)).
  An MSIX package redirects writes to `AppData`, so the app and the command line must be in the same
  package.
- **Azure Artifact Signing** (about $9.99 a month) is open to individuals only in the USA and Canada
  ([code signing options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)).
- **GitHub releases** can use [Velopack](https://docs.velopack.io/packaging/operating-systems/windows),
  which builds the installer and does updates. It installs into `%LocalAppData%` and replaces the whole
  app folder on update, which is fine: settings live in `%APPDATA%`. It supports macOS too, so it could
  later replace [`AppBundleUpdater`](../src/ContextSwitcher.Infrastructure.MacOS/Updates/AppBundleUpdater.cs).
  This is how the preview ships ([decided](windows-plan.md#decisions-made)); the Store can come at
  Windows 1.0.

## Left out on Windows

Focus (unless Microsoft grants the token), Apple Music, Safari, the Dock icon setting and the
Automation permission section.

Changed on Windows: a profile's pages live in a browser window of its own. Without an extension, a
page moved to another window is not closed with the profile, and a duplicate in another window is
found only by its title.

Added on Windows: Edge, closing a whole browser profile, telling a window on another desktop from a
hidden one, and a global hotkey.

## Sources

- [Changes to remote debugging switches to improve security](https://developer.chrome.com/blog/remote-debugging-port) - Chrome 136
- [Let your coding agent debug your browser session](https://developer.chrome.com/blog/chrome-devtools-mcp-debug-your-browser-session) - the `chrome://inspect` setting, Chrome 144
- [chrome-devtools-mcp #1794](https://github.com/ChromeDevTools/chrome-devtools-mcp/issues/1794) - a dialog for every connection, by design
- [Windows shortcut and taskbar handling](https://chromium.googlesource.com/chromium/src/+/main/docs/windows_shortcut_and_taskbar_handling.md) - Chrome's AppUserModelID for each profile
- [Alternative extension installation](https://developer.chrome.com/docs/extensions/how-to/distribute/install-extensions) - registering an extension in the registry
- [Decoding Firefox session store data](https://blog.dend.ro/decoding-firefox-session-store-data/) and [Session Store Utilities](https://firefox-source-docs.mozilla.org/toolkit/components/sessionstore/utils.html) - Firefox's `recovery.jsonlz4`
- [FocusSessionManager.TryStartFocusSession](https://learn.microsoft.com/en-us/uwp/api/windows.ui.shell.focussessionmanager.trystartfocussession) - a Limited Access Feature
- [IVirtualDesktopManager::IsWindowOnCurrentVirtualDesktop](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ivirtualdesktopmanager-iswindowoncurrentvirtualdesktop)
