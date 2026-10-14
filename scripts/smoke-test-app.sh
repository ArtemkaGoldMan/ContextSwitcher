#!/bin/bash
# Runs a built ContextSwitcher.app through its command line against a throwaway settings folder, and
# checks what a user would notice if the bundle were broken: that it starts at all (the runtime is
# inside it), reads settings, switches, says no to a profile that doesn't exist, stays silent when all
# is well, and writes the whole story of a switch when a step fails.
#
#   ./scripts/smoke-test-app.sh dist/ContextSwitcher.app
#
# It never opens a window, so it works on a machine with no screen - a CI runner - and it never
# touches the real settings: CONTEXTSWITCHER_CONFIG_DIR points the app somewhere else.
set -euo pipefail

app="${1:?usage: smoke-test-app.sh path/to/ContextSwitcher.app}"
bin="$app/Contents/MacOS/ContextSwitcher"
[ -x "$bin" ] || { echo "no executable at $bin" >&2; exit 1; }

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
export CONTEXTSWITCHER_CONFIG_DIR="$work"

fail() { echo "FAIL: $*" >&2; exit 1; }
pass() { echo "ok   $*"; }

cat > "$work/settings.json" <<'JSON'
{
  "schemaVersion": 1,
  "activeContextId": "work",
  "onboardingCompleted": true,
  "checkForUpdates": false,
  "contexts": [
    { "id": "work", "displayName": "Work" },
    { "id": "personal", "displayName": "Personal" },
    { "id": "broken", "displayName": "Broken", "launchApps": ["NoSuchAppAnywhere12345"] }
  ]
}
JSON

"$bin" status >"$work/status.txt" 2>&1 || fail "status did not run: $(cat "$work/status.txt")"
pass "the app starts and reads its settings"

"$bin" switch --context work --force >"$work/switch.txt" 2>&1 || fail "switching to work failed: $(cat "$work/switch.txt")"
grep -q "Succeeded" "$work/switch.txt" || fail "switching to work did not succeed: $(cat "$work/switch.txt")"
"$bin" switch --context personal >/dev/null 2>&1 || fail "switching to personal failed"
"$bin" status | grep -q "personal" || fail "status does not say personal is on after switching to it"
pass "it switches between profiles"

if "$bin" switch --context nobody >/dev/null 2>&1; then
  fail "switching to a profile that does not exist did not fail"
fi
pass "a profile that does not exist is refused"

[ ! -e "$work/app.log.jsonl" ] || fail "the log has something in it after only clean switches:
$(cat "$work/app.log.jsonl")"
pass "the log is silent when all goes well"

"$bin" switch --context broken >"$work/broken.txt" 2>&1 || true
grep -q "RunStarted" "$work/app.log.jsonl" || fail "no header in the log after a problem"
grep -q "SwitchPlanned" "$work/app.log.jsonl" || fail "the log does not say what the switch planned"
grep -q "NoSuchAppAnywhere12345" "$work/app.log.jsonl" || fail "the log does not name what went wrong"
pass "a problem is written with the switch that led to it"

if grep -q "/Users/" "$work/app.log.jsonl"; then
  fail "the log has a path through a home folder in it"
fi
pass "the log has no home folder path in it"

echo "all checks passed"
