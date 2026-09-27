# Automation Permissions

Context Switcher controls other apps through AppleScript, which macOS gates behind an explicit
Automation permission. This page explains what that permission is for,
how to grant it, and how to tell when a missing permission — rather than a bug — is the reason a
switch step failed.

## Automation permission (AppleScript / `osascript`)

**What it's for:** every `CloseApplications`, `ManageBrowserContext`,
and media-control step runs through `osascript`, which drives other apps and `System Events` via
AppleScript.

**First-run prompt:** the first time Context Switcher scripts a given app (e.g. Slack, Safari,
System Events, Music, Spotify), macOS shows a one-time dialog: *"ContextSwitcher" wants access to
control "System Events"*. Click **OK**. You'll see one prompt per target app the first time it's
automated, not one prompt total.

**Checking or fixing it manually:**

1. Open **System Settings → Privacy & Security → Automation**.
2. Find **ContextSwitcher** in the list.
3. Make sure the apps you use it with (System Events, Music, Spotify, Safari, Chrome, Brave, etc.)
   are toggled on.

If you accidentally denied a prompt, the toggle for that specific app won't reappear on its own —
you have to switch it on manually here.

**Symptom of a missing grant:** a switch step returns `Warning` or `Failed` with a message like
*"Could not check Google Chrome for existing tabs, so URLs were opened without duplicate checking. Check Automation permissions."* — the AppleScript ran, macOS silently
blocked it, and `osascript` returned a non-zero exit code.

## Music and Spotify automation

Apple Music and Spotify are each controlled via their own AppleScript dictionary (`tell application
"Music" ...` / `tell application "Spotify" ...`). Both fall under the general **Automation**
permission above — there's no separate music-specific toggle. If media control isn't working:

- Confirm the target app (Music or Spotify) is actually installed.
- Confirm it's allowed under System Settings → Privacy & Security → Automation → ContextSwitcher.
- For Spotify, prefer a `spotify:playlist:...` URI in your context's `media.playlist` setting over a
  plain playlist name — Spotify's scripting dictionary plays URIs reliably; plain names are
  best-effort and may not resolve to anything.
- Media failures never fail a context switch outright unless you've explicitly marked
  `ControlMedia` as a critical step for that context (and Spotify failures are always treated as
  non-critical, regardless of that setting).

## Troubleshooting checklist

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| App doesn't quit/launch | Automation permission denied for that app, or app not installed | Check Automation settings; verify the app name matches exactly |
| Focus mode doesn't change | Required Shortcut missing or Shortcuts automation blocked | See `docs/shortcuts-integration.md` |
| Docker steps fail | Docker CLI not installed or daemon not running | Install Docker Desktop / start the daemon |
| A step reports `Skipped` | That automation isn't implemented yet in this phase | Check agent.md's roadmap for the target phase |

Every step's outcome (`Succeeded`, `Warning`, `Failed`, `Skipped`, `TimedOut`) and message are
recorded in the switch result and in `~/.config/ContextSwitcher/app.log.jsonl` — that log is always
the fastest way to find out exactly what happened and why.
