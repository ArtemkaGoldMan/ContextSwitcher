## What's new

- **Update notifications.** When a new version is out, macOS lets you know. Click the notification to update.
- **Close a profile's tabs when you leave it.** Each page in a profile now has Open and Close, like apps. Only those pages' tabs close; your other tabs stay open.
- **Fewer duplicate tabs.** A page you already have open is recognised even after it redirected or you clicked around in it.
- **Turn a profile off.** Click the active profile again in the menu bar or the dashboard, and no profile is on until you pick one. `ContextSwitcher off` does the same from Shortcuts or Siri.
- **More accurate time tracking.** Time no longer counts while your Mac sleeps. Switches from Shortcuts, Siri and the command line are counted correctly. Stats shows the profile you're in as the time goes.

## Install

**Easiest — paste this into Terminal.** It downloads the app, puts it in Applications and opens it.
Nothing else to do:

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
