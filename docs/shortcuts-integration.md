# Shortcuts and Siri Integration

There are two independent integrations with the macOS Shortcuts app, in opposite directions:

1. **ContextSwitcher → Shortcuts**: during a switch, the app runs a Shortcut you create to change
   macOS Focus mode (Focus mode has no direct AppleScript API, so Shortcuts is the bridge).
2. **Shortcuts/Siri → ContextSwitcher**: you create a Shortcut that runs ContextSwitcher's CLI, so
   you can trigger a context switch by voice ("Hey Siri, switch to work") or from the Shortcuts app,
   menu bar, or Spotlight.

## 1. Focus mode shortcuts (ContextSwitcher runs these)

For each context that has `focus.enabled: true`, ContextSwitcher runs:

```text
shortcuts run "ContextSwitcher - Focus <ModeName>"
```

where `<ModeName>` is exactly the context's `focus.modeName` value from `settings.json`. Switching
to a context with `focus.enabled: false` **from one that had it enabled** instead runs:

```text
shortcuts run "ContextSwitcher - Focus Off"
```

If neither the context being left nor the one being entered enables Focus, no Focus step runs at
all. That matters: without it, anyone who has not created these Shortcuts would get a warning on
every single switch for a feature they never asked for.

**You must create these Shortcuts yourself** — ContextSwitcher does not create them for you, and a
missing Shortcut just produces a `Warning` (or `Failed`, if you've marked focus critical for that
context), not a crash.

To create one:

1. Open the **Shortcuts** app.
2. Click **+** to create a new shortcut.
3. Name it **exactly** `ContextSwitcher - Focus Work` (matching your `modeName`, case-sensitive).
4. Add the **Set Focus** action (search "Focus" in the action library).
5. Configure it to turn **on** the Focus you want (e.g. your "Work" Focus, which must already exist
   under System Settings → Focus) for **"Until Turned Off"**.
6. Save.
7. Repeat for every other `modeName` your contexts use.
8. Create one more named exactly `ContextSwitcher - Focus Off`, with a **Set Focus** action
   configured to turn Focus **off**.

If a Shortcut is missing or its name doesn't match exactly, `shortcuts run` fails with a non-zero
exit code — ContextSwitcher reports this as a warning pointing back to this doc, not a silent no-op.

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
