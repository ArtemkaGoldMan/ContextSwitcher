<div align="center">

<img src="docs/images/icon.png" width="112" alt="">

# ContextSwitcher

**Switch your Mac between work and personal in one click.**

It opens the apps you need, quits the ones you don't, opens your tabs, turns on Focus and starts
your music — all from the menu bar.

<a href="https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/latest"><img src="docs/images/download.svg" height="44" alt="Download for macOS"></a>

![macOS 13+](https://img.shields.io/badge/macOS-13%2B-000000?logo=apple&logoColor=white)
![Apple silicon](https://img.shields.io/badge/Apple%20silicon-M1%20and%20later-555555)
![Free and open source](https://img.shields.io/badge/free-MIT-20A67A)
![English · Українська](https://img.shields.io/badge/languages-English%20%C2%B7%20%D0%A3%D0%BA%D1%80%D0%B0%D1%97%D0%BD%D1%81%D1%8C%D0%BA%D0%B0-555555)
[![Support the developer](https://img.shields.io/badge/support-the%20developer-8B5CF6?logo=data:image%2Fsvg%2Bxml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0id2hpdGUiIHN0cm9rZT0id2hpdGUiIHN0cm9rZS13aWR0aD0iMiIgc3Ryb2tlLWxpbmVjYXA9InJvdW5kIiBzdHJva2UtbGluZWpvaW49InJvdW5kIj48cGF0aCBkPSJNMTkgMTRjMS40OS0xLjQ2IDMtMy4yMSAzLTUuNUE1LjUgNS41IDAgMCAwIDE2LjUgM2MtMS43NiAwLTMgLjUtNC41IDItMS41LTEuNS0yLjc0LTItNC41LTJBNS41IDUuNSAwIDAgMCAyIDguNWMwIDIuMyAxLjUgNC4wNSAzIDUuNWw3IDdaIi8%2BPC9zdmc%2B)](https://artemkagoldman.github.io/support/)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/dashboard-dark.png">
  <img src="docs/images/dashboard-light.png" width="360" alt="The ContextSwitcher dashboard under the menu bar: the active Work profile, buttons to switch to Personal or Study, quick links, notes and a chart of the last seven days">
</picture>

[Install](#install) · [How it works](#how-it-works) · [User guide](docs/user-guide.md) · [FAQ](#faq) · [Contact](#contact) · [Support the developer](#support-the-developer)

</div>

---

## Why

You finish work. Slack is still pinging, your editor is still open, a dozen work tabs are still
loaded and Do Not Disturb is still on from the morning. Switching by hand takes five minutes, so
you never do all of it — and work follows you into the evening.

ContextSwitcher keeps a **profile** for each part of your day. Click one, and your Mac turns into it:

| | What a profile can do |
| --- | --- |
| **Apps** | Open apps when you switch in, and quit them when you switch out — the polite way, like ⌘Q. Nothing is ever forced to quit unless you click **Force quit** |
| **Browser** | Open web pages without duplicating tabs that are already open, or a whole Chrome or Brave profile — and close a profile's pages when you leave it, leaving every other tab alone. Safari can switch to a tab group or profile with the profile's Focus |
| **Focus** | Turn on a macOS Focus — Do Not Disturb, Work, Sleep… — and turn it off again when you leave |
| **Music** | Start an Apple Music or Spotify playlist |
| **Docker** | Start the containers this profile needs, stop the ones it doesn't |
| **Dashboard** | Keep a few links and notes for this profile one click away |
| **Time** | See how your week splits between work and the rest of your life, stored only on your Mac |
| **Everything else** | Optionally, ask on each switch about the apps and tabs no profile has — keep them, close them, or add them to a profile — and keep a list of apps it never touches, like a messenger |

Every part is optional. A profile that only opens two apps is a perfectly good profile.

## Install

You need a Mac with **Apple silicon** (M1 or later) and **macOS 13 Ventura or later**.

Pick one of the three. Options 1 and 2 are one command and nothing else. Option 3 is a download
**plus one Terminal command you can't skip**. Never used Terminal? It's a built-in Mac app, and all
you do is paste one line into it — [here's how](#never-used-terminal).

### Option 1 — one command (recommended)

Open **Terminal** (press ⌘Space, type *Terminal*, press Return), paste this and press Return
([first time?](#never-used-terminal)):

```bash
curl -fsSL https://raw.githubusercontent.com/ArtemkaGoldMan/ContextSwitcher/main/install.sh | bash
```

It downloads the latest version, checks it isn't damaged, puts it in Applications and opens it.
Nothing else to do: macOS doesn't ask you to approve it.

### Option 2 — Homebrew

If you use [Homebrew](https://brew.sh) — if you don't know what that is, use Option 1:

```bash
brew install --cask artemkagoldman/tap/contextswitcher
```

Homebrew installs the latest release. Later versions come from the app itself (Settings → Updates);
`brew upgrade` leaves it alone unless you add `--greedy`.

### Option 3 — download it yourself

> [!IMPORTANT]
> **This option has a required step in Terminal.** macOS blocks a downloaded copy of
> ContextSwitcher, and the app won't open until you run the command in step 3 —
> [why](#why-the-terminal-command). Options 1 and 2 don't need it.

1. Download **ContextSwitcher-x.y.z.dmg** from the [latest release](https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/latest).
2. Open it and drag **ContextSwitcher** onto **Applications**.
3. **Required: run this in Terminal** (press ⌘Space, type *Terminal*, press Return), then press
   Return once more ([first time?](#never-used-terminal)):

   ```bash
   xattr -dr com.apple.quarantine /Applications/ContextSwitcher.app
   ```

   It prints nothing when it works.
4. Open **ContextSwitcher** from Applications. It lives in the menu bar.

### Never used Terminal?

<details>
<summary>Open Terminal, paste one line, press Return — step by step</summary>

<br>

Terminal is an app that comes with every Mac. Instead of clicking, you type a command and press
Return. You don't need to know anything about it — you copy one line from this page and paste it in.

1. **Open Terminal.** Press **⌘ Command + Space**, type **Terminal**, and press **Return**. (It's
   also in Finder → Applications → Utilities.) A small window opens with a line of text and a
   blinking cursor.
2. **Copy the command.** Point at the grey box with the command on this page and click the copy
   button (⧉) at its right edge — or select the whole line and press **⌘C**.
3. **Paste it.** Click inside the Terminal window and press **⌘V**. The command appears after the
   text that was already there.
4. **Press Return.**
5. **Wait for it to finish** — when the blinking cursor is back on a new line. Option 1 shows a
   progress bar and ends with *Installed ContextSwitcher …*, then opens the app. The command in
   Option 3 prints nothing at all when it works.
6. **Close Terminal** (**⌘Q**). You won't need it again: updates install from inside the app.

If something looks wrong:

- **It asks for a password.** None of these commands does — stop and check that what you pasted is
  exactly the line from this page.
- ***command not found*, or a line starting with `>`**: only part of the command was pasted. Press
  **Control + C**, then copy and paste it again.
- ***couldn't put the app in /Applications***: your Mac account isn't an administrator. Ask whoever
  manages the Mac to install it.

A command pasted into Terminal can do anything you can, so only paste ones from a source you trust.
These only download ContextSwitcher, put it in Applications and remove the *"from the internet"*
label — Option 1's whole script is [install.sh](install.sh), short enough to read first.
</details>

### Why the Terminal command?

Only for Option 3. Your browser marks everything it downloads as *"from the internet"*, and macOS
opens such an app without asking only if its developer pays Apple **$99 a year** to have every
release notarised. ContextSwitcher is a free hobby project, and **$99 a year is too much for me**,
so it isn't. The command removes that label from this one app and changes nothing else.

<details>
<summary>Why only Option 3, what happens without it, whether it's safe, and a way without Terminal</summary>

<br>

**When you need it.** Only when you downloaded the app in a browser — Option 3. Options 1 and 2
never put the label on, and updates don't need it either: the app installs those itself, and they
open straight away.

**What happens without it.** Nothing breaks — the app just doesn't open. macOS shows *"Apple could not
verify ContextSwitcher is free of malware"* with only **Done** and **Move to Trash**. Since macOS 15
there is no right-click → **Open** way around it any more.

**Is it safe?** The app is built by GitHub from the public source code in this repository, which
anyone can read, and every release is signed with the project's own certificate. The app checks that
signature before it installs an update, and refuses one that didn't come from this project.

**Rather not use Terminal?** Try to open the app and click **Done**, then go to **System Settings →
Privacy & Security**, scroll down to *"ContextSwitcher was blocked"* and click **Open Anyway**. It does
the same as the command.
</details>

## How it works

1. **A short setup.** The first time it opens, a three-step wizard makes a **Work** and a
   **Personal** profile from apps you actually have, and switches you into Work.
2. **Switch from the menu bar.** Click the icon of two overlapping squares and pick a profile; the
   one that's on has a tick. The Dock icon opens the main window.
3. **Make the profiles yours.** On the **Profiles** page, **Edit** a profile — mostly picking from
   lists: your apps, your open tabs, a Focus, a playlist, Docker containers.
4. **If a step goes wrong**, the rest of the switch still happens, and a card says what happened and
   how to fix it.

> [!IMPORTANT]
> In the setup, **Close** starts ticked for the apps it suggests. Switching to another profile
> quits those apps — politely, like ⌘Q, so anything unsaved asks first. Untick **Close** for any
> app you want left alone.

<div align="center">
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/profiles-dark.png">
  <img src="docs/images/profiles-light.png" width="720" alt="The Profiles page with Work, Personal and Study, Personal active now">
</picture>
</div>

**[Read the user guide →](docs/user-guide.md)** — every step with screenshots, and Focus,
permissions, settings, time stats, updates and uninstalling.

## Privacy

- **No account, no tracking, no analytics sent anywhere.** Recorded time stays in
  `~/.config/ContextSwitcher` on your Mac.
- **One network request a day**, to GitHub, to see whether there is a new version. Turn it off in
  **Settings → Updates**.
- It never reads your browsing history. It only looks at open tabs to avoid opening a page twice,
  to close the pages you asked it to close, or when you pick one in the profile editor.
- It only uses a short, fixed set of macOS's own tools to do its job — AppleScript, `open`,
  Shortcuts, Docker if you use it, and `ditto`, `codesign` and `sips` for updates and icons.
- **Diagnostics stay on your Mac.** **Copy diagnostics** and **Write an email** only put the report
  on your clipboard and open your own mail app; nothing is sent until you send it yourself.

## FAQ

<details>
<summary><b>I can't find the menu bar icon</b></summary>

<br>

On a MacBook with a notch, macOS hides menu bar icons that don't fit beside it. Open
ContextSwitcher again from Applications or Spotlight (⌘Space, *ContextSwitcher*) and its window
opens. To keep it reachable, keep **Settings → Show in the Dock** on, or make room in the menu bar:
hold ⌘ and drag system icons you don't need (Wi-Fi, battery…) out of it, or quit apps whose icons
you don't use.
</details>

<details>
<summary><b>"ContextSwitcher can't be opened" / "Apple could not verify…"</b></summary>

<br>

You installed from the `.dmg` and the required step was skipped. Run this in Terminal, then open the
app again:

```bash
xattr -dr com.apple.quarantine /Applications/ContextSwitcher.app
```

Why it's needed: [Why the Terminal command?](#why-the-terminal-command)
</details>

<details>
<summary><b>An app didn't quit when I switched</b></summary>

<br>

ContextSwitcher asks apps to quit the way ⌘Q does, so an app with unsaved work shows its save
dialog and stays open. The card on the Profiles page names it. Save your work and switch again, or
untick **Close** for that app.

Some apps close their windows and take a while longer to go - Autodesk Fusion takes about half a
minute. The card says it is **still closing**, and the line goes on its own once it has quit. An
app still running two minutes later with no window on screen is in the background, as Steam
sometimes stays, or has its window on another desktop; its line has a **Force quit** button, which
ends it at once, the way Force Quit in macOS does - check first that it isn't asking about saving.
Nothing is ever ended by force unless you click it.
</details>

<details>
<summary><b>Focus didn't change</b></summary>

<br>

Open the profile and check the Focus mode shows **Ready**. If it says a shortcut is missing, click
**Create it** and add it in Shortcuts.
</details>

<details>
<summary><b>How do I update?</b></summary>

<br>

ContextSwitcher checks for a new version once a day and tells you. Click **Install and restart** in
**Settings → Updates**, or **Update to x.y.z…** in the menu bar menu. Your profiles, settings and
history are kept, and you don't need the Terminal command again. More in the
[user guide](docs/user-guide.md#updates).
</details>

<details>
<summary><b>How do I uninstall it?</b></summary>

<br>

Quit it from the menu bar icon, then drag **ContextSwitcher** from Applications to the Bin — or
`brew uninstall --cask contextswitcher`. To remove your profiles and history too:

```bash
rm -rf ~/.config/ContextSwitcher
```

If you made Focus shortcuts, delete the ones named *ContextSwitcher - Focus …* in Shortcuts.
</details>

<details>
<summary><b>Does it work on Intel Macs?</b></summary>

<br>

No — releases are built for Apple silicon (M1 and later) only.
</details>

<details>
<summary><b>Can I switch with Siri, a keyboard shortcut or a script?</b></summary>

<br>

Yes. Every switch can be run from the command line, so a shortcut in the Shortcuts app can do it —
and Siri or a keyboard shortcut can run that:

```bash
/Applications/ContextSwitcher.app/Contents/MacOS/ContextSwitcher switch --context work
```

See [Focus and Siri](docs/shortcuts-integration.md) for the commands and how to set it up.
</details>

<details>
<summary><b>Where are my settings? Can I edit them by hand?</b></summary>

<br>

In `~/.config/ContextSwitcher/settings.json`. Yes — see [Configuration](docs/configuration.md).
The app checks the file and keeps backups, so a typo can't lose your profiles.
</details>

## Contact

Something doesn't work, or you have an idea? Write to me — I read everything, in English or
Ukrainian.

**By email — no account needed:** [contextswitcher.app@gmail.com](mailto:contextswitcher.app@gmail.com).
The easiest way is **Settings → Contact → Write an email**: it opens a new message in your mail app
with the questions below already in it, and copies the diagnostics for you to paste in.

**On GitHub**, if you have an account: [open an issue](https://github.com/ArtemkaGoldMan/ContextSwitcher/issues/new?template=bug_report.yml),
or **Settings → Contact → Open an issue on GitHub**.

### What to write

- **What happened, and what you expected** — for example *"I switched from Work to Personal and
  Slack stayed open"*.
- **What you did just before** — which profile you switched from and to, and whether it happens
  every time.
- **The diagnostics** — **Settings → Diagnostics → Copy diagnostics**, then paste them into the
  message with ⌘V. They have the app and macOS versions, what is switched on and the recent log; no
  profile names, web addresses or emails, but glance over them before sending.
- **A screenshot**, if something looks wrong or a warning card says what happened.

An idea is just as welcome: what you'd like it to do, and what for.

## Documentation

| | |
| --- | --- |
| [User guide](docs/user-guide.md) | Every step with screenshots: setup, profiles, Focus, settings, stats, updates |
| [Permissions](docs/automation-permissions.md) | What macOS asks for, and what to do when a step is blocked |
| [Focus and Siri](docs/shortcuts-integration.md) | Focus shortcuts, Siri, and the command line |
| [Configuration](docs/configuration.md) | Every field in `settings.json`, with examples |

Building it or changing it? [CONTRIBUTING.md](CONTRIBUTING.md) has building from source, the tests,
and the docs on releases, translating and performance.

## Support the developer

ContextSwitcher is free and always will be — every feature, no account, nothing locked. If it saves
you time and you'd like to say thanks, every way to donate is on
**[my support page](https://artemkagoldman.github.io/support/)** — Donatello, a Monobank jar and more.
**Support the developer** in the app opens it too.

Telling a friend about it, a star on GitHub, or [a line about what works and what doesn't](#contact)
helps just as much.

## License

[MIT](LICENSE). Icons from [Lucide](https://lucide.dev); see [third-party notices](THIRD-PARTY-NOTICES.md).
