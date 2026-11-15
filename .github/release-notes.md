## What's new in 0.7.0

- **ContextSwitcher now runs on Windows, as a preview.** Windows 10 (version 2004 or later) and Windows 11, on Intel and AMD PCs: profiles that open, close and hide your apps, the dashboard with links and notes, time in each profile (paused while the PC sleeps or is locked), opening at sign-in, English and Ukrainian, an installer and updating from inside the app. Browser pages, Docker, Spotify and the command line come next; there is no Focus on Windows. The installer isn't signed, so Windows asks once before it runs. How to install it is below.
- **On a Mac nothing changes on screen.** The shared code under both versions grew to carry Windows, and the Mac's release process now builds both from one tag.

Coming from an older version? Everything new in [0.6.8](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.8), [0.6.7](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.7), [0.6.6](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.6), [0.6.5](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.5), [0.6.4](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.4), [0.6.3](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.3), [0.6.2](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.2), [0.6.1](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.1) and [0.6.0](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.0) is in this one too.

## Install

**Easiest — paste this into Terminal.** It downloads the app, puts it in Applications and opens it.
Nothing else to do. Terminal is a built-in app: press ⌘Space, type *Terminal*, press Return, paste
with ⌘V and press Return — [step by step](https://github.com/ArtemkaGoldMan/ContextSwitcher#never-used-terminal):

```bash
curl -fsSL https://raw.githubusercontent.com/ArtemkaGoldMan/ContextSwitcher/main/install.sh | bash
```

**Or with [Homebrew](https://brew.sh)** — also nothing else to do:

```bash
brew install --cask artemkagoldman/tap/contextswitcher
```

**Or download `ContextSwitcher-*.dmg` below** and drag the app to Applications. Then **you must run
this once in Terminal — the app won't open without it:**

```bash
xattr -dr com.apple.quarantine /Applications/ContextSwitcher.app
```

Why: your browser marks downloads as *"from the internet"*, and macOS only opens such an app without
asking if its developer pays Apple **$99 a year** to have every release notarised. ContextSwitcher is a
free hobby project and **$99 a year is too much for me**, so it isn't. The command removes that label
from this one app and changes nothing else. **System Settings → Privacy & Security → Open Anyway**
does the same. You do it once: the two options above and the app's own updates don't need it.

Requires an Apple silicon Mac and macOS 13 or later.

## Install on Windows (preview)

Download **`ContextSwitcher-win-Setup.exe`** below and run it. **Windows shows "Windows protected your
PC" once**, because the installer isn't signed: click **More info**, then **Run anyway**. It installs
for you alone, with no administrator rights, and opens the app in the tray (Windows 11 hides a new
icon behind the **^** arrow by the clock).

On Windows it starts with apps, the dashboard and time; browser pages, Docker, Spotify and the command
line come next, and there is no Focus. Requires Windows 10 (version 2004 or later) or Windows 11 on an
Intel or AMD PC; Arm PCs are not built yet. There is also `ContextSwitcher-win-Portable.zip`, which
needs no installing.

## Update

Already installed? **Settings → Updates → Install and restart**, or click **Update to …** in the menu
bar menu. Your profiles and settings are kept.
