#!/bin/bash
# Starts a built ContextSwitcher.app and checks its icon reaches the menu bar - the app's only way in
# besides the Dock. No unit test can see the menu bar: 0.6.4 to 0.6.6 shipped with no icon at all,
# every test green, after an Avalonia update changed how a tray icon starts. This runs the app as a
# user would and counts the menu bar's items before and after.
#
#   ./scripts/check-menu-bar-icon.sh dist/ContextSwitcher.app
#
# It needs a logged-in screen, which GitHub's macOS runners have. If this machine shows no menu bar
# items at all - not even a test item of its own - it can't tell, says so and passes. It never touches
# the real settings: CONTEXTSWITCHER_CONFIG_DIR points the app somewhere else.
set -euo pipefail

app="${1:?usage: check-menu-bar-icon.sh path/to/ContextSwitcher.app}"
bin="$app/Contents/MacOS/ContextSwitcher"
[ -x "$bin" ] || { echo "no executable at $bin" >&2; exit 1; }
here="$(cd "$(dirname "$0")" && pwd)"

work="$(mktemp -d)"
pid=""
cleanup() {
  if [ -n "$pid" ]; then kill "$pid" 2>/dev/null || true; fi
  rm -rf "$work"
}
trap cleanup EXIT

fail() { echo "FAIL: $*" >&2; exit 1; }

swiftc -O "$here/menu-bar-items.swift" -o "$work/menu-bar-items"
count() { "$work/menu-bar-items" count; }

# Waits up to $1 seconds for more than $2 items; prints how many there were last.
wait_for_more() {
  local now
  for _ in $(seq 1 "$1"); do
    sleep 1
    now="$(count)"
    if [ "$now" -gt "$2" ]; then echo "$now"; return 0; fi
  done
  echo "$now"
  return 1
}

mkdir -p "$work/settings"
cat > "$work/settings/settings.json" <<'JSON'
{
  "schemaVersion": 1,
  "activeContextId": "work",
  "onboardingCompleted": true,
  "checkForUpdates": false,
  "contexts": [
    { "id": "work", "displayName": "Work" },
    { "id": "personal", "displayName": "Personal" }
  ]
}
JSON

before="$(count)"
CONTEXTSWITCHER_CONFIG_DIR="$work/settings" "$bin" >"$work/app.out" 2>&1 &
pid=$!
disown "$pid"

if after="$(wait_for_more 30 "$before")"; then
  kill -0 "$pid" 2>/dev/null || fail "the app quit right after it started: $(cat "$work/app.out")"
  echo "ok   its icon is in the menu bar ($before items before it started, $after after)"
  exit 0
fi
kill -0 "$pid" 2>/dev/null || fail "the app quit right after it started: $(cat "$work/app.out")"
kill "$pid" 2>/dev/null || true
pid=""

# Nothing appeared. Could this machine show a menu bar item at all? A new program's first item can
# take a few seconds to appear on macOS 26, so it gets as long as the app did.
sleep 2
base="$(count)"
"$work/menu-bar-items" show 40 &
disown $!
if wait_for_more 30 "$base" >/dev/null; then
  fail "no ContextSwitcher icon in the menu bar 30 seconds after it started ($before items before, $after after), though a test item of its own did show up"
fi
echo "::warning::This machine shows no menu bar items, not even a test one, so the menu bar icon could not be checked."
