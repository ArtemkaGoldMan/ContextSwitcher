# Roadmap

Where ContextSwitcher is, what comes next, and what it will not do. A plan, not a promise: nothing
under [Next](#next) and after is built yet, and the order can change. The Windows version and
counting users have pages of their own; this one puts everything in order.

Have an opinion on the order, or an idea that isn't here? [Write to me](../README.md#contact).

## What every new feature keeps to

ContextSwitcher is meant to be set up in a minute and then forgotten. Everything below keeps it that
way:

- **Off, or out of sight, until wanted.** A new install looks and behaves as it does today. A new
  feature is a tick or a button, never another step in the setup.
- **No new permission.** Nothing here asks macOS for Accessibility, Location, Calendars or Screen
  Recording. Where macOS already does something well - Shortcuts, Focus - ContextSwitcher hands over
  to it rather than building its own.
- **Nothing leaves the computer.** No new network requests, no account.
- **Ready for Windows.** A feature that talks to the system does it through an interface, by
  [the design rules](architecture.md#design-rules), so it never makes the Windows version harder.

## Now

**0.6.7**, October 2026, for macOS 13 and later on Apple silicon. Everything in the
[README](../README.md#why) works: apps, browser pages and profiles, Focus, music, Docker, quick links
and notes, time in each profile, asking about everything else on a switch, the command line, English
and Ukrainian.

Since 0.6.7 the code under the app has been rebuilt for a second platform, without changing what it
does: every piece of macOS sits behind an interface, in a project of its own, and the solution builds
and tests on Windows ([stage 0 of the Windows plan](windows-plan.md#stage-0---groundwork-done)).

## Next

1. **A Mac release built from the rebuilt code.** Nothing since
   [#76](https://github.com/ArtemkaGoldMan/ContextSwitcher/pull/76) has reached Mac users yet, and
   CI can't try a switch on a real Mac. Before it ships, an installed build is tried on a Mac:
   switching both ways, closing and launching apps, browser pages, Focus, music, the dashboard under
   the menu bar, updating from 0.6.7, and [the checks by hand](release-process.md#by-hand-on-an-installed-build).
2. **The Windows version**, a stage at a time - apps, then the browser, then the rest, then Windows
   1.0 - as unsigned previews first: [the Windows plan](windows-plan.md). What Windows can and can't
   do is in [A Windows version](windows-port.md).
3. **New features**, below, in four groups. They go alongside the Windows stages: each release
   carries what is ready, and both platforms share one version number.
4. **Counting the people who use it**, if wanted. Downloads don't say how many people use the app; a
   once-a-day count, asked for and anonymous, would. Not built; the design and what is left to decide
   are in [usage statistics](usage-statistics.md#the-plan-counting-the-people-who-use-it).

| | Group | What it brings | Size |
| --- | --- | --- | --- |
| 1 | [Everyday polish](#1-everyday-polish) | The menu bar shows the profile you're in, music pauses when you leave, back to the last profile in one go, ready-made profiles | small |
| 2 | [Switch without clicking](#2-switch-without-clicking) | `contextswitcher://` links and a Shortcut per profile in one click: a keyboard shortcut, Siri, and on macOS 26 switching by time, screen or Wi-Fi | small to medium |
| 3 | [Pick up where you left off](#3-pick-up-where-you-left-off) | A line on where you stopped, shown when you come back, and the same tabs back | medium |
| 4 | [Keep work out of the evening](#4-keep-work-out-of-the-evening) | A break with a timer, a nudge at the end of the day, and how often you switched | small |

## 1. Everyday polish

Small things that make the app feel finished.

### The menu bar shows the profile you're in

Today the menu bar icon is always the same two squares and its tooltip only says *ContextSwitcher*
(`CreateTrayIcon` in [`App.axaml.cs`](../src/ContextSwitcher.App/App.axaml.cs)). The one thing the
app promises - knowing which mode you're in - takes a click to find out.

- With a profile on, the icon is that profile's icon - the drawings in
  [`ProfileIcons`](../src/ContextSwitcher.App/ViewModels/ProfileIcons.cs) - as a template image, so
  macOS colours it for a light or a dark menu bar. With none on, the two squares as now.
- The tooltip says *Work, since 9:12*: a start time rather than a running count, so nothing has to
  tick to keep it right.
- `menuBarLabel` is written for every profile but shown nowhere. Avalonia's tray icon is an image
  and a tooltip, with no text, so the label either goes into the tooltip or stops being written.
- On Windows the tray icon needs a light and a dark `.ico` anyway, so each profile's icon comes in
  both.

### Pause the music on the way out

A profile can start a playlist when you enter it, but nothing stops it when you leave:
`AddControlMedia` in
[`AutomationPlanBuilder`](../src/ContextSwitcher.Core/Automation/AutomationPlanBuilder.cs) plans
music for the profile being entered only. Leave Personal for Work, and Spotify keeps playing.

- A tick under the profile's music: **Pause when I leave**.
- It pauses Music or Spotify only if it is running, since scripting an app that isn't starts it.
- Nothing is paused when the profile being entered starts music of its own.

### Back to the last profile

For one keyboard shortcut that goes back and forth between two profiles: `switch --previous` on the
command line, and `contextswitcher://previous` once there are [links](#contextswitcher-links).
`state.json` already keeps `previousContextId`. The menu bar menu gets no new item: every profile is
already one click there.

### Ready-made profiles

**Add new profile** starts empty, and the setup wizard only makes Work and Personal.

- Add new profile asks what to start from: **Empty**, the default, as now; **Meeting or screen
  sharing** - Do Not Disturb, messengers and mail hidden; **Study**; **Deep work** - Do Not
  Disturb, chat apps quit; **Games** - work apps quit.
- The apps are picked from the ones installed, the way the setup wizard picks them, and everything
  stays editable. The Focus still needs its **Create it** click.
- The meeting profile is the one most worth having on a laptop: one click before a call, and
  nothing personal pops up on a shared screen.

## 2. Switch without clicking

Switching from the keyboard, with Siri or on its own today means building a Shortcut by hand around
**Run Shell Script**, which Shortcuts only runs once *Allow Running Scripts* is on in its settings.
Global hotkeys were removed, and with them the app's need for the Accessibility permission. This
group brings switching without a click back, with neither.

### `contextswitcher://` links

- `contextswitcher://switch/work`, `contextswitcher://off`, `contextswitcher://previous` and
  `contextswitcher://dashboard`. A link names the profile's id, which never changes, so renaming a
  profile breaks nothing.
- Registered with `CFBundleURLTypes` in the Info.plist that
  [`build-app.sh`](../scripts/build-app.sh) writes. The app hears them through Avalonia's
  `IActivatableLifetime`, where it already handles being opened again
  ([`App.axaml.cs`](../src/ContextSwitcher.App/App.axaml.cs)), and macOS starts it first if it
  isn't running.
- A link works from anywhere that opens one: Raycast, Alfred, a Stream Deck button, a bookmark, a
  Shortcut's **Open URLs**.
- On Windows the same links are a registered protocol - the way to switch from PowerToys,
  AutoHotkey or a Stream Deck, as Windows has no Shortcuts app.
- **Safety.** Any web page can hold a link, so a link only does what the menu bar menu does -
  switch, turn off, open the dashboard - and never changes a setting. Apps are quit the same polite
  way as from the menu, and browsers ask before they open an app's link.

### A Shortcut for each profile, in one click

- In Profile Setup, **Make a Shortcut** creates *ContextSwitcher - Switch to Work*, which opens
  `contextswitcher://switch/work`. It works like Focus's **Create it**
  ([`FocusShortcutInstaller`](../src/ContextSwitcher.Infrastructure.MacOS/FocusShortcutInstaller.cs)):
  the app writes the Shortcut, signs it with `shortcuts sign` and opens it, and Shortcuts asks to
  **Add Shortcut**.
- From there, in Shortcuts, with nothing more from ContextSwitcher:
  - a **keyboard shortcut** - the Shortcut's ⓘ → Add Keyboard Shortcut;
  - **Siri** - "Switch to Work";
  - on **macOS 26 Tahoe**, an **automation**, which Shortcuts on the Mac can now run at a time of
    day, when a display or a Wi-Fi network connects, or when a Focus turns on. *9:00 on weekdays →
    Work*, *the external monitor → Work*, *home Wi-Fi → Personal*: no scheduler of ContextSwitcher's
    own, and no Location permission.
- [Focus and Siri](shortcuts-integration.md) gets a short recipe for each, and the link replaces Run
  Shell Script there.
- Mac only. On Windows a global hotkey does this job ([stage 3](windows-plan.md#stage-3---the-rest)).

### A tip for the user guide

With **Share across devices** on in System Settings → Focus, a Focus that a switch turns on is on
for the iPhone and iPad too: switch to Work on the Mac, and the phone goes quiet as well. One line
in the user guide's [Focus section](user-guide.md#4-turn-on-focus-optional).

## 3. Pick up where you left off

The hard part of switching isn't the apps. It's the work you're still thinking about in the evening,
and not remembering in the morning where you were. Research on interruptions calls what stays in
your head *attention residue*, and found that a short "ready-to-resume" plan - noting where you'll
pick up - lets people put the old task down and give the new one their attention
([Leroy and Glomb, 2018](https://doi.org/10.1287/orsc.2017.1184)).

### "Where did I stop?"

- Leaving a profile, a small window asks for one line: *Next: answer Anna about the release notes*.
  Return saves it and Esc skips it; either way the switch carries on at once. With
  [ask-on-switch](user-guide.md#6-make-it-yours) on, the line is part of that window, so a switch
  never asks twice.
- Coming back, the line is at the top of the dashboard until you clear it.
- Only for switches started in the app. One from the command line, a link or an automation never
  asks, as nobody may be there to answer.
- Off until ticked: **Settings → Switching → Ask where I stopped**. Kept in `state.json`, on the
  computer.

### The same tabs back

A profile that closes its pages on the way out closes exactly the tabs that show them: the pull
request, the ticket, the document, not just the site. Coming back opens the profile's fixed list of
pages instead.

- With **Reopen the pages I had open** ticked, the tabs a profile closes are remembered and opened
  again the next time you enter it, without duplicating any still open (`PageMatch`).
- Only tabs ContextSwitcher closed itself, kept on the computer in one file that each switch
  replaces. They are never counted in Stats or written to the log, which already cuts addresses to
  their site.
- The file is a new one, so [Files](architecture.md#files) in the architecture doc gets a line, and
  the README's Privacy section says what it holds.
- On Windows it needs tabs closed one by one, which comes with the browser extension or the
  DevTools browser profile ([later](#later-if-wanted)).

## 4. Keep work out of the evening

### A break with a timer

- Next to each profile in the dashboard, a small clock: **15 min**, **30 min**, **1 hour**. It
  switches as usual, and when the time is up a notification asks *Back to Work?* Clicking it
  switches back.
- Nothing switches on its own: ignore the notification and you stay where you are.
- On Windows it waits for notifications ([stage 3](windows-plan.md#stage-3---the-rest)).

### A nudge at the end of the day

- Per profile, under Advanced: **Remind me after** *n* **hours here**. *9 hours in Work - time to
  stop?* Clicking it opens the dashboard; it never quits anything by itself, and comes once a day
  at most.
- Time the computer is asleep or locked doesn't count, as in Stats already.

### How often you switched

- The Stats page gets two numbers for the week or the month: **switches a day**, and **the longest
  stretch without one**. Every switch is already a line in `analytics.jsonl` (`endReason: Switch`),
  so this only reads what's there.
- Fewer switches and longer stretches are what the app is for, so they're worth seeing.

## Later, if wanted

- **A browser extension** of our own, with a native messaging host - the only way to get every tab's
  address and close it in any window, and to drive real tab groups, on Windows and on the Mac too
  ([browser control](browser-control.md#the-one-way-to-tab-groups-an-extension),
  [on Windows](windows-port.md#what-to-build)). The heaviest option: a separate project, the Chrome
  Web Store, and people installing it.
- **A browser profile of the app's own**, driven through the DevTools protocol, for people who want
  exact control of tabs on Windows without an extension.
- **Safari tab groups through Shortcuts** - its Open Tab Group action is the only official way to
  them - and **ContextSwitcher's own groups of pages** for any browser: a named list that opens in a
  window of its own and closes on leaving ([ideas](browser-control.md#ideas-for-later)).
- **The Microsoft Store**, signed, for Windows 1.0 ([shipping](windows-port.md#shipping-it)).

## Not planned

Ideas that come up, and why they're left out:

| | Why not |
| --- | --- |
| Global hotkeys on the Mac | They need the Accessibility permission. A Shortcut's keyboard shortcut ([group 2](#2-switch-without-clicking)) does the same without it |
| Switching by Wi-Fi or location from inside the app | Reading the Wi-Fi network needs the Location permission. Shortcuts automations on macOS 26 do it ([group 2](#2-switch-without-clicking)) |
| Switching by the calendar | Another permission, and meetings that move, overlap or get cancelled would make it guess wrong |
| Arranging windows per profile | It needs Accessibility, and breaks as apps change |
| Changing the theme or the wallpaper | Both were in the app once and were taken out |
| Accounts, cloud sync, a server holding anyone's profiles | Everything stays on the computer; that is the promise |
| Telemetry | No profiles, apps, sites or times ever leave the computer. The only thing being thought about is the anonymous count under [Next](#next), and only in the open |
| Paid features, or anything unlocked by donating | Every feature is free, looks included. The first specification had cosmetic unlocks for donors; they were never built, and the README promises "nothing locked" |
| Running any command from a profile | Every step is one the app knows how to do safely; nothing from the settings is run as a shell command |

## Decisions to make

- **The order**: which of the four groups come before which Windows stage.
- **The name of a profile's browser window on Windows** - before stage 2.
- **Whether to build the extension**, and before Windows 1.0 or after.
- **Counting users**: whether to, asked or on by default, and where the counts go
  ([its decisions](usage-statistics.md#decisions-to-make)).

## Sources

- Shortcuts automations on the Mac in macOS 26:
  [MacStories](https://www.macstories.net/stories/macos-26-tahoe-the-macstories-review/4),
  [MacMost](https://macmost.com/an-introduction-to-shortcuts-automation-in-macos-tahoe.html),
  [9to5Mac](https://9to5mac.com/2025/07/24/macos-26-tahoe-underrated-features/)
- Sophie Leroy and Theresa M. Glomb, *Tasks Interrupted: How Anticipating Time Pressure on
  Resumption of an Interrupted Task Causes Attention Residue and Low Performance on Interrupting
  Tasks and How a "Ready-to-Resume" Plan Mitigates the Effects*, Organization Science 29(3),
  2018: [doi.org/10.1287/orsc.2017.1184](https://doi.org/10.1287/orsc.2017.1184)
