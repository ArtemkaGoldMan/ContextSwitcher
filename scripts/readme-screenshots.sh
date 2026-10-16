#!/usr/bin/env bash
# Redraws the screenshots of the main window in the README and the user guide - the Profiles page,
# a switch's warnings card and Settings, light and dark - into docs/images, from the app's own pages
# with demo profiles.
# The drawing is a test (src/ContextSwitcher.Tests/Ui/ReadmeScreenshots.cs) that does nothing unless
# CONTEXTSWITCHER_SCREENSHOTS names a folder. Run it after changing what those pages look like.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
CONTEXTSWITCHER_SCREENSHOTS="$root/docs/images" \
  dotnet test "$root/src/ContextSwitcher.Tests" --filter "FullyQualifiedName~ReadmeScreenshots" --nologo
echo "Redrawn in docs/images: profiles, switch-problems and settings, light and dark."
