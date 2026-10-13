## What's new

- **See what an update brings before installing it.** Settings → Updates now lists what's new in a version, right above Install and restart.
- **"Default browser" works like the browser it is.** When it's Chrome, Brave or Safari, avoiding duplicate tabs and closing a profile's tabs now work with it too.
- **Apps aren't restarted for nothing.** An app or Docker container the next profile also uses stays open instead of being quit and opened again.
- **Links with commas open.** Addresses such as map coordinates no longer break.
- **More accurate time tracking.** Time after midnight counts for the new day, and a crash no longer counts the hours until the app is opened again.
- **Clearer install instructions.** The manual download needs one Terminal command, and the README and this page now say when, why and what it does.

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
