# Release process

> **Status: not implemented yet.** There is no `.github/` directory, no packaging script and no
> published release. This document is the intended process, written down so the work has a target
> rather than being invented during the first release. Everything below is a plan; nothing here runs
> today.

## What a release contains

A GitHub Release for tag `vX.Y.Z`, containing `ContextSwitcher-X.Y.Z.dmg` — an Apple silicon
(`osx-arm64`) build of the app.

## Signing: what we can and can't do

There is **no Apple Developer ID certificate**, and buying one is not currently planned. That has two
consequences, and both need to be honest in the release notes rather than discovered by users.

**Gatekeeper will block the download.** Without notarisation macOS refuses to open the app on first
launch. Users clear it once:

```bash
xattr -d com.apple.quarantine /Applications/ContextSwitcher.app
```

or use **System Settings → Privacy & Security → Open Anyway**. This is normal for unsigned
open-source Mac apps, but it must be in the README and in every release note.

**Accessibility permission breaks on every update.** macOS ties that grant to the app's code
identity. An ad-hoc signature (`codesign -s -`) pins the code hash, so a new build is a new identity
even when `CFBundleIdentifier` is unchanged — this was measured, not assumed: dropping a fresh build
into a signed bundle and re-signing changed the cdhash and the grant was dropped.

The practical effect is that **global hotkeys stop working after an update** until the user grants
Accessibility again. Settings already shows the permission as "Not granted" when this happens, so the
app can tell them — the release notes should too.

A paid Developer ID is the only thing that fixes either problem.

## Building the bundle

The app needs to be a real `.app` bundle, not a bare executable — otherwise it has no icon, no Dock
presence, and no identity macOS can attach permissions to at all.

```bash
dotnet publish src/ContextSwitcher.App/ContextSwitcher.App.csproj \
  -c Release -r osx-arm64 --self-contained
```

Then assemble:

```
ContextSwitcher.app/
  Contents/
    Info.plist          CFBundleIdentifier com.artem.contextswitcher
                        CFBundleIconFile   AppIcon
                        LSUIElement        true      (menu bar app, no Dock icon)
    MacOS/              the publish output
    Resources/
      AppIcon.icns      from src/ContextSwitcher.App/Assets/AppIcon.icns
```

and sign ad-hoc with a stable identifier:

```bash
codesign --force --deep --sign - --identifier com.artem.contextswitcher ContextSwitcher.app
```

`LSUIElement` matters: without it a menu bar app also gets a Dock icon and an app menu, which is not
what this app is.

## Cutting a release

1. Update the version in the `.csproj` and in `Info.plist`.
2. Confirm `dotnet build` is warning-free and `dotnet test` is green.
3. Tag: `git tag vX.Y.Z && git push --tags`.
4. The release workflow builds, packages the `.dmg`, and creates the GitHub Release.
5. Write release notes including the quarantine command and, when hotkeys are affected, the
   re-grant note above.

## Versioning

Semantic versioning. The one project-specific rule: **treat a change to `settings.json`'s schema as
breaking**, because people hand-edit that file. A `schemaVersion` bump needs a migration path and a
release note, not just a new field.

## Checklist before tagging

- [ ] `dotnet build` — no warnings
- [ ] `dotnet test` — all green
- [ ] App launches and survives; the menu bar icon appears
- [ ] A switch completes and `state.json` updates
- [ ] Bundle opens on a machine that has never run it, after the quarantine command
- [ ] README install steps followed literally on that machine
- [ ] Release notes mention quarantine, and the Accessibility re-grant if relevant
