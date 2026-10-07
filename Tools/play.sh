#!/usr/bin/env bash
# Runs the built Linux player. On this machine the X11/XWayland path hangs at startup,
# so force Unity's native Wayland backend when a Wayland session is available.
#
# Automated runs (-pttAutopilot, -pttBench, -pttShowcase) play inside a private, headless KWin
# (kwin_wayland --virtual), so no test window ever opens on the desktop. PTT_NESTED=0 shows a
# normal window instead; without kwin_wayland it falls back to that. PTT_PIDFILE, if set, gets the
# player's own PID (the nested compositor is its parent), for a caller that needs to kill the game.
set -euo pipefail
GAME="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/Builds/Linux/PackTheTrunk.x86_64"
[ -x "$GAME" ] || { echo "No build yet. Run Tools/unity.sh build-linux first." >&2; exit 1; }
args=(-screen-fullscreen 0)
# Default window size unless the caller picked one (the recorder asks for 1080p).
[[ " $* " == *" -screen-width "* ]] || args+=(-screen-width 1600 -screen-height 900)

nested=0
if [ "${PTT_NESTED:-1}" != 0 ] && command -v kwin_wayland > /dev/null; then
  for a in "$@"; do
    case "$a" in -pttAutopilot|-pttBench|-pttShowcase) nested=1 ;; esac
  done
fi

if [ "$nested" = 0 ]; then
  [ -n "${WAYLAND_DISPLAY:-}" ] && args+=(-force-wayland)
  [ -n "${PTT_PIDFILE:-}" ] && echo $$ > "$PTT_PIDFILE"
  exec "$GAME" "${args[@]}" "$@"
fi

# The virtual screen has to hold the window: at least 2560x1600, or the window plus a margin.
w=1600; h=900; prev=""
for a in "${args[@]}" "$@"; do
  [ "$prev" = -screen-width ] && w="$a"
  [ "$prev" = -screen-height ] && h="$a"
  prev="$a"
done
(( w + 64 > 2560 )) && sw=$(( w + 64 )) || sw=2560
(( h + 64 > 1600 )) && sh=$(( h + 64 )) || sh=1600

# KWin starts the session from a single path, so write one that execs the player with every
# argument quoted as its own word (it deletes itself first). It lives in the gitignored Builds/.
dir="$(dirname "$GAME")/../.nested"
mkdir -p "$dir"
session="$(cd "$dir" && pwd)/session-$$.sh"
{
  echo '#!/usr/bin/env bash'
  printf 'rm -f %q\n' "$session"
  echo 'unset DISPLAY'
  [ -n "${PTT_PIDFILE:-}" ] && printf 'echo $$ > %q\n' "$PTT_PIDFILE"
  printf 'exec %q' "$GAME"
  printf ' %q' "${args[@]}" -force-wayland "$@"
  echo
} > "$session"
chmod +x "$session"
exec env -u DISPLAY -u WAYLAND_DISPLAY kwin_wayland --virtual --no-lockscreen --no-global-shortcuts --no-kactivities \
  --socket "ptt-nested-$$" --width "$sw" --height "$sh" --exit-with-session "$session"
