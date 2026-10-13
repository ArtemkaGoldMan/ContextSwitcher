## What's new

- **Update notifications.** When a new version is out, macOS lets you know. Click the notification to update.
- **Close a profile's tabs when you leave it.** Each page in a profile now has Open and Close, like apps. Only those pages' tabs close; your other tabs stay open.
- **Fewer duplicate tabs.** A page you already have open is recognised even after it redirected or you clicked around in it.
- **Turn a profile off.** Click the active profile again in the menu bar or the dashboard, and no profile is on until you pick one. `ContextSwitcher off` does the same from Shortcuts or Siri.
- **More accurate time tracking.** Time no longer counts while your Mac sleeps. Switches from Shortcuts, Siri and the command line are counted correctly. Stats shows the profile you're in as the time goes.

## Install

Paste this into Terminal - it downloads the app, puts it in Applications and opens it:

```bash
curl -fsSL https://raw.githubusercontent.com/ArtemkaGoldMan/ContextSwitcher/main/install.sh | bash
```

Or with [Homebrew](https://brew.sh):

```bash
brew install --cask artemkagoldman/tap/contextswitcher
```

Or download `ContextSwitcher-*.dmg` below and drag the app to Applications — and then **run this in
Terminal. It's required: the app won't open without it.**

```bash
xattr -dr com.apple.quarantine /Applications/ContextSwitcher.app
```

ContextSwitcher isn't notarised by Apple (that needs a paid developer account), so macOS blocks a
downloaded copy until it's allowed once. Instead of the command you can use **System Settings →
Privacy & Security → Open Anyway**. The two options above don't need this step.

Requires an Apple silicon Mac and macOS 13 or later.

## Update

Already installed? **Settings → Updates → Install and restart**, or click **Update to …** in the menu
bar menu. Your profiles and settings are kept.
