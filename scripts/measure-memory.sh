#!/bin/bash
# Measures what the app costs in memory: a Release build, run against a throwaway settings folder
# (never the real one), once with no window open and once with the dashboard open.
#
#   ./scripts/measure-memory.sh [seconds-to-settle]      (default: 25)
#
# "Footprint" is what Activity Monitor's Memory column shows; resident size also counts the shared
# libraries every Mac app maps, so it says little. Set CONTEXTSWITCHER_RENDERING=software to measure
# CPU drawing instead of Metal. See docs/performance.md.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
settle="${1:-25}"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

dotnet publish "$root/src/ContextSwitcher.App/ContextSwitcher.App.csproj" \
  --configuration Release --framework net10.0 --runtime osx-arm64 --self-contained true \
  --output "$work/app" --nologo --verbosity quiet

measure() {
  local label="$1"; shift
  local config="$work/config-$RANDOM"
  mkdir -p "$config"
  # Onboarding completed, so the run is the menu bar app alone - a new install would open its wizard.
  cat > "$config/settings.json" <<'JSON'
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
  CONTEXTSWITCHER_CONFIG_DIR="$config" "$work/app/ContextSwitcher" "$@" >/dev/null 2>&1 &
  local pid=$!
  sleep "$settle"
  if ! kill -0 "$pid" 2>/dev/null; then
    echo "$label: the app exited"
    return 1
  fi
  local footprint
  footprint="$(footprint -p "$pid" 2>/dev/null | awk '/Footprint:/ {print $5, $6; exit}')"
  printf '%-22s footprint %-8s  cpu time %s\n' "$label" "$footprint" "$(ps -o cputime= -p "$pid" | tr -d ' ')"
  kill "$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true
}

measure "menu bar only"
measure "dashboard open" open-dashboard
