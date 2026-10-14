## What's new

- **Ask about everything else.** When you switch profiles, Context Switcher can now ask what to do with the apps and tabs no profile has: keep them, close them, or add them to the profile. Each comes with its icon. It has a Skip button and a "don't ask again" tick, and Settings → Switching lists apps to never touch (Telegram, say) - they are taken out of every profile and left alone.
- **Click a profile to turn it on or off.** The whole card on the Profiles page is the switch - no Activate button - and the one that is on is bolder, with a glow.
- **The dashboard opens where you are.** It comes up on the desktop you're on instead of taking you to another one.
- **Chrome tabs close properly.** Tabs a profile opened are closed when you leave it even after they have redirected to another address (Fusion and the like).
- **Only what your browser can do.** Browsers and profiles are chosen from the ones installed. Tab groups are gone: neither Chrome nor Safari lets another app open them, so that mode never worked. Pages you had in a group are kept as the profile's pages.
- **Lighter.** Opening the dashboard costs about 14 MB less memory, and its clock no longer ticks while it is hidden.
- **Starts on a dark screen.** Launching while the display is asleep (a Shortcut, a restart) waits for it to wake instead of crashing.

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
