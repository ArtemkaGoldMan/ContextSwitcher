#!/bin/bash
# Zips dist/ContextSwitcher.app into dist/ContextSwitcher.zip - what the in-app updater and
# install.sh download.
#
#   ./scripts/build-zip.sh [output-dir]      (default: dist/)
#
# The name carries no version on purpose: GitHub serves the newest release's copy at
# releases/latest/download/ContextSwitcher.zip, which is the one address install.sh needs.
# ditto rather than zip, because it keeps the bundle's symlinks, permissions and extended attributes,
# without which the code signature no longer verifies.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out="${1:-$root/dist}"
app="$out/ContextSwitcher.app"
zip="$out/ContextSwitcher.zip"

[ -d "$app" ] || "$root/scripts/build-app.sh" "$out"

rm -f "$zip"
ditto -c -k --keepParent "$app" "$zip"
echo "built $zip ($(du -sh "$zip" | cut -f1))"
