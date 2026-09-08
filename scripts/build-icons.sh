#!/bin/bash
# Regenerates the app icon and the menu bar template from assets-src/icons/*.svg.
#
# Needs rsvg-convert (brew install librsvg); iconutil ships with macOS.
#
# Three drawings, not one scaled drawing: the overlap that carries the idea at 128px turns into an
# unreadable blob at 16px, so the small sizes get progressively simpler artwork. That is normal for
# an icon set and the reason this is a script rather than a single export.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
src="$root/assets-src/icons"
out="$root/src/ContextSwitcher.App/Assets"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

iconset="$work/AppIcon.iconset"
mkdir -p "$iconset"

gen() { rsvg-convert -w "$2" -h "$2" "$src/$1" -o "$iconset/$3"; }

gen app-16.svg      16 icon_16x16.png
gen app-small.svg   32 icon_16x16@2x.png
gen app-small.svg   32 icon_32x32.png
gen app-full.svg    64 icon_32x32@2x.png
gen app-full.svg   128 icon_128x128.png
gen app-full.svg   256 icon_128x128@2x.png
gen app-full.svg   256 icon_256x256.png
gen app-full.svg   512 icon_256x256@2x.png
gen app-full.svg   512 icon_512x512.png
gen app-full.svg  1024 icon_512x512@2x.png

iconutil -c icns "$iconset" -o "$out/AppIcon.icns"

# The menu bar icon is a macOS template image: black plus alpha only. macOS inverts it for light
# and dark itself, so any colour here would be wrong rather than merely ignored.
rsvg-convert -w 36 -h 36 "$src/menu-template.svg" -o "$out/Icons/menu-neutral.png"

echo "wrote $out/AppIcon.icns and $out/Icons/menu-neutral.png"
