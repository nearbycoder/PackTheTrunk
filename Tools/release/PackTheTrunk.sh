#!/usr/bin/env bash
# Launches Pack The Trunk. On Wayland it uses Unity's native Wayland backend: the player has hung
# at startup through XWayland. Extra arguments are passed through (e.g. -force-vulkan).
cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")"
args=()
[ -n "${WAYLAND_DISPLAY:-}" ] && args+=(-force-wayland)
exec ./PackTheTrunk.x86_64 "${args[@]}" "$@"
