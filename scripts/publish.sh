#!/usr/bin/env bash
#
# Publishes self-contained, single-file desktop builds into ./dist, one folder per
# runtime identifier (RID). Each build bundles the .NET runtime and Avalonia's native
# libraries, so the result is a single executable that runs without a .NET install.
#
# Desktop RIDs covered (Windows / macOS / Linux, x64 + arm64):
#   win-x64  win-arm64  osx-x64  osx-arm64  linux-x64  linux-arm64
#
# .NET cross-publishes self-contained apps from any host OS, so this produces all six
# from a single machine. (The iOS and Android heads need their own SDK workloads — and
# iOS a Mac — so they're not built here.)
#
# The build version comes from Directory.Build.props (<Version>); override it for a
# release by setting the VERSION env var, e.g.  VERSION=1.2.0 scripts/publish.sh
#
# Usage:
#   scripts/publish.sh                 # publish every desktop RID
#   scripts/publish.sh win-x64 osx-arm64   # publish only the RIDs you name
#   VERSION=1.2.0 scripts/publish.sh   # stamp a specific version
#
set -uo pipefail

cd "$(dirname "$0")/.."

PROJECT="src/BardsTale.Desktop/BardsTale.Desktop.csproj"
DIST="$(pwd)/dist"

# An optional version override (defaults to whatever Directory.Build.props declares).
VERSION_ARG=()
[ -n "${VERSION:-}" ] && VERSION_ARG=(-p:Version="$VERSION")

ALL_RIDS=(win-x64 win-arm64 osx-x64 osx-arm64 linux-x64 linux-arm64)
RIDS=("$@")
[ ${#RIDS[@]} -eq 0 ] && RIDS=("${ALL_RIDS[@]}")

echo "Publishing single-file desktop builds to $DIST"
echo "RIDs: ${RIDS[*]}"
echo

declare -a SUMMARY
FAILED=0

for RID in "${RIDS[@]}"; do
  OUT="$DIST/$RID"
  echo "── $RID ───────────────────────────────────────────────"
  rm -rf "$OUT"

  if dotnet publish "$PROJECT" \
      -c Release \
      -r "$RID" \
      --self-contained true \
      -p:PublishSingleFile=true \
      -p:IncludeNativeLibrariesForSelfExtract=true \
      -p:DebugType=none \
      -p:DebugSymbols=false \
      ${VERSION_ARG[@]+"${VERSION_ARG[@]}"} \
      -o "$OUT"; then
    # Find the produced executable (BardsTale.Desktop or BardsTale.Desktop.exe).
    BIN="$(find "$OUT" -maxdepth 1 -name 'BardsTale.Desktop*' ! -name '*.pdb' -type f | head -1)"
    if [ -n "$BIN" ]; then
      SIZE="$(du -h "$BIN" | cut -f1)"
      SUMMARY+=("  ✓ $RID  →  ${BIN#"$(pwd)/"}  ($SIZE)")
    else
      SUMMARY+=("  ✓ $RID  →  $OUT  (built; executable name not matched)")
    fi
  else
    SUMMARY+=("  ✗ $RID  →  publish FAILED")
    FAILED=1
  fi
  echo
done

echo "═══════════════════════════════════════════════════════════"
echo "Publish summary:"
printf '%s\n' "${SUMMARY[@]}"
echo "═══════════════════════════════════════════════════════════"

exit $FAILED
