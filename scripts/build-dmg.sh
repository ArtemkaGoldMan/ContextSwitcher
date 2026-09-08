#!/bin/bash
# Packages dist/ContextSwitcher.app into a distributable .dmg.
#
#   ./scripts/build-dmg.sh [output-dir]      (default: dist/)
#
# Builds the app first if it isn't there. The disk image contains the app and a symlink to
# /Applications, so installing is the usual drag across.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out="${1:-$root/dist}"
app="$out/ContextSwitcher.app"

[ -d "$app" ] || "$root/scripts/build-app.sh" "$out"

version="$(/usr/libexec/PlistBuddy -c "Print :CFBundleShortVersionString" "$app/Contents/Info.plist")"
dmg="$out/ContextSwitcher-$version.dmg"

stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT

cp -R "$app" "$stage/ContextSwitcher.app"
ln -s /Applications "$stage/Applications"

# A short README inside the image, because the quarantine step is not optional for an unsigned app
# and a user who hits Gatekeeper with no explanation just deletes the download.
cat > "$stage/READ ME FIRST.txt" <<TXT
ContextSwitcher $version

1. Drag ContextSwitcher.app onto the Applications folder in this window.

2. macOS will refuse to open it the first time. ContextSwitcher is not notarised by Apple -
   that needs a paid Developer ID, and this is a free MIT project. To allow it, run this once
   in Terminal:

       xattr -d com.apple.quarantine /Applications/ContextSwitcher.app

   Or open it once from System Settings > Privacy & Security > Open Anyway.

3. The app lives in the menu bar. Look for the two-squares icon - there is no Dock icon and no
   window until you open one.

Global hotkeys need Accessibility permission (System Settings > Privacy & Security >
Accessibility). Because the app is not signed with a Developer ID, macOS drops that permission
whenever the app is updated, so you will need to grant it again after installing a new version.

Docs: https://github.com/ArtemkaGoldMan/ContextSwitcher
TXT

rm -f "$dmg"
hdiutil create \
  -volname "ContextSwitcher $version" \
  -srcfolder "$stage" \
  -fs HFS+ \
  -format UDZO \
  -ov \
  "$dmg" >/dev/null

echo "built $dmg ($(du -sh "$dmg" | cut -f1))"
