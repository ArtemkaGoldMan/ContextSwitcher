## Install

Paste this into Terminal - it downloads the app, puts it in Applications and opens it:

```bash
curl -fsSL https://raw.githubusercontent.com/ArtemkaGoldMan/ContextSwitcher/main/install.sh | bash
```

Or download `ContextSwitcher-*.dmg` below and drag the app to Applications. ContextSwitcher isn't
notarised by Apple (that needs a paid developer account), so macOS refuses to open it the first time.
Allow it once with:

```bash
xattr -dr com.apple.quarantine /Applications/ContextSwitcher.app
```

or from **System Settings → Privacy & Security → Open Anyway**.

Requires an Apple silicon Mac and macOS 13 or later.

## Update

Already installed? **Settings → Updates → Install and restart**, or click **Update to …** in the menu
bar menu. Your profiles and settings are kept.
