#!/usr/bin/env bash
# Zips the Linux build for a GitHub release:
#   Tools/package_release.sh 0.1.0   ->  Builds/PackTheTrunk-v0.1.0-linux-x86_64.zip
# Build first with Tools/unity.sh build-linux.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${1:?usage: $0 <version>}"
BUILD="$ROOT/Builds/Linux"
[ -x "$BUILD/PackTheTrunk.x86_64" ] || { echo "No build yet. Run Tools/unity.sh build-linux first." >&2; exit 1; }
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
DIR="$STAGE/PackTheTrunk"
mkdir -p "$DIR"
# Everything except Unity's "_BackUpThisFolder_ButDontShipItWithYourGame" debug symbols.
cp -a "$BUILD"/PackTheTrunk.x86_64 "$BUILD"/UnityPlayer.so "$BUILD"/PackTheTrunk_Data "$DIR"/
cp -a "$BUILD"/*.so* "$DIR"/ 2>/dev/null || true
cp -a "$ROOT/Tools/release/PackTheTrunk.sh" "$DIR"/
cat > "$DIR/README.txt" <<TXT
Pack The Trunk v$VERSION (Linux x86_64)
https://github.com/nearbycoder/PackTheTrunk

Run ./PackTheTrunk.sh (or ./PackTheTrunk.x86_64).
Mouse and keyboard. Controls are listed in Settings > Controls.
Saves live in ~/.config/unity3d/Nearby Games/Pack The Trunk/.
TXT
OUT="$ROOT/Builds/PackTheTrunk-v$VERSION-linux-x86_64.zip"
rm -f "$OUT"
# Python's zipfile keeps the executable bits, and is on every machine that runs the other tools.
(cd "$STAGE" && python3 - "$OUT" <<'PY'
import os, sys, zipfile
with zipfile.ZipFile(sys.argv[1], "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for base, dirs, files in os.walk("PackTheTrunk"):
        dirs.sort()
        for name in sorted(files):
            z.write(os.path.join(base, name))
PY
)
ls -la "$OUT"
