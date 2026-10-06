#!/usr/bin/env bash
# Records a gameplay video with sound: the built game plays the prologue, two later trips and the finale with a visible cursor, key
# badges and captions, rendering lossless 30 fps frames plus lockstep audio that ffmpeg muxes into
# Recordings/<name>.mp4.
#   Tools/record.sh [name]
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NAME="${1:-pack-the-trunk-gameplay}"
mkdir -p "$ROOT/Recordings"
# Lossless 1080p frames run to gigabytes: keep them on disk under Recordings/, not the /tmp RAM disk.
WORK="$(mktemp -d "$ROOT/Recordings/.record-work.XXXX")"

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
  rm -rf "$WORK/frames"
  timeout 2400 "$ROOT/Tools/play.sh" -screen-width 1920 -screen-height 1080 -logFile "$WORK/player.log" \
    -pttShowcase "$WORK/frames" -pttSolutions "$WORK/solutions.txt" > /dev/null 2>&1 || true
  cp "$WORK/player.log" "$ROOT/Recordings/record-player-$attempt.log" 2>/dev/null || true
  grep -q "\[Showcase\] done" "$WORK/player.log" && break
  echo "recording attempt $attempt did not finish (log kept in Recordings/record-player-$attempt.log)" >&2
done
grep -E "\[Showcase\]|Exception" "$WORK/player.log" || true

AUDIO=()
# Raw game mix, before loudness normalisation: the peak should sit below 0 dBFS (no clipping).
[ -s "$WORK/frames/audio.wav" ] && ffmpeg -hide_banner -i "$WORK/frames/audio.wav" -af astats=measure_perchannel=none -f null - 2>&1 \
  | grep -E "Peak level dB|RMS level dB" | sed 's/^.*\] /raw mix: /' || true
[ -s "$WORK/frames/audio.wav" ] && AUDIO=(-i "$WORK/frames/audio.wav" -af loudnorm=I=-16:TP=-1.5:LRA=11,alimiter=limit=0.8:level=disabled -c:a aac -b:a 192k -shortest)
ffmpeg -y -loglevel error -framerate 30 -i "$WORK/frames/frame_%05d.png" "${AUDIO[@]}" \
  -c:v libx264 -preset slow -crf 16 -tune animation -pix_fmt yuv420p -movflags +faststart "$ROOT/Recordings/$NAME.mp4"
echo "$ROOT/Recordings/$NAME.mp4"
rm -rf "$WORK"
