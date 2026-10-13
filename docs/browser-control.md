# Controlling Chrome and Safari from another app

What another macOS app can do to Chrome and Safari: their tabs, their tab groups and their
profiles (the separate signed-in people). This is the reference for ContextSwitcher's browser
features and for any new ones. It covers what ContextSwitcher already uses, what could be used
later, and what can't be done without a workaround.

Checked in October 2026 against Chrome 155 and Safari 26.6 on macOS 26.6. Sources are listed at the
end. Check again before building on any of it: browsers change, and some of this is undocumented.

## Summary

| | Tabs | Tab groups | Profiles |
| --- | --- | --- | --- |
| **Chrome / Brave** | ✅ AppleScript | ❌ Only through a browser extension | ✅ Launch switches |
| **Safari** | ✅ AppleScript | ✅ Shortcuts actions, or a Focus filter | ⚠️ Only a Focus filter |

What ContextSwitcher does today:
- **Pages** in any browser: opens them, and checks for duplicates and closes them on leaving in
  Chrome, Brave and Safari. In Chrome and Brave it also remembers each tab it opened for a page that
  closes, by the tab's `id`, and closes it by that id on leaving, wherever the page has gone.
- **Chrome and Brave profiles** with `--profile-directory`.
- **Safari's tab groups and profiles**: it doesn't open them itself. Profile Setup explains how a
  Focus filter does it.

Tab groups mode was removed. It tried AppleScript commands that don't exist in either browser, so
it never worked for anyone.

## Chrome (and Brave, which shares Chromium's scripting)

### AppleScript

The dictionary is `Google Chrome.app/Contents/Resources/scripting.sdef`. Everything it has:

- **Application:** `windows`; `name`, `frontmost`, `version`; `bookmarks bar` and `other bookmarks`.
- **Window:**
  - `tabs`;
  - `id`, `index`, `bounds`, `visible`, `minimized`, `zoomed`;
  - `name` (the title) and `given name`, which is settable and gives the window its own title, like
    *Name Window…* in Chrome's Window menu;
  - `active tab` and `active tab index` (settable);
  - `mode`: `normal` or `incognito`, settable only when the window is created.
- **Tab:** `id`, `title`, `URL` (settable, which navigates), `loading`.
- **Bookmarks:** folders and items, with `title`, `URL` and `index`.
- **Commands:**
  - `make` (new window or tab), `close`, `move`, `duplicate`, `delete`, `count`, `exists`;
  - `reload`, `go back`, `go forward`, `stop`, `view source`;
  - `select all`, `cut selection`, `copy selection`, `paste selection`, `undo`, `redo`;
  - `save`, `print`;
  - `execute javascript`, which runs JavaScript in a tab's page.

Not in the dictionary: tab groups, profiles, and which profile a window belongs to. AppleScript sees
the windows of every open profile, including installed web apps' windows, and can't tell them apart.

A tab's `id` is its `SessionID`. It stays the same wherever the tab navigates, but it is only unique
within one run of the browser, which numbers its tabs afresh when it starts. Each new tab gets a
higher id than the last. ContextSwitcher relies on all three facts (`ProcessBrowserRuns`,
`BrowserLauncher`).

Scripting a browser that isn't running starts it. ContextSwitcher's scripts check
`application "…" is running` first wherever starting the browser would be wrong.

### Launch switches

These are passed with `open -na "Google Chrome" --args …`. Checked in Chromium's
`chrome/common/chrome_switches.h`:

| Switch | Does |
| --- | --- |
| `--profile-directory="Profile 2"` | Opens in that profile (folder name; `chrome://version` shows it). |
| `--profile-email=a@b.com` | The same, chosen by the signed-in email. |
| `--new-window` | Opens the URLs in a new window. |
| `--window-name="Work"` | Gives the new window a title, the same as `given name`. |
| `--window-position=x,y`, `--window-size=w,h` | Places the new window. |
| `--incognito`, `--guest` | Opens an incognito or guest window. |
| `--app=URL` | Opens the URL as an app window, without the tab strip. |
| `--restore-last-session` | Restores the previous session. |

No switch opens or restores a tab group.

### What else exists

- **Shortcuts:** Chrome for Mac has no Shortcuts actions: there are no App Intents in its bundle.
- **`chrome://tab-group-home`:** an internal page that Chromium is still building. It can't be used
  to open a group.
- **Remote debugging (DevTools protocol):** since Chrome 136, `--remote-debugging-port` and
  `--remote-debugging-pipe` are ignored for the user's normal profile, for security. They only work
  with a separate `--user-data-dir`. The protocol has no tab group commands anyway.
- **Saved tab groups** sync with the Google account. They aren't stored as bookmarks that
  AppleScript or the `Bookmarks` file could read, and no public interface reaches them.
- **Enterprise policies** set behaviour. They can't open anything.

### The one way to tab groups: an extension

A Chrome extension can create real groups and control them:
- `chrome.tabs.group` makes a group from tabs;
- `chrome.tabGroups.update` sets its title, colour and whether it's collapsed;
- `chrome.tabs.remove` closes its tabs;
- `chrome.tabGroups.query` finds open groups by title.

The limit: an extension sees only groups open in a window. A saved group that is closed is out of
reach. So ContextSwitcher could create a "Generation" group from its own list of pages, but it
couldn't reopen the user's saved "Generation" group.

What it would take:
- writing the extension;
- a native messaging host, so the app can talk to it;
- every user installing the extension;
- publishing it on the Chrome Web Store, which needs a one-off $5 developer registration. Without
  the store, it can only be loaded unpacked in developer mode.

## Safari

### AppleScript

The dictionary is `Safari.app/Contents/Resources/Safari.sdef` (Safari.app lives in
`/System/Cryptexes/App/System/Applications/`). It has:

- the standard windows and documents;
- **Window:** `tabs`, `current tab` (settable);
- **Tab:** `URL` (settable), `name`, `index`, `visible`, `source`, `text`, `pid`;
- **Commands:** `do JavaScript`, `add reading list item`, `search the web`, `show bookmarks`,
  `email contents`, `show privacy report`.

Not in the dictionary: tab groups and profiles. A window's title begins with the name of the tab
group it shows. That is a convention, not an interface.

### Shortcuts actions

Safari's App Intents extension is `Safari.app/Contents/Extensions/SafariLinkExtension.appex`, built
for macOS. Its actions:

- **Tab groups:**
  - Open Tab Group;
  - New Empty Tab Group;
  - Move Tabs to Tab Group;
  - Delete Tab Groups.
- **Windows:** Show Window, Close Windows, Move Tabs to Window.
- **Tabs:**
  - New Tab, which can be private;
  - Open Tab, Close Tab, Close Tabs;
  - Load URL in Tab, Search Tabs, Find on Page.
- **Bookmarks:** Add Bookmark, Bookmark Tab, Open Bookmark, Delete Bookmarks.
- **Views:** Open View and Close View, for the sidebar, Start Page, reading list, history and so on.

The entities it works with:

| Entity | Has |
| --- | --- |
| Tab | Title, URL |
| Window | Title, Tabs, Private Browsing |
| Tab Group | Title |

There is no action to open a profile.

ContextSwitcher already creates and runs Shortcuts: Focus works that way
([shortcuts-integration.md](shortcuts-integration.md)). So "open Safari tab group X" could be done
the same way. To generate such a shortcut, ContextSwitcher needs the action's identifier and the
shape of its parameter. Both come from exporting a shortcut that uses Open Tab Group. Open Tab Group
has not yet been run from `shortcuts run` to confirm it works without the Shortcuts app in front.

Closing a tab group needs care. A group keeps its tabs, so closing its tabs removes them from the
group for good. "Closing" a group should instead mean closing the window that shows it, with Close
Windows. The group stays saved for next time.

### Focus filters: tab groups and profiles without code

macOS can switch Safari to a tab group, a profile, or both when a Focus turns on. Set it up in
System Settings › Focus › the Focus › Focus Filters › Add Filter › Safari.

ContextSwitcher can already turn a Focus on for each profile, so this works as is. Profile Setup
says so when the browser is Safari, with a button that opens the Focus pane
(`x-apple.systempreferences:com.apple.Focus-Settings.extension`).

A forum report says Safari doesn't always switch back when the Focus turns off. Worth testing
before relying on it.

### What else exists

- **`open`:** no option for a profile or a tab group; links open in the last active profile.
- **Safari Web Extensions:** no tab group API. Distribution also needs the Apple Developer Program
  ($99 a year).
- **Pages opened while a tab group is showing go into that group.** That is how Safari works. It's
  worth knowing, since tabs added to a group stay in it and sync.

## Workarounds (not used)

These drive the menus or keyboard as if they were the user. They need the Accessibility permission
and break when the interface, its layout or the Mac's language changes:

- **Safari profiles:** Option-Shift-Command-*number* opens a window in a profile (0 is the
  default). The same thing is in File › New Window.
- **Safari tab groups:** the sidebar or the tab group picker.
- **Chrome saved groups:** the bookmarks bar or the Tab groups menu.

## Ideas for later

- **Safari tab groups through Shortcuts.** This is the only official route to real groups. It needs
  the action identifier described above. Opening a group would be Open Tab Group; closing it would
  be Close Windows on the window showing it.
- **ContextSwitcher's own groups for any browser.** A named list of pages that opens in its own
  window and closes on leaving. In Chrome, the window can carry the group's name (`given name`,
  `--window-name`).
- **Chrome profiles by email** (`--profile-email`), and **a new window per profile**
  (`--new-window`).
- **A Chrome extension** for real Chrome groups. It is the heaviest option, so only if the above
  isn't enough.

## Sources

- Chrome's and Safari's own dictionaries, App Intents strings and Chromium's `chrome_switches.h`,
  read directly.
- [Chrome: changes to remote debugging switches](https://developer.chrome.com/blog/remote-debugging-port)
- [Chrome extensions: `chrome.tabGroups`](https://developer.chrome.com/docs/extensions/reference/api/tabGroups)
- [Chromium: Tab Group Home WebUI commit](https://chromium.googlesource.com/chromium/src/+/18ddd3634977350e1bf34355868a180bcb7b3f5a)
- [Apple: Use profiles in Safari on Mac](https://support.apple.com/en-au/105100)
- [MacRumors: associate a Safari tab group with a Focus](https://macrumors.com/how-to/associate-safari-tab-group-focus)
- [Apple Developer Forums: Safari profile in AppleScript](https://developer.apple.com/forums/thread/744609)
- [Keyboard Maestro forum: Safari tab groups](https://forum.keyboardmaestro.com/t/safari-tab-groups/26701)
- [Keyboard Maestro forum: opening a Safari profile](https://forum.keyboardmaestro.com/t/is-there-any-way-to-open-a-specific-safari-profile/33321)
- [Keyboard Maestro forum: Chrome tab groups](https://forum.keyboardmaestro.com/t/has-anyone-taken-advantage-of-chromes-tab-groupings-feature-yet/21930)
- [MacPowerUsers: switching Safari to a tab group](https://talk.macpowerusers.com/t/safari-switching-to-specific-tab-group/31955)
- [Mozilla: WebExtensions support for tab groups](https://blog.mozilla.org/addons/2025/04/30/webextensions-support-for-tab-groups/)
