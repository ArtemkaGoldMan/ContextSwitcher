# Automation Permissions

Context Switcher controls other apps and system settings through AppleScript and global hotkeys.
macOS gates both behind explicit user permission. This page explains what each permission is for,
how to grant it, and how to tell when a missing permission — rather than a bug — is the reason a
switch step failed.

## Automation permission (AppleScript / `osascript`)

**What it's for:** every `SetTheme`, `SetWallpaper`, `CloseApplications`, `ManageBrowserContext`,
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
*"Could not change theme. Check Automation permissions."* — the AppleScript ran, macOS silently
blocked it, and `osascript` returned a non-zero exit code.

## Accessibility permission (global hotkeys)

**What it's for:** global hotkeys are implemented with `SharpHook`, a cross-platform keyboard hook.
On macOS this requires **Accessibility** access, not Automation.

**Granting it:**

1. Open **System Settings → Privacy & Security → Accessibility**.
2. Enable **ContextSwitcher**.
3. Restart the app — the hotkey hook checks this once at startup, not continuously.

**Symptom of a missing grant:** hotkeys silently do nothing (macOS doesn't error, it just never
delivers key events to an unauthorized hook). Context Switcher detects this proactively at startup
and logs a `HotkeyPermissionMissing` warning to `~/.config/ContextSwitcher/app.log.jsonl` instead of
failing silently — check there if your hotkeys aren't firing.

> Unsigned/self-signed builds: because the app isn't signed with a paid Apple Developer ID, macOS
> ties this permission grant to the exact build's code signature. If you rebuild from source with a
> different signing identity, you'll need to re-grant Accessibility access. Official releases use a
> stable self-signed certificate specifically so this only needs to be granted once across updates
> (see agent.md section 15.2).

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
| Theme/wallpaper doesn't change | Automation permission denied for System Events | Grant it under Privacy & Security → Automation |
| App doesn't quit/launch | Automation permission denied for that app, or app not installed | Check Automation settings; verify the app name matches exactly |
| Hotkeys do nothing | Accessibility permission not granted | Grant it, then restart the app |
| Focus mode doesn't change | Required Shortcut missing or Shortcuts automation blocked | See `docs/shortcuts-integration.md` |
| Docker steps fail | Docker CLI not installed or daemon not running | Install Docker Desktop / start the daemon |
| A step reports `Skipped` | That automation isn't implemented yet in this phase | Check agent.md's roadmap for the target phase |

Every step's outcome (`Succeeded`, `Warning`, `Failed`, `Skipped`, `TimedOut`) and message are
recorded in the switch result and in `~/.config/ContextSwitcher/app.log.jsonl` — that log is always
the fastest way to find out exactly what happened and why.
