#!/usr/bin/env bash
# Captures the raw footage for the trailer: the built game plays a scripted clip per feature
# (Showcase.Trailer.cs) at a locked 30 fps with lockstep audio, and writes
#   <out>/frame_NNNNN.jpg   video frames (1920x1080)
#   <out>/audio.wav         game sound effects and ambience (the soundtrack is muted)
#   <out>/beats.tsv         clip name, first frame, end frame
#   <out>/stills/*.png      clean screenshots (no cursor) for the README
# Then run Tools/make_trailer.py to cut it together.
#   Tools/record_trailer.sh [out-dir]       (default Recordings/trailer-capture, about 3 GB)
#   PTT_TRAILER_ONLY=fragile,clown Tools/record_trailer.sh Recordings/trailer-pickups
#       re-shoots just those sections (intro, fragile, clown, arrivals, album, speed, heights, coldopen);
#       pass both folders to make_trailer.py and the newer takes win.
#   PTT_STILLS_ONLY=1 Tools/record_trailer.sh Recordings/stills
#       plays the same script but saves only the stills (no frames or audio): refreshes the README
#       screenshots in a few minutes; then make_trailer.py <all folders> --only stills.
# The capture plays on a sandboxed fresh save (Prefs.cs) and never touches yours; the script hashes
# your save folder before and after to prove it (it never writes there itself).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$(mkdir -p "${1:-$ROOT/Recordings/trailer-capture}" && cd "${1:-$ROOT/Recordings/trailer-capture}" && pwd)"
SAVE="$HOME/.config/unity3d/Nearby Games/Pack The Trunk"
save_hash() { [ -d "$SAVE" ] && (cd "$SAVE" && find . -path ./Unity -prune -o -type f -print0 | sort -z | xargs -0 sha256sum) | sha256sum || echo none; }
BEFORE="$(save_hash)"
# Scratch (the solver's solutions) lives next to the capture, not on the shared /tmp.
WORK="$OUT/work"
mkdir -p "$WORK"

python3 "$ROOT/Tools/solve_levels.py" --dump "$WORK/solutions.json" > /dev/null
python3 - "$WORK" <<'PY'
import json, sys
work = sys.argv[1]
with open(f"{work}/solutions.txt", "w") as f:
    for level, placements in json.load(open(f"{work}/solutions.json")).items():
        for p in placements:
            f.write(f"{level}\t{p['id']}\t" + ";".join(",".join(map(str, c)) for c in p["cells"]) + "\n")
PY

# The player has crashed (rarely) mid-recording; keep that log and try once more.
for attempt in 1 2; do
  rm -rf "$OUT"/frame_*.jpg "$OUT/stills" "$OUT/audio.wav" "$OUT/beats.tsv"
  timeout 3600 "$ROOT/Tools/play.sh" -screen-width 1920 -screen-height 1080 -logFile "$OUT/player.log" \
    -pttShowcase "$OUT" -pttTrailer ${PTT_TRAILER_ONLY:+-pttTrailerOnly "$PTT_TRAILER_ONLY"} ${PTT_STILLS_ONLY:+-pttStillsOnly} \
    -pttSolutions "$WORK/solutions.txt" > /dev/null 2>&1 || true
  grep -q "\[Showcase\] done" "$OUT/player.log" && break
  cp "$OUT/player.log" "$OUT/player-attempt-$attempt.log" 2>/dev/null || true
  echo "trailer capture attempt $attempt did not finish (log: $OUT/player-attempt-$attempt.log)" >&2
done
grep -E "\[Showcase\]|Exception" "$OUT/player.log" || true
[ "$(save_hash)" = "$BEFORE" ] && echo "your save is untouched" || echo "WARNING: your save folder changed during the capture" >&2
grep -q "\[Showcase\] done" "$OUT/player.log"
echo "$OUT"
