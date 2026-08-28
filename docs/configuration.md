# Configuration

Everything ContextSwitcher does is driven by one file:

```
~/.config/ContextSwitcher/settings.json
```

The UI writes it, and you can edit it by hand. The app validates the whole file before saving and
refuses to write an invalid one, so a typo in the UI can't corrupt your setup. If the file on disk is
ever unreadable JSON, it's moved aside as `settings.json.corrupt.<timestamp>` and a fresh default is
created — your old one is still there, and so are the last ten backups in `backups/`.

Run this any time to check a hand-edit:

```bash
ContextSwitcher validate-config
```

## What lives where

| File | Contents |
| --- | --- |
| `settings.json` | Profiles, hotkeys, app settings. The only file you'd edit. |
| `state.json` | Which profile is active, and how the last switch went. |
| `analytics.jsonl` | Local time tracking, one line per session. |
| `app.log.jsonl` | Structured log, one line per event. First place to look when something misbehaves. |
| `backups/` | The last ten copies of each file, written before every save. |
| `icon-cache/` | Extracted app icons for the picker. Safe to delete. |

## Top level

```json
{
  "schemaVersion": 1,
  "activeContextId": "work",
  "defaultSwitchTimeoutSeconds": 45,
  "showDockIcon": false,
  "onboardingCompleted": true,
  "analytics": { "enabled": true, "retentionDays": 365 },
  "hotkeys": [],
  "contexts": []
}
```

| Field | Notes |
| --- | --- |
| `schemaVersion` | Always `1` today. |
| `activeContextId` | Must match a `contexts[].id`. |
| `showDockIcon` | Read once at startup — changing it needs a restart, and the UI says so. |
| `onboardingCompleted` | Absent means "already onboarded", so upgrading never re-runs the wizard. |
| `analytics.retentionDays` | Sessions older than this are pruned at startup. |

## A profile

Each entry in `contexts[]` is one profile. Only `id` and `displayName` are required; every other
field has a sensible default, and an omitted section simply does nothing.

```json
{
  "id": "work",
  "displayName": "Work",
  "menuBarLabel": "WORK",
  "accentColor": "#2F6FED",
  "icon": "briefcase",

  "launchApps": ["Slack", "Visual Studio Code"],
  "closeApps": ["Slack", "Visual Studio Code"],

  "browser_management": {
    "mode": "urls",
    "browser": "Chrome",
    "urls": ["https://mail.google.com/"],
    "tab_groups": [],
    "profiles": [],
    "avoid_duplicate_tabs": true
  },

  "theme": { "mode": "light" },
  "wallpaper": { "path": "", "allSpaces": true },
  "focus": { "enabled": true, "modeName": "Work" },
  "media": { "player": "None", "playlist": "", "autoPlay": false },
  "docker": { "start": [], "stop": [] },

  "quickLinks": [{ "title": "Team board", "url": "https://example.com/board", "icon": "link" }],
  "notes": ["Check the incident queue before opening the IDE."],
  "switchPolicy": { "continueOnNonCriticalFailure": true, "criticalSteps": [] }
}
```

`id` must be lowercase and URL-safe, and it's permanent once saved — hotkeys, `state.json` and your
recorded time all reference it.

## Apps: launch on enter, quit on leave

This is the one part worth reading carefully, because the two lists point in **opposite directions**.

- `launchApps` — opened when you **enter** this profile
- `closeApps` — quit when you **leave** this profile

So both lists belong to the profile that *owns* those apps. A Work profile that launches Slack and
quits Slack means: arriving at work opens Slack, leaving work closes it. That's why the Profile Setup
UI shows one row per app with two checkboxes, and why both are ticked by default.

To have an app quit as you *arrive* somewhere, put it in the `closeApps` of the profile you're
arriving **from** — not the one you're arriving at.

Quitting is a graceful AppleScript quit, the same as pressing `Cmd+Q`. An app with unsaved work will
show its save dialog and refuse; that's reported as a warning naming the app, and the switch carries
on. ContextSwitcher never force-kills anything.

## Browser

| `mode` | Behaviour |
| --- | --- |
| `none` | Don't touch the browser. |
| `urls` | Open `urls[]`. |
| `groups` | Activate the named `tab_groups[]`, falling back to `urls[]` if that fails. |
| `profiles` | Launch each entry in `profiles[]` with its own `--profile-directory`. |

`browser` is `Default`, `Chrome`, `Brave` or `Safari`.

**`avoid_duplicate_tabs` needs a named browser.** Finding an already-open tab means scripting a
specific browser, and the *default* browser can't be identified ahead of time — so with
`"browser": "Default"` the setting has no effect and every switch opens a fresh tab. Profile Setup
disables the toggle and explains this when the browser is `Default`. Pick a concrete browser if you
want it.

Chrome's scripting dictionary doesn't reliably expose tab groups, so `groups` mode against Chrome
generally falls back to opening `urls[]`. That's expected, and the fallback is why you should set
`urls[]` even in `groups` mode.

## Theme, wallpaper, Focus, media, Docker

**`theme.mode`** is `light`, `dark` or `system`. `system` builds no step at all and leaves your
appearance alone.

**`wallpaper.path`** must be a real image. A missing file, or a file that isn't an image, is reported
as a warning and your wallpaper is left untouched.

**`focus`** runs a Shortcut you create, because macOS has no Focus scripting API. With
`enabled: true` it runs `ContextSwitcher - Focus <modeName>`; leaving a profile that had Focus on
runs `ContextSwitcher - Focus Off`. If neither the profile you're leaving nor the one you're entering
uses Focus, no Focus step runs — so you never see a warning for a feature you're not using. Setup is
in [shortcuts-integration.md](shortcuts-integration.md).

**`media.player`** is `AppleMusic`, `Spotify` or `None`. Nothing plays unless `autoPlay` is `true`.

**`docker.start` / `docker.stop`** take container names. `start` runs on entering the profile,
`stop` on leaving it — the same direction as the app lists.

## Hotkeys

```json
"hotkeys": [
  { "id": "switch-work", "contextId": "work", "accelerator": "Cmd+Alt+Ctrl+W", "enabled": true }
]
```

Modifiers are `Cmd`, `Ctrl`, `Alt`, `Shift`, joined with `+`. Editing a hotkey takes effect
immediately — no restart. Global hotkeys need Accessibility permission; without it the app logs that
it can't register and hotkeys simply never fire.

## Switch policy

```json
"switchPolicy": { "continueOnNonCriticalFailure": true, "criticalSteps": ["LaunchApplications"] }
```

By default a failing step is a warning and the switch continues. Naming a step type in
`criticalSteps` makes its failure stop the switch and report `Failed`. Valid values are the
`AutomationStepType` names: `CloseApplications`, `LaunchApplications`, `ManageBrowserContext`,
`SetTheme`, `SetWallpaper`, `SetFocusMode`, `ControlMedia`, `StartDockerResources`,
`StopDockerResources`.

Criticality applies to the whole step, not to one app inside it.

## Two complete profiles

```json
{
  "schemaVersion": 1,
  "activeContextId": "work",
  "hotkeys": [
    { "id": "switch-work", "contextId": "work", "accelerator": "Cmd+Alt+Ctrl+W", "enabled": true },
    { "id": "switch-personal", "contextId": "personal", "accelerator": "Cmd+Alt+Ctrl+P", "enabled": true }
  ],
  "contexts": [
    {
      "id": "work",
      "displayName": "Work",
      "menuBarLabel": "WORK",
      "accentColor": "#2F6FED",
      "icon": "briefcase",
      "launchApps": ["Slack", "Visual Studio Code"],
      "closeApps": ["Slack", "Visual Studio Code"],
      "browser_management": {
        "mode": "urls",
        "browser": "Chrome",
        "urls": ["https://mail.google.com/", "https://github.com/notifications"],
        "avoid_duplicate_tabs": true
      },
      "theme": { "mode": "light" },
      "focus": { "enabled": true, "modeName": "Work" },
      "docker": { "start": ["postgres-work"], "stop": [] },
      "notes": ["Check the incident queue before opening the IDE."]
    },
    {
      "id": "personal",
      "displayName": "Personal",
      "menuBarLabel": "HOME",
      "accentColor": "#20A67A",
      "icon": "home",
      "launchApps": ["Spotify"],
      "closeApps": ["Spotify"],
      "browser_management": {
        "mode": "urls",
        "browser": "Chrome",
        "urls": ["https://youtube.com/"],
        "avoid_duplicate_tabs": true
      },
      "theme": { "mode": "dark" },
      "focus": { "enabled": false, "modeName": "" },
      "docker": { "start": [], "stop": ["postgres-work"] }
    }
  ]
}
```

Switching **work → personal** here: Slack and VS Code quit, `postgres-work` stops, the theme goes
dark, Focus turns off, Spotify opens and YouTube loads in Chrome.

## When something doesn't happen

`app.log.jsonl` records every step of every switch with its status, duration, exit code and trimmed
error output:

```bash
tail -20 ~/.config/ContextSwitcher/app.log.jsonl | python3 -m json.tool --json-lines
```

Or preview a switch without applying anything:

```bash
ContextSwitcher switch --context work --dry-run
```
