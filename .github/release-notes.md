## What's new in 0.6.5

- **Write to the developer from the app.** Settings has a new **Contact** section:
  - **Write an email** opens a new message to contextswitcher.app@gmail.com in your mail app. The version is already in it, along with the questions worth answering, and the diagnostics are copied for you to paste in.
  - **Open an issue on GitHub** opens the bug form, also with the diagnostics copied.
  - **What to write** says what helps.

  No GitHub account is needed for the email, and nothing is sent until you send it yourself.
- **Support the developer** now opens [one page with every way to donate](https://artemkagoldman.github.io/support/): Donatello, a Monobank jar and more to come.

Coming from an older version? Everything new in [0.6.4](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.4), [0.6.3](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.3), [0.6.2](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.2), [0.6.1](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.1) and [0.6.0](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.6.0) is in this one too.

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
