#!/usr/bin/env bash
# Runs the built Linux player. On this machine the X11/XWayland path hangs at startup,
# so force Unity's native Wayland backend when a Wayland session is available.
set -euo pipefail
GAME="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/Builds/Linux/PackTheTrunk.x86_64"
[ -x "$GAME" ] || { echo "No build yet. Run Tools/unity.sh build-linux first." >&2; exit 1; }
args=(-screen-fullscreen 0)
# Default window size unless the caller picked one (the recorder asks for 1080p).
[[ " $* " == *" -screen-width "* ]] || args+=(-screen-width 1600 -screen-height 900)
[ -n "${WAYLAND_DISPLAY:-}" ] && args+=(-force-wayland)
exec "$GAME" "${args[@]}" "$@"
