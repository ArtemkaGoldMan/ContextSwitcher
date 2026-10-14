## What's new in 0.6.2

- **Drag profiles into another order.** On the Profiles page, press and hold a profile and drag it where you want it: the menu bar menu and the dashboard list profiles in the same order. A quick click still turns a profile on or off, and holding one without moving it changes nothing. Escape puts a dragged profile back, and Option with ↑ or ↓ moves the selected one a place from the keyboard.

Coming from an older version? Everything new in [0.6.1](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.1) and [0.6.0](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.0) is in this one too.

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
