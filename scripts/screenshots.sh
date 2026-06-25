#!/usr/bin/env bash
#
# Regenerates the README screenshots into ./SCREENSHOTS by launching the desktop app
# in capture mode (the BT_SHOT environment variable). The app drives itself through the
# town hub, every shop, the catacombs, a battle and the bestiary, rendering each screen
# to a PNG via Avalonia's RenderTargetBitmap. A macOS/desktop session is required (it runs
# the real UI to capture real fonts, theme and layout).
#
# Usage:  scripts/screenshots.sh
#
set -euo pipefail

cd "$(dirname "$0")/.."
OUT="$(pwd)/SCREENSHOTS"
mkdir -p "$OUT"

echo "Capturing screenshots to $OUT ..."
BT_SHOT=1 BT_SHOT_DIR="$OUT" dotnet run --project src/BardsTale.Desktop -c Release
echo "Done. Wrote PNGs to $OUT"
