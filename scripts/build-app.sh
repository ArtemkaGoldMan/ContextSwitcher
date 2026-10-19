#!/bin/bash
# Builds ContextSwitcher.app - a self-contained Apple silicon bundle.
#
#   ./scripts/build-app.sh [output-dir]      (default: dist/)
#
# Self-contained on purpose: a framework-dependent build would make every user install the .NET
# runtime first, which is a worse first experience than a larger download.
#
# Signed with the project's self-signed certificate (scripts/create-signing-identity.sh), taken from
# the first of:
#   CS_SIGNING_P12_BASE64 + CS_SIGNING_PASSWORD   the release workflow's secrets
#   ~/.contextswitcher-signing/                   this Mac, after create-signing-identity.sh
# With neither it falls back to an ad-hoc signature - fine for trying a build, but such a copy cannot
# update itself and macOS treats every rebuild as a new app. CS_REQUIRE_SIGNING=1 makes that an
# error instead, so a release can never go out ad-hoc by accident.
#
# There is no Apple Developer ID, so either way Gatekeeper blocks the download until the user
# clears the quarantine flag - see docs/release-process.md.
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

# The project builds for Windows as well; net10.0 is the Mac app.
dotnet publish "$proj" \
  --configuration Release \
  --framework net10.0 \
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

# The app's languages, from its translation files: Strings.resx is English, Strings.<code>.resx each
# other language. Declaring them puts the app under System Settings > Language & Region >
# Applications, and macOS shows the Automation prompt's text in the user's language, from a
# <code>.lproj/InfoPlist.strings per language.
resources="$root/src/ContextSwitcher.App/Resources"
usage_of() {
  xmllint --xpath 'string(//data[@name="InfoPlist_AppleEventsUsage"]/value)' "$1"
}
languages=(en)
for file in "$resources"/Strings.*.resx; do
  [ -e "$file" ] || continue
  code="${file##*/Strings.}"
  languages+=("${code%.resx}")
done
localizations=""
for code in "${languages[@]}"; do
  localizations+="<string>$code</string>"
  source="$resources/Strings.$code.resx"
  [ "$code" = en ] && source="$resources/Strings.resx"
  mkdir -p "$app/Contents/Resources/$code.lproj"
  usage="$(usage_of "$source" | sed 's/"/\\"/g')"
  printf '"NSAppleEventsUsageDescription" = "%s";\n' "$usage" > "$app/Contents/Resources/$code.lproj/InfoPlist.strings"
done
english_usage="$(usage_of "$resources/Strings.resx")"

# LSUIElement keeps this a menu bar app: without it macOS shows a Dock icon and an app menu for a moment
# at launch before Avalonia switches to accessory mode. The showDockIcon setting still works - Avalonia
# raises the activation policy at startup when it is on.
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
  <key>CFBundleDevelopmentRegion</key><string>en</string>
  <key>CFBundleLocalizations</key><array>$localizations</array>
  <key>LSMinimumSystemVersion</key><string>13.0</string>
  <key>LSUIElement</key><true/>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSAppleEventsUsageDescription</key>
  <string>$english_usage</string>
</dict>
</plist>
PLIST

identity_dir="$HOME/.contextswitcher-signing"
p12=""
p12_password=""
if [ -n "${CS_SIGNING_P12_BASE64:-}" ]; then
  p12="$publish/signing.p12"
  printf '%s' "$CS_SIGNING_P12_BASE64" | base64 --decode > "$p12"
  p12_password="${CS_SIGNING_PASSWORD:?CS_SIGNING_PASSWORD is required with CS_SIGNING_P12_BASE64}"
elif [ -f "$identity_dir/signing.p12" ]; then
  p12="$identity_dir/signing.p12"
  p12_password="$(cat "$identity_dir/signing-password.txt")"
fi

if [ -n "$p12" ]; then
  # codesign only finds an identity in a keychain on the search list, so the certificate goes into
  # a throwaway keychain that is on the list just for the signing, and the list is put back after -
  # the login keychain is never touched.
  keychain="$publish/signing.keychain-db"
  keychain_password="$(/usr/bin/openssl rand -hex 16)"
  saved_keychains=()
  while IFS= read -r line; do
    line="${line#"${line%%[![:space:]]*}"}"
    line="${line#\"}"
    saved_keychains+=("${line%\"}")
  done < <(security list-keychains -d user)
  restore_keychains() {
    security list-keychains -d user -s "${saved_keychains[@]}"
    security delete-keychain "$keychain" 2>/dev/null || true
  }
  trap 'restore_keychains; rm -rf "$publish"' EXIT

  security create-keychain -p "$keychain_password" "$keychain"
  security set-keychain-settings -lut 900 "$keychain"
  security unlock-keychain -p "$keychain_password" "$keychain"
  security import "$p12" -k "$keychain" -P "$p12_password" -f pkcs12 -T /usr/bin/codesign >/dev/null
  # Lets codesign use the key without a password dialog, which would hang an unattended build.
  security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "$keychain_password" "$keychain" >/dev/null
  security list-keychains -d user -s "$keychain" "${saved_keychains[@]}"

  # By hash, not name: the certificate is self-signed, so macOS calls it untrusted and codesign's
  # lookup by name skips it.
  identity="$(security find-identity -p codesigning "$keychain" | awk '/[0-9]\)/ { print $2; exit }')"
  [ -n "$identity" ] || { echo "no signing identity in $p12" >&2; exit 1; }

  codesign --force --deep --keychain "$keychain" --sign "$identity" --identifier "$bundle_id" "$app" 2>&1 | sed 's/^/  /'
  restore_keychains
  trap 'rm -rf "$publish"' EXIT
elif [ "${CS_REQUIRE_SIGNING:-}" = "1" ]; then
  echo "CS_REQUIRE_SIGNING=1 but no signing identity was found - see scripts/create-signing-identity.sh" >&2
  exit 1
else
  echo "  no signing identity - signing ad-hoc. This build cannot update itself; see scripts/create-signing-identity.sh"
  codesign --force --deep --sign - --identifier "$bundle_id" "$app" 2>&1 | sed 's/^/  /'
fi

codesign --verify --deep --strict "$app" && echo "  signature verifies"
echo "  identifier: $(codesign -dvvv "$app" 2>&1 | sed -n 's/^Identifier=//p')"
echo "  $(codesign -d -r- "$app" 2>/dev/null | sed -n 's/^#* *designated => /requirement: /p')"
echo "  size: $(du -sh "$app" | cut -f1)"
echo "built $app"
