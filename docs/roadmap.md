# Roadmap

Where ContextSwitcher is, what comes next, and what it will not do. Each piece of work has a page of
its own; this one only puts them in order.

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
2. **Windows, stage 1 - apps.** The tray, profiles, launching, closing and hiding apps, and time,
   shipped as an unsigned preview. Then the browser, then the rest, one release at a time:
   [the Windows plan](windows-plan.md). What Windows can and can't do is in
   [A Windows version](windows-port.md).
3. **Counting the people who use it**, if wanted. Downloads don't say how many people use the app; a
   once-a-day count, asked for and anonymous, would. Not built; the design and what is left to decide
   are in [usage statistics](usage-statistics.md#the-plan-counting-the-people-who-use-it).

## Later, if wanted

- **A browser extension** of our own, with a native messaging host - the only way to get every tab's
  address and close it in any window, and to drive real tab groups, on Windows and on the Mac too
  ([browser control](browser-control.md#the-one-way-to-tab-groups-an-extension),
  [on Windows](windows-port.md#what-to-build)).
- **A browser profile of the app's own**, driven through the DevTools protocol, for people who want
  exact control of tabs on Windows without an extension.
- **Safari tab groups through Shortcuts**, and ContextSwitcher's own groups of pages for any browser
  ([ideas](browser-control.md#ideas-for-later)).
- **The Microsoft Store**, signed, for Windows 1.0 ([shipping](windows-port.md#shipping-it)).

## Not planned

- **Accounts, cloud sync or a server holding anyone's profiles.** Everything stays on the computer.
- **Telemetry.** No profiles, apps, sites or times ever leave the computer. The only thing being
  thought about is the anonymous count above, and only in the open.
- **Paid features, or anything unlocked by donating** - not even looks. The first specification had
  cosmetic unlocks for donors; they were never built, and the README promises "nothing locked".
- **Running arbitrary commands from a profile.** Every step is one the app knows how to do safely.

## Decisions to make

- **Windows 10** too, or Windows 11 only - before Windows stage 1
  ([the plan](windows-plan.md#decisions-to-make)).
- **The name of a profile's browser window on Windows** - before stage 2.
- **Whether to build the extension**, and before Windows 1.0 or after.
- **Counting users**: whether to, asked or on by default, and where the counts go
  ([its decisions](usage-statistics.md#decisions-to-make)).
