# Permissions

ContextSwitcher needs one macOS permission: to **control other apps**. Opening apps, web pages and
Docker containers needs nothing. Quitting apps, reading your browser's tabs and starting music do —
macOS calls this *Automation*.

## Allowing it

You don't need to do anything in advance. The first time a switch needs to control an app — Slack
to quit it, Chrome to see its tabs, Music to start a playlist, *System Events* to see what's
running — macOS asks:

> "ContextSwitcher" wants access to control "System Events".

Click **OK**. macOS asks once **per app**, the first time that app is controlled, not once overall.

**Settings → Permission** in ContextSwitcher shows whether it's allowed:

- **Allowed** — all good.
- **Not allowed** — click **Open System Settings ↗**, find **ContextSwitcher** and switch on the apps
  it should control. Then click **Check again**.

## If you clicked "Don't Allow"

macOS doesn't ask a second time. Turn it on yourself:

1. Open **System Settings → Privacy & Security → Automation**.
2. Find **ContextSwitcher**.
3. Switch on the app you blocked — **System Events**, your browser, **Music**, **Spotify**…

## After an update

Nothing to redo. Every release is signed with the same certificate, so macOS knows it's the same
app and keeps what you allowed. (A copy you build yourself is signed differently, and macOS will ask
again for it.)

## Telling a missing permission from a bug

A step that macOS blocked doesn't stop the switch. It shows up in the card at the bottom of the
Profiles page and in the dashboard's **Last switch** section, with a message such as:

> Could not check Google Chrome for existing tabs, so URLs were opened without duplicate checking.
> Check Automation permissions.

| What happened | Likely reason | What to do |
| --- | --- | --- |
| An app didn't quit | It had unsaved work, or quitting it isn't allowed | Save and switch again; check Automation for that app |
| Pages opened twice | Reading the browser's tabs isn't allowed | Allow your browser under Automation |
| Music didn't start | Music or Spotify isn't allowed, or isn't installed | Allow it under Automation |
| Focus didn't change | The Focus shortcut is missing | Edit the profile and click **Create it** — see [Focus and Siri](shortcuts-integration.md) |
| Docker containers didn't start or stop | Docker Desktop isn't running | Start Docker Desktop |

For the full story of a switch — every step, how long it took, and what the system said — look at
the log:

```bash
tail -20 ~/.config/ContextSwitcher/app.log.jsonl
```

## Music and Spotify

Both are controlled through Automation; there's no separate setting. For Spotify, paste the
playlist's link (**Share → Copy link**) into the profile — Spotify only plays reliably from a link,
not a name.
