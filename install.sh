#!/bin/bash
# Installs ContextSwitcher - or updates it - from the latest GitHub release:
#
#   curl -fsSL https://raw.githubusercontent.com/ArtemkaGoldMan/ContextSwitcher/main/install.sh | bash
#
# Why this is one command: macOS quarantines what a browser downloads and won't open an app that
# isn't notarised by Apple until you allow it. curl's downloads aren't quarantined, so an app put in
# place this way just opens. It is still checked first - its signature has to be intact.
#
# Everything is inside main so that bash has read the whole script before running any of it - with
# `curl | bash`, a command that read standard input would otherwise swallow the rest of the script.
set -euo pipefail

main() {
  local repo="ArtemkaGoldMan/ContextSwitcher"
  local url="https://github.com/$repo/releases/latest/download/ContextSwitcher.zip"
  local target="/Applications/ContextSwitcher.app"

  [ "$(uname -s)" = "Darwin" ] || fail "ContextSwitcher is a macOS app."
  [ "$(uname -m)" = "arm64" ] || fail "this build is for Apple silicon Macs (M1 and later)."
  local major
  major="$(sw_vers -productVersion | cut -d. -f1)"
  [ "$major" -ge 13 ] || fail "macOS 13 Ventura or later is needed."

  work="$(mktemp -d)"
  trap 'rm -rf "$work"' EXIT

  echo "Downloading the latest ContextSwitcher…"
  curl -fL --progress-bar "$url" -o "$work/ContextSwitcher.zip" || fail "the download failed. Is there a release yet? https://github.com/$repo/releases"
  ditto -x -k "$work/ContextSwitcher.zip" "$work/unpacked" || fail "the download couldn't be unpacked."

  local app="$work/unpacked/ContextSwitcher.app"
  [ -d "$app" ] || fail "the download doesn't contain ContextSwitcher.app."
  codesign --verify --deep --strict "$app" 2>/dev/null || fail "the app's signature is broken, so it wasn't installed."
  local version
  version="$(plutil -extract CFBundleShortVersionString raw -o - "$app/Contents/Info.plist")"

  if pgrep -xq ContextSwitcher; then
    echo "Quitting the running copy…"
    osascript -e 'tell application id "com.artem.contextswitcher" to quit' >/dev/null 2>&1 || true
    for _ in $(seq 1 50); do
      pgrep -xq ContextSwitcher || break
      sleep 0.2
    done
    if pgrep -xq ContextSwitcher; then
      fail "ContextSwitcher is still running. Quit it from its menu bar icon and run this again."
    fi
  fi

  if [ -e "$target" ]; then
    rm -rf "$target" || fail "couldn't replace $target."
  fi
  mv "$app" "$target" || fail "couldn't put the app in /Applications."
  # In case an earlier copy came from a browser download; this one never was quarantined.
  xattr -dr com.apple.quarantine "$target" 2>/dev/null || true

  echo "Installed ContextSwitcher $version."
  open "$target"
  echo "It lives in the menu bar - look for the two-squares icon. Later versions install from"
  echo "Settings > Updates, or by running this command again."
}

fail() {
  echo "ContextSwitcher: $*" >&2
  exit 1
}

main "$@"
