## What's new in 0.6.7

- **The menu bar icon is back.** Since 0.6.4 ContextSwitcher ran with no icon in the menu bar: an update of the library its windows are built with changed how a menu bar icon starts, and the app didn't follow. Everything else kept working - the main window, switching from it, counting time - but the icon is the main way in, and now it's there again.

  No icon yet? Click ContextSwitcher in the Dock, or open it from Applications, then **Settings → Updates → Install and restart**.
- Every change is now checked by starting the app and looking for its icon in the menu bar, so this can't slip out again unnoticed.

Coming from an older version? Everything new in [0.6.6](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.6), [0.6.5](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.5), [0.6.4](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.4), [0.6.3](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.3), [0.6.2](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.2), [0.6.1](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.1) and [0.6.0](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.0) is in this one too.

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

## Update

Already installed? **Settings → Updates → Install and restart**, or click **Update to …** in the menu
bar menu. Your profiles and settings are kept.
