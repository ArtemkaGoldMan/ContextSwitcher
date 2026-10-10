# Shortcuts and Siri Integration

There are two independent integrations with the macOS Shortcuts app, in opposite directions:

1. **ContextSwitcher → Shortcuts**: during a switch, the app runs a Shortcut you create to change
   macOS Focus mode (Focus mode has no direct AppleScript API, so Shortcuts is the bridge).
2. **Shortcuts/Siri → ContextSwitcher**: you create a Shortcut that runs ContextSwitcher's CLI, so
   you can trigger a context switch by voice ("Hey Siri, switch to work") or from the Shortcuts app,
   menu bar, or Spotlight.

## 1. Focus mode shortcuts (ContextSwitcher runs these)

macOS gives other apps no way to switch Focus; the Shortcuts "Set Focus" action is the only
supported route. So a switch runs a Shortcut.

Entering a context with `focus.enabled: true` runs:

```text
shortcuts run "ContextSwitcher - Focus <ModeName>"
```

where `<ModeName>` is exactly the context's `focus.modeName`. Leaving such a context for one
**without** Focus runs that mode's own Off Shortcut:

```text
shortcuts run "ContextSwitcher - Focus Off - <ModeName>"
```

and, only if that one does not exist, the original single `ContextSwitcher - Focus Off`.

If neither the context being left nor the one being entered uses Focus, no Focus step runs at all,
so nobody gets a warning for a feature they never set up. Focus enabled with no mode named counts as
not using Focus.

### Creating them: the "Create it" button

For the Focus modes macOS ships with - Do Not Disturb, Work, Personal, Sleep, Reduce Interruptions,
Reading, Fitness, Gaming, Mindfulness - you don't build anything by hand. In Profile Setup, choose
the **Focus mode**; under it, each missing Shortcut has a **Create it** button. It writes the
Shortcut, signs it with the system's `shortcuts sign`, and opens it, and Shortcuts asks you to
**Add Shortcut**. The profile notices once it's added and the mode turns **Ready**.

Each mode gets its own Off Shortcut on purpose. A single Off Shortcut that turned off every built-in
mode was tried first, and failed: turning off a Focus the Mac has never had set up (here,
"Reading") is an error that stops the Shortcut, so it never reached the mode that was actually on.
If you still have that earlier `ContextSwitcher - Focus Off` Shortcut, you can delete it.

### By hand, for a Focus you made yourself

A Focus you created has an identifier other apps can't see, so it can't be written for you:

1. Open the **Shortcuts** app and create a new shortcut.
2. Name it **exactly** `ContextSwitcher - Focus <ModeName>` (case-sensitive).
3. Add **Set Focus**, set it to turn your Focus **on** **Until Turned Off**, and save.
4. Make a second one named `ContextSwitcher - Focus Off - <ModeName>` that turns it **off**.

If a Shortcut is missing, the switch's warning says which one and how to create it. If it exists but
fails, the warning gives macOS's own reason - for example a Focus that doesn't exist on this Mac.

## 2. Triggering ContextSwitcher from Shortcuts or Siri

Add a **Run Shell Script** action to any shortcut, pointing at the installed binary:

```text
/Applications/ContextSwitcher.app/Contents/MacOS/ContextSwitcher switch --context work
```

Then tap **Add to Siri** on that shortcut and record a phrase, e.g. "Switch to work". Example
phrases:

- "Switch to work" → `ContextSwitcher switch --context work`
- "Switch to personal" → `ContextSwitcher switch --context personal`

Because these commands run headlessly (no window opens, no Dock icon appears — see agent.md
section 5), they're fast and unobtrusive to trigger from Siri or a keyboard shortcut manager.

## CLI command reference

All CLI commands exit without starting the GUI (except `open-dashboard`, which opens it):

```bash
ContextSwitcher switch --context work        # switch to a context
ContextSwitcher switch --context work --json # same, machine-readable output
ContextSwitcher status                        # show current context and last switch outcome
ContextSwitcher list-contexts                  # list configured context ids and names
ContextSwitcher validate-config                # check settings.json for errors
ContextSwitcher open-dashboard                 # launch the app and open the dashboard
```

Exit codes (useful for Shortcuts' "If" / error-handling actions):

| Code | Meaning |
| --- | --- |
| 0 | Success |
| 1 | General failure |
| 2 | Invalid arguments |
| 3 | Unknown context |
| 4 | Configuration invalid |
| 5 | Switch completed with warnings |
| 6 | Another switch is already running |
