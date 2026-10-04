#!/usr/bin/env bash
# Rebuilds Assets/Resources/Audio from the CC0 sources:
#   - Kenney's Interface Sounds, Impact Sounds, RPG Audio and UI Audio packs (kenney.nl)
#   - "Park ambiences" by Thimras (opengameart.org/content/park-ambiences)
#   - stingers/horn/engine/whooshes rendered by render_stingers.py (needs numpy + scipy)
# Each clip is trimmed of trailing silence and peak-normalised to -3 dBFS; ambience loops are
# cut to 90 s / 60 s with an equal-power crossfade so they loop seamlessly.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
K="${KENNEY:-/tmp/kenney}"; AMB="${AMB:-/tmp/amb}"; OUT="${PTT_SFX_OUT:-/tmp/sfx/out}"
DEST="$ROOT/Assets/Resources/Audio/Sfx"; mkdir -p "$DEST" "$ROOT/Assets/Resources/Audio/Ambience"
python3 "$ROOT/Tools/audio/render_stingers.py"

pick() { # name source
  ffmpeg -v error -y -i "$2" -af "areverse,silenceremove=start_periods=1:start_threshold=-62dB,areverse" -ar 44100 /tmp/ptt_sfx_tmp.wav
  peak=$(ffmpeg -v info -i /tmp/ptt_sfx_tmp.wav -af volumedetect -f null - 2>&1 | grep max_volume | awk '{print $5}')
  ffmpeg -v error -y -i /tmp/ptt_sfx_tmp.wav -af "volume=$(python3 -c "print(-3.0 - ($peak))")dB" -c:a libvorbis -q:a 7 "$DEST/$1.ogg"
}
for f in "$OUT"/*.wav; do pick "$(basename "$f" .wav)" "$f"; done
I=$K/kenney_interface-sounds/Audio; U=$K/kenney_ui-audio/Audio; R=$K/kenney_rpg-audio/Audio; M=$K/kenney_impact-sounds/Audio
pick ui_hover $U/rollover2.ogg; pick ui_click $I/click_001.ogg; pick ui_back $I/back_002.ogg; pick ui_toggle $I/toggle_002.ogg
pick ui_tick $I/tick_002.ogg; pick ui_open $I/maximize_002.ogg; pick ui_close $I/minimize_002.ogg; pick ui_select $I/select_003.ogg
pick page_1 $R/bookFlip1.ogg; pick page_2 $R/bookFlip2.ogg; pick page_3 $R/bookFlip3.ogg
pick pencil_1 $I/scratch_004.ogg; pick pencil_2 $I/scratch_005.ogg; pick check $I/scratch_001.ogg
pick message $I/drop_002.ogg; pick bubble $I/pluck_002.ogg
for i in 1 2 3 4; do pick cloth_$i $R/cloth$i.ogg; done
pick leather_1 $R/handleSmallLeather.ogg; pick leather_2 $R/handleSmallLeather2.ogg
for i in 0 1 2 3 4; do
  pick land_soft_$i $M/impactSoft_medium_00$i.ogg; pick land_heavy_$i $M/impactSoft_heavy_00$i.ogg
  pick land_wood_$i $M/impactWood_light_00$i.ogg; pick land_glass_$i $M/impactGlass_light_00$i.ogg
  pick land_metal_$i $M/impactMetal_light_00$i.ogg
done
pick slam_1 $R/doorClose_1.ogg; pick slam_2 $R/doorClose_4.ogg; pick latch $R/metalLatch.ogg; pick creak $R/creak3.ogg
pick stamp $M/impactPunch_medium_000.ogg

loop() { # in start length out gain
  ffmpeg -v error -y -ss "$2" -t $(($3 + 4)) -i "$1" -filter_complex \
    "[0:a]asplit[x][y];[x]atrim=0:$3,asetpts=PTS-STARTPTS,afade=t=in:st=0:d=4:curve=qsin[a];[y]atrim=$3:$(($3 + 4)),asetpts=PTS-STARTPTS,afade=t=out:st=0:d=4:curve=qsin[b];[a][b]amix=inputs=2:duration=first:normalize=0,highpass=f=70,volume=$5dB" \
    -c:a libvorbis -q:a 6 "$4"
}
loop "$AMB/park_ambience_birds.wav" 30 90 "$ROOT/Assets/Resources/Audio/Ambience/birds.ogg" 13
loop "$AMB/park_ambience_wind.wav" 20 60 "$ROOT/Assets/Resources/Audio/Ambience/wind.ogg" 11
