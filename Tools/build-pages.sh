#!/usr/bin/env bash
# Builds the browser version and lays it out as a GitHub Pages site:
#   Tools/build-pages.sh            ->  Builds/WebGL (Unity's output) and Builds/pages (the site: index.html, Build/, .nojekyll)
#   PTT_SKIP_UNITY=1 Tools/build-pages.sh   re-lay Builds/pages from the existing Builds/WebGL
# The site is static and works under any subpath (https://nearbycoder.github.io/PackTheTrunk/): every URL in it is
# relative. Serve Builds/pages from a folder named PackTheTrunk to test it as Pages will (Tools/check-pages.mjs).
# Builds/ is gitignored; the site is deployed to the gh-pages branch by hand, not by this script.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD="$ROOT/Builds/WebGL"
SITE="$ROOT/Builds/pages"
LOG="$ROOT/Builds/build-webgl.log"

if [ "${PTT_SKIP_UNITY:-0}" != 1 ]; then
  mkdir -p "$ROOT/Builds"
  echo "Building the WebGL player (log: $LOG)…"
  set +e
  nice -n 10 "$ROOT/Tools/unity.sh" build-webgl > "$LOG" 2>&1
  status=$?
  set -e
  if [ $status -ne 0 ] || ! grep -q "WebGL build Succeeded" "$LOG"; then
    echo "The WebGL build failed (exit $status); see $LOG" >&2
    grep -E "error CS|Error building|Build Failed" "$LOG" | head -20 >&2 || true
    exit 1
  fi
  # A shader that doesn't compile doesn't fail the build: Unity keeps going without it.
  if grep -q "Shader error" "$LOG"; then
    echo "The build has shader errors; see $LOG" >&2
    grep "Shader error" "$LOG" | head -10 >&2
    exit 1
  fi
fi

[ -f "$BUILD/index.html" ] || { echo "No WebGL build in $BUILD" >&2; exit 1; }

rm -rf "$SITE"
mkdir -p "$SITE"
cp -a "$BUILD"/. "$SITE"/
# GitHub Pages runs Jekyll otherwise (which skips files starting with "_" and slows the deploy).
touch "$SITE/.nojekyll"

# GitHub refuses files over 100 MB and warns over 50 MB.
too_big=$(find "$SITE" -type f -size +100M)
if [ -n "$too_big" ]; then
  echo "Files over GitHub's 100 MB limit:" >&2
  echo "$too_big" >&2
  exit 1
fi
find "$SITE" -type f -size +50M -printf "warning: %p is over 50 MB\n"

echo "Site: $SITE ($(du -sh "$SITE" | cut -f1))"
find "$SITE" -type f -printf "%10s  %P\n" | sort -rn | head -8
