#!/usr/bin/env bash
# Crash test for "your trunk waits for you": the built player half-packs a trip on a sandboxed save
# file, gets killed with SIGKILL (as a crash would), then a second player on the same save file must
# put the same trunk back. Output in ${1:-Recordings/resume-test}; prints PASS/FAIL lines.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$(realpath -m "${1:-$ROOT/Recordings/resume-test}")"
rm -rf "$OUT"; mkdir -p "$OUT"
python3 "$ROOT/Tools/solve_levels.py" --dump "$OUT/solutions.json" > /dev/null
python3 - "$OUT" <<'PY'
import json, sys
out = sys.argv[1]
with open(f"{out}/solutions.txt", "w") as f:
    for level, placements in json.load(open(f"{out}/solutions.json")).items():
        for p in placements:
            f.write(f"{level}\t{p['id']}\t" + ";".join(",".join(map(str, c)) for c in p["cells"]) + "\n")
PY
SAVE="$HOME/.config/unity3d/Nearby Games/Pack The Trunk"
save_hash() { [ -d "$SAVE" ] && (cd "$SAVE" && find . -path ./Unity -prune -o -type f -print0 | sort -z | xargs -0 sha256sum) | sha256sum || echo none; }
BEFORE="$(save_hash)"
COMMON=(-pttAutopilot "$OUT" -pttSolutions "$OUT/solutions.txt" -pttPrefsFile "$OUT/prefs.txt")

# play.sh may run the player inside a nested compositor, so ask it for the player's own PID: the
# crash has to hit the game, not the compositor around it.
PTT_PIDFILE="$OUT/player.pid" "$ROOT/Tools/play.sh" -logFile "$OUT/pack.log" "${COMMON[@]}" -pttResumeTest pack > /dev/null 2>&1 &
PID=$!
for _ in $(seq 1 240); do
  grep -q "ready to be killed" "$OUT/pack.log" 2>/dev/null && break
  kill -0 "$PID" 2>/dev/null || break
  sleep 0.5
done
grep -q "ready to be killed" "$OUT/pack.log" 2>/dev/null || { echo "[AutoPilot] FAIL resume-crash: the first run never finished packing"; kill -9 "$(cat "$OUT/player.pid" 2>/dev/null)" "$PID" 2>/dev/null || true; exit 1; }
GAME_PID="$(cat "$OUT/player.pid")"
kill -9 "$GAME_PID"
wait "$PID" 2>/dev/null || true
grep "resume-crash" "$OUT/pack.log"
echo "saved trunk: $(grep -a 'ptt.trunk' "$OUT/prefs.txt" || echo none)"

timeout 300 "$ROOT/Tools/play.sh" -logFile "$OUT/check.log" "${COMMON[@]}" -pttResumeTest check > /dev/null 2>&1 || true
[ "$(save_hash)" = "$BEFORE" ] && echo "[AutoPilot] PASS the player's own save is untouched" >> "$OUT/check.log" \
  || echo "[AutoPilot] FAIL the player's own save changed during the run" >> "$OUT/check.log"
grep -E "\[AutoPilot\] (PASS|FAIL|done)|\[Resume\]|Exception" "$OUT/check.log"
! grep -qE "\[AutoPilot\] FAIL|Exception" "$OUT/check.log" && grep -q "\[AutoPilot\] done" "$OUT/check.log"
