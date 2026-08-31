#!/bin/bash
# Builds ContextSwitcher.app - a self-contained Apple silicon bundle, ad-hoc signed.
#
#   ./scripts/build-app.sh [output-dir]      (default: dist/)
#
# Self-contained on purpose: a framework-dependent build would make every user install the .NET
# runtime first, which is a worse first experience than a larger download.
#
# Ad-hoc signed, because there is no Apple Developer ID. Two consequences worth knowing, both
# documented in docs/release-process.md: Gatekeeper blocks the download until the user clears the
# quarantine flag, and macOS drops the Accessibility grant on every update because an ad-hoc
# signature pins the code hash. Hotkeys stop working until the user re-grants it.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out="${1:-$root/dist}"
proj="$root/src/ContextSwitcher.App/ContextSwitcher.App.csproj"
app="$out/ContextSwitcher.app"

bundle_id="com.artem.contextswitcher"
version="$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$proj" | head -1)"
[ -n "$version" ] || { echo "could not read <Version> from the csproj" >&2; exit 1; }

echo "building ContextSwitcher $version (osx-arm64, self-contained)"

publish="$(mktemp -d)"
trap 'rm -rf "$publish"' EXIT

dotnet publish "$proj" \
  --configuration Release \
  --runtime osx-arm64 \
  --self-contained true \
  --output "$publish" \
  --nologo \
  --verbosity quiet

rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -R "$publish"/. "$app/Contents/MacOS"/
cp "$root/src/ContextSwitcher.App/Assets/AppIcon.icns" "$app/Contents/Resources/AppIcon.icns"
chmod +x "$app/Contents/MacOS/ContextSwitcher"

# LSUIElement keeps this a menu bar app: without it macOS shows a Dock icon and an app menu for a
# moment at launch before Avalonia switches to accessory mode. The showDockIcon setting still works
# - Avalonia raises the activation policy at startup when it is on.
cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>ContextSwitcher</string>
  <key>CFBundleDisplayName</key><string>Context Switcher</string>
  <key>CFBundleIdentifier</key><string>$bundle_id</string>
  <key>CFBundleExecutable</key><string>ContextSwitcher</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleVersion</key><string>$version</string>
  <key>CFBundleShortVersionString</key><string>$version</string>
  <key>LSMinimumSystemVersion</key><string>13.0</string>
  <key>LSUIElement</key><true/>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSAppleEventsUsageDescription</key>
  <string>ContextSwitcher quits and launches apps, opens browser tabs, and changes appearance when you switch profiles.</string>
</dict>
</plist>
PLIST

codesign --force --deep --sign - --identifier "$bundle_id" "$app" 2>&1 | sed 's/^/  /'

codesign --verify --deep --strict "$app" && echo "  signature verifies"
echo "  identifier: $(codesign -dvvv "$app" 2>&1 | sed -n 's/^Identifier=//p')"
echo "  size: $(du -sh "$app" | cut -f1)"
echo "built $app"
