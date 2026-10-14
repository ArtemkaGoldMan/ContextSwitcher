# Security

## Reporting a problem

If you find a way to make Context Switcher do something it shouldn't - run a command it wasn't asked to,
read or change something outside its own settings folder, install something that isn't a release of
this project - please **don't open a public issue**. Report it privately instead:
[Security → Report a vulnerability](https://github.com/ArtemkaGoldMan/ContextSwitcher/security/advisories/new).

Say what you did, what happened, and which version (Settings → Updates). It will be looked at, and
fixed in the next release when it is real.

## What the app can do, and what protects that

- **It controls other apps** (quits them, reads a browser's tabs) only after macOS asks you and you say
  yes, under System Settings → Privacy & Security → Automation. [docs/automation-permissions.md](docs/automation-permissions.md)
  has the list.
- **It updates itself only from this repository's releases,** and installs a download only if it is
  signed with the same certificate as the copy that is running. A file anyone else signed is refused.
  [docs/release-process.md](docs/release-process.md) explains how.
- **Its log has nothing private in it:** web addresses are cut to their site and emails and your user
  name are taken out before anything is written.
- **It makes two kinds of request over the network:** the update check, which asks GitHub for the
  latest release, and - only with *Ask about other apps and tabs* on - fetching the icon of each open
  tab's site from that site. It sends nothing about you in either.

## Supported versions

Only the latest release is supported; the app offers it in Settings → Updates.
