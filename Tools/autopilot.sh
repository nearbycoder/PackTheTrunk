#!/usr/bin/env bash
# Self-test: the built game plays itself (real mouse/keyboard events + solver solutions),
# saves screenshots to ${1:-/tmp/ptt-autopilot} and prints PASS/FAIL lines.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:-/tmp/ptt-autopilot}"
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
# The run plays on a sandboxed save (Prefs.cs); prove the player's own save is left alone.
SAVE="$HOME/.config/unity3d/Nearby Games/Pack The Trunk"
save_hash() { [ -d "$SAVE" ] && (cd "$SAVE" && find . -path ./Unity -prune -o -type f -print0 | sort -z | xargs -0 sha256sum) | sha256sum || echo none; }
BEFORE="$(save_hash)"
timeout 900 "$ROOT/Tools/play.sh" -logFile "$OUT/player.log" -pttAutopilot "$OUT" -pttSolutions "$OUT/solutions.txt" ${PTT_QUICK:+-pttQuick} > /dev/null 2>&1 || true
[ "$(save_hash)" = "$BEFORE" ] && echo "[AutoPilot] PASS the player's own save is untouched" >> "$OUT/player.log" \
  || echo "[AutoPilot] FAIL the player's own save changed during the run" >> "$OUT/player.log"
grep -E "\[AutoPilot\] (PASS|FAIL|done)|Exception" "$OUT/player.log"
! grep -qE "\[AutoPilot\] FAIL|Exception" "$OUT/player.log" && grep -q "\[AutoPilot\] done" "$OUT/player.log"
