## What's new in 0.6.1

- **Clear reasons when a switch doesn't go fully right.** A step that ran out of time says which. A browser tab that stayed open is named, by its site. An app that didn't quit says why that happens, and one that isn't installed is told apart from one that failed to open. When Chrome, Brave or Safari can't be reached, it says whether macOS refused (and where to turn it on: System Settings → Privacy & Security → Automation) or the browser didn't answer. Problems in the settings name the profile instead of "contexts[1]".
- **A log that is quiet until something goes wrong, and then tells all of it.** When everything works, nothing is written. A problem is written with the whole switch that led to it, step by step, a header with the version and macOS, and nothing private: web addresses are cut to their site and emails and your user name are taken out. It starts again every week, and a crash is written too.
- **Settings → Diagnostics.** *Copy diagnostics* puts the version, your macOS, what is switched on (counts, never names) and the recent log on the clipboard to paste into a message. *Detailed log* writes everything the app does for a week, for looking into something that isn't a failure.

Coming from 0.5? Everything new in [0.6.0](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.0) is in this one too.

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
