# User guide

Everything ContextSwitcher does, one step at a time. Not installed yet? See
[Install](../README.md#install). Stuck? Try the [FAQ](../README.md#faq) or
[write to me](../README.md#contact).

1. [A short setup](#1-a-short-setup)
2. [Switch from the menu bar](#2-switch-from-the-menu-bar)
3. [Set up your profiles](#3-set-up-your-profiles)
4. [Turn on Focus (optional)](#4-turn-on-focus-optional)
5. [Allow ContextSwitcher to control other apps](#5-allow-contextswitcher-to-control-other-apps)
6. [Make it yours](#6-make-it-yours)

Then: [When something doesn't go to plan](#when-something-doesnt-go-to-plan) ·
[See where your time goes](#see-where-your-time-goes) · [Updates](#updates) ·
[Uninstall](#uninstall) · [Going further](#going-further)

## 1. A short setup

The first time it opens, a three-step wizard creates a **Work** and a **Personal** profile. It
suggests apps you actually have installed: tick **Launch** to open an app when you switch to that
profile, and **Close** to quit it when you switch away. Its last step offers to open ContextSwitcher
whenever you log in, so it's always there and counting time. You can change everything later.

> [!IMPORTANT]
> **Close** starts ticked for the apps it suggests, and finishing the wizard switches you into Work
> straight away. Switching to another profile later quits those apps — politely, like ⌘Q, so
> anything unsaved asks first. Untick **Close** for any app you want left alone.

<div align="center">
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/onboarding-dark.png">
  <img src="images/onboarding-light.png" width="560" alt="The setup wizard, choosing the apps for the Work profile">
</picture>
</div>

## 2. Switch from the menu bar

ContextSwitcher lives in the **menu bar** — look for the icon of two overlapping squares — and in
the **Dock**, where clicking it opens the main window. Rather keep it to the menu bar? Turn off
**Settings → Show in the Dock**.

- **Click the icon** and pick a profile to switch to it. The active one has a tick; pick it again to
  turn it off — it closes what it closes when you switch away, and no profile is on until you pick
  one. No time is counted while none is on.
- **Open Dashboard** shows the active profile, how long you've been in it, its quick links and notes,
  and the last seven days. Click anywhere else to close it.
- **Open App** opens the main window, where you set profiles up.

Can't see the icon? On a MacBook with a notch, macOS hides menu bar icons that don't fit. Open
ContextSwitcher again from Applications or Spotlight and its window opens — see the
[FAQ](../README.md#faq) for keeping the icon in view.

## 3. Set up your profiles

The **Profiles** page lists every profile: click one to switch to it — or click the one that's on to
turn it off — and **Edit** changes what it does. The profile that's on stands out in its own colour.
**Press and hold a profile, then drag it** to put it somewhere else — or hold Option and press ↑ or ↓
on the one you've selected — and the menu bar and the dashboard list the profiles in that order too.
The order can only be changed here.

<div align="center">
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/profiles-dark.png">
  <img src="images/profiles-light.png" width="720" alt="The Profiles page with Work, Personal and Study, Personal active now">
</picture>
</div>

Editing a profile is mostly picking from lists: apps from the apps you have installed, web pages
from the tabs you have open, playlists from your library, containers from Docker. Apps and pages
each have two ticks — open when you enter the profile, close when you leave it — and an app can be
**hidden** instead of quit, so it comes back at once with its windows.

<div align="center">
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/profile-setup-dark.png">
  <img src="images/profile-setup-light.png" width="720" alt="Editing the Work profile: its apps with Launch and Close, and its pages with Open and Close">
</picture>
</div>

## 4. Turn on Focus (optional)

macOS doesn't let other apps change Focus directly — only the **Shortcuts** app can. So for each
Focus you use, ContextSwitcher needs two small shortcuts: one that turns it on and one that turns it
off. You don't build them yourself:

1. In **Focus & music**, choose a **Focus mode**.
2. Click **Create it**. Shortcuts opens and asks to **Add Shortcut** — click it. The mode now shows
   **Ready**.
3. If a second **Create it** appears — for the shortcut that turns Focus off — do the same.

<div align="center">
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/profile-setup-focus-dark.png">
  <img src="images/profile-setup-focus-light.png" width="720" alt="The Focus & music and Advanced sections of a profile, with Do Not Disturb ready">
</picture>
</div>

A Focus you created yourself needs the two shortcuts made by hand — see
[Focus and Siri](shortcuts-integration.md).

## 5. Allow ContextSwitcher to control other apps

The first time a switch quits an app, reads your browser's tabs or starts music, macOS asks whether
ContextSwitcher may control that app. Click **OK**. **Settings → Permission** shows whether it's
allowed and opens the right page in System Settings if it isn't. More in
[Permissions](automation-permissions.md).

## 6. Make it yours

**Settings** has the rest: the **Language** — English or Ukrainian, or whichever of them your Mac
uses — a light or dark **Theme** (or *Match macOS*, the default), whether the app shows in the
Dock, whether it opens when you log in, how long to keep your history, and updates. **Export and
import** saves your profiles and settings to a file — a copy to keep, or the way to set up another
Mac the same — and loads them back.

- **Switching** can ask, on each switch, what to do with the apps and tabs no profile has — keep
  them, close them, or add them to a profile — and keeps a list of apps it never touches, such as a
  messenger you use in every profile.
- **Diagnostics** has **Copy diagnostics**, the report to send when something is wrong, and a
  **Detailed log** that writes down everything for a week when something is off but nothing failed.
- **Contact** writes to me: an email, no account needed, or an issue on GitHub — see
  [Contact](../README.md#contact).

<div align="center">
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/settings-dark.png">
  <img src="images/settings-light.png" width="720" alt="Settings: language, theme, Dock icon, open at login, permission to control other apps, and the apps it never touches">
</picture>
</div>

## When something doesn't go to plan

One step going wrong doesn't stop the rest of the switch. If an app refused to quit (it had unsaved
work, say) or a shortcut is missing, a card at the bottom of the Profiles page says what happened
and how to fix it. It stays until you close it or switch again. Still stuck? See the
[FAQ](../README.md#faq) or [write to me](../README.md#contact).

<div align="center">
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/switch-problems-dark.png">
  <img src="images/switch-problems-light.png" width="720" alt="A card at the bottom of the window: Switched to Personal, with warnings — Visual Studio Code may be asking to save changes, and Steam still hasn't quit, with a Force quit button">
</picture>
</div>

## See where your time goes

**Stats** shows how long you spent in each profile, per day, for the last week or month — the
profile you're in counts as it goes, and time while your Mac sleeps or its screen is locked doesn't
count. It's recorded only on your Mac and you can turn it off in Settings.

<div align="center">
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/stats-dark.png">
  <img src="images/stats-light.png" width="720" alt="The Stats page: a column per day split by profile">
</picture>
</div>

## Updates

ContextSwitcher checks for a new version once a day. When there is one, macOS shows a notification,
and **Update to x.y.z…** appears in the menu bar menu and in **Settings → Updates**. Click
**Install and restart**: it downloads the newest version, checks it is signed with the same
certificate as the one you have, and restarts into it.

- Your profiles, settings and history are kept.
- You don't need the Terminal command again, and macOS doesn't ask for permissions again.
- Prefer to check yourself? Turn off **Check automatically** and use **Check now**.
- The first notification makes macOS ask whether ContextSwitcher may send them. You can change that
  later in **System Settings → Notifications**.

Running the one-command install again also updates, and so does `brew upgrade --greedy` if you
installed with Homebrew.

## Uninstall

1. Click the menu bar icon → **Quit**.
2. Drag **ContextSwitcher** from Applications to the Bin — or `brew uninstall --cask contextswitcher`.
3. To remove your profiles and history too:

   ```bash
   rm -rf ~/.config/ContextSwitcher
   ```

4. If you created Focus shortcuts, delete the ones named *ContextSwitcher - Focus …* in Shortcuts.

## Going further

| | |
| --- | --- |
| [Focus and Siri](shortcuts-integration.md) | Focus shortcuts made by hand, switching with Siri or a keyboard shortcut, and the command line |
| [Permissions](automation-permissions.md) | What macOS asks for, and what to do when a step is blocked |
| [Configuration](configuration.md) | Every field in `settings.json`, with examples, for editing it by hand |
