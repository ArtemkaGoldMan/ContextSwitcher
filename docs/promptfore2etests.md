# Prompt for the E2E test agent

Copy everything below the line into a fresh Claude Code session in
`/Users/artem/Projects/ContextSwitcher`.

---

You are doing an **end-to-end functional test** of ContextSwitcher, a macOS menu bar app
(.NET 10 + Avalonia) that switches the Mac between "work" and "personal" contexts by launching
and quitting apps, and opening browser URLs.

Read `agent.md` first (sections 6.1 config schema, 8 switch pipeline, 9 macOS command templates,
12 error handling). It is the source of truth.

## What I need you to prove

Two profiles, configured through the app's own UI layer, that actually do the right thing when
switched:

1. **Profile A ("Test Work")** — launches 2 apps, opens 2 URLs in a browser
2. **Profile B ("Test Personal")** — launches 1 different app, closes the 2 apps from Profile A,
   opens 1 different URL

Then switch A → B → A and verify, **by observing the real system**, that:

- Apps in `launchApps` actually launched
- Apps in `closeApps` actually quit
- The configured URLs actually opened in the right browser
- `settings.json` round-tripped correctly (what the UI saved is what's on disk)
- `state.json` reflects the correct active context after each switch
- Every step's `AutomationResult` status is `Succeeded` (not silently `Skipped` / `Warning`)

Report what worked and what didn't. **Finding bugs is a success outcome — do not paper over
failures or adjust the test until it passes.** If a step reports `Succeeded` but the app didn't
actually open, that is exactly the kind of bug worth catching, so verify with `ps`/`osascript`
rather than trusting the app's own result codes.

## Hard constraint: you cannot click the UI

This machine has **no Accessibility permission** for the terminal, so:

- `osascript` UI scripting (`tell process ... to click`) fails with `-1719 not allowed assistive access`
- Synthetic clicks (`cliclick`, `CGEventPost`) do not work
- **Do not** try to grant the permission, install click tools, or open System Settings

`osascript` Automation _does_ work (`tell application "System Events" to get name of every process`
succeeds), so you can query and control apps — you just cannot drive ContextSwitcher's own UI.

**Two workable approaches — use both:**

**(a) Drive the ViewModels directly via a temporary debug hook.** Add a block in
`App.axaml.cs` `OnFrameworkInitializationCompleted` gated on the existing `open-dashboard` arg
(any _unrecognized_ arg is treated as a headless CLI command by `CliCommandRouter.IsHeadlessCommand`
and never starts the UI, so invent an arg and you will just get "Unknown command"). Instantiate
`MainAppViewModel` / `ProfileSetupViewModel`, drive commands programmatically, call `SaveCommand`,
then inspect `~/.config/ContextSwitcher/settings.json`. **Revert the hook when done and confirm with
`git diff`.**

**(b) Use the CLI for the switches themselves** — this is the real pipeline, no UI needed:

```bash
dotnet run --project src/ContextSwitcher.App/ContextSwitcher.App.csproj -- switch --context test-work --json
```

Also available: `status`, `list-contexts`, `validate-config`, all with `--json`.

Screenshots work (`screencapture -x -R<x>,<y>,<w>,<h> /tmp/shot.png`) and there is a real display,
so you can visually confirm windows/tabs opened. Read the PNG back to inspect it.

## Safety rules — this test has real side effects on my machine

- **Back up my config first**: `cp -R ~/.config/ContextSwitcher ~/.config/ContextSwitcher-backup-$(date +%s)`
  and restore it at the end.
- **Use only harmless apps** that are safe to open and force-quit. Good choices, all present in
  `/System/Applications`: **Calculator**, **Stickies**, **TextEdit**, **Chess**, **Font Book**.
  **Do not** put Slack, Teams, Outlook, Docker, Cursor, VS Code, or Chrome in `closeApps` — I have
  real work open in those and quitting them would lose state.
- **Browser URLs**: use harmless ones (`https://example.com/`, `https://www.google.com/`). Chrome is
  installed. Opening tabs is fine; do **not** put the browser itself in `closeApps`.
- Kill any app instance you start before finishing: `pkill -f "bin/Debug/net10.0/ContextSwitcher"`.
- Never `git commit`, `git push`, or `git checkout`/`reset` — I will review changes myself. There is
  uncommitted work in the tree (see below); do not discard it.

## Current state you are inheriting

- Build is **green**, `dotnet test` is **135/135 passing**. Keep it that way.
- `main` is pushed and clean up to commit `23b1978`. **On top of that there is uncommitted
  work-in-progress**: a first-run onboarding wizard (`OnboardingViewModel`, `OnboardingWindow`,
  `AppPickerViewModel`) plus an `OnboardingCompleted` flag on `AppConfiguration`. Leave it alone
  unless it is the thing that is broken.
- `~/.config/ContextSwitcher/settings.json` currently holds **leftover test data** from my session:
  profiles `work` and `personal`, both empty, and the `onboardingCompleted` field is absent. The
  original (which was only an empty `default` profile — nothing valuable) is at
  `~/.config/ContextSwitcher-backup-1786133202/`. Feel free to overwrite the current config with
  your test profiles.
- **Watch out:** `onboardingCompleted` absent means "already onboarded", so the wizard will not
  appear. If you write a config with `"onboardingCompleted": false`, the wizard _will_ open on next
  launch and block the dashboard — that is intended behavior, not a bug.

## Known trip-hazards from previous sessions

- **A clean build does not mean it works.** A LiveCharts2/Avalonia binary incompatibility compiled
  fine and crashed instantly at runtime. Always launch and confirm the process survives:
  `nohup dotnet run --project src/ContextSwitcher.App/ContextSwitcher.App.csproj > /tmp/run.log 2>&1 &`,
  wait ~8s, check `ps aux | grep ContextSwitcher` and that `/tmp/run.log` has no exception.
- **`~/.config/ContextSwitcher/app.log.jsonl` is the fastest way to see what really happened** —
  every switch writes `SwitchStarted`/`SwitchCompleted` plus per-step results with exit codes.
  Check it after every switch.
- `IProcessRunner` enforces `CommandAllowlist` (`osascript`, `open`, `docker`, `shortcuts`, `sips`).
  A blocked executable surfaces as a thrown exception, which in a fire-and-forget path can vanish
  silently. If something does nothing at all, suspect this first.
- Quitting apps is a graceful AppleScript `quit`, not `kill` — an app with unsaved changes may
  refuse and be reported as a warning. That is correct behavior; note it, do not "fix" it.
- Verify library APIs against the installed assembly rather than assuming; guessing method names
  from memory has caused real bugs here.

## Deliverable

A written report covering:

1. What you configured (the two profiles, verbatim from `settings.json`)
2. For each switch (A→B, B→A): which apps actually launched/quit, which URLs actually opened,
   verified independently of the app's own reporting
3. Every discrepancy between what the app _reported_ and what _actually happened_
4. Any bug you found, with the reproduction steps and the relevant `app.log.jsonl` lines
5. Confirmation that you reverted all debug hooks (`git diff` output) and restored my config

Do not fix bugs you find unless they are trivial and you flag them clearly — I want the report
first so I can decide what to prioritize.
