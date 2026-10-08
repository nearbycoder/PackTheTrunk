# Pack The Trunk: improvement plan

Phase 1 of the post-launch pass (branch `improvements`, 2026-10-06). This file records the
baseline, a ranked list of what would most improve the game for a real player, and the proposed
scope for the next round. Nothing here is implemented yet.

## Baseline (v0.1.0, commit 2ef8b74)

| Check | Result |
| --- | --- |
| `Tools/unity.sh build-linux` | **Pass.** 147 MB, 0 errors, 0 compiler warnings. |
| `python3 Tools/solve_levels.py` | **Pass.** All 33 levels pack 100% (14 s total; the hardest is `wedding` at 829k nodes / 7.7 s). |
| `Tools/autopilot.sh` (full, 33 trips) | **Pass.** 79 PASS, 0 FAIL, no exceptions, `[AutoPilot] done`, 70+ screenshots. |
| `[Audio]` line | Mix peaks at −1.5 dBFS after the limiter (2.3 dBFS in), at most 4.1 dB of gain reduction. |
| `[Perf]` lines | **Not trustworthy this run.** Load average was 26–38 on 32 cores (other games' autopilots and builds were running at the same time). Packing averaged 15.6 ms with p95 34 ms and 294 frames over 33 ms, against the README's "2–3 ms" figure from a quiet machine. This has to be measured again with `-pttBench` when the machine is quiet before claiming any regression. |
| Level build | 23–167 ms per trip (`[Perf] built …`), almost all of it the pile/blanket layout. It happens behind the paper-wipe transition, so it's hidden for now. |
| macOS test build (`-buildTarget OSXUniversal -buildOSXUniversalPlayer`, Mono) | **Builds from Linux.** 3 min 24 s, 122 MB `.app`, 0 compiler errors. The executable is **arm64-only** (Apple Silicon), so Intel Macs need the architecture set to x64+ARM64. Unity ad-hoc signs it; it isn't notarized. **Not run on a Mac**, because there isn't one here. Output went to `Builds/macOS-test`, which is gitignored. |
| Unit tests | There aren't any. The autopilot and the solver are the test suite. |

Side effects to know about: the autopilot writes stars and album photos into the real save, and
`-pttShowcase` wipes it. I backed up `~/.config/unity3d/Nearby Games/Pack The Trunk` before the
run and restored it afterwards.

## What I looked at

The code in `Assets/Scripts` (`GameController`, `TrunkGrid`, `CameraRig`, `GameUI*`,
`GameSettings`, the audio classes), the level data (fill ratios, item counts and fragile counts
per trip, computed from `PackTheTrunkData.json`), the autopilot screenshots of every trip, and the
README screenshots.

Findings that drive the ranking:

- **A new player never sees how to play.** The only how-to panel (CLICK / R T F / WHEEL) is on the
  Trip Map's story note (`GameUI.BuildMenu`). A new player goes Title → *Begin the story* →
  story → packing and skips the map entirely. Trip 1, the wagon, is 1 cell tall, so it never
  asks for tip (T), roll (F) or a shelf choice (W/S). Trip 2 introduces height, trip 3 introduces
  fragile cakes and eggs, and trip 4 is 19 items in an SUV with wheel wells. All of that is
  explained only by the key-hint bar and the red-ghost error toasts.
- **There's no help when you're stuck.** Getting 3 stars means filling 78–100% of the trunk
  (the median is about 91%). Essentials fill only 40–75%, so one star is easy and all the
  challenge is in the extras. The solver already has a 100% solution for every trip, but the
  player can't use it.
- **Input is hard-coded to the mouse and keyboard.** `Keyboard.current` and `Mouse.current` are
  read directly in 7 files, and `GameUI.Update` clears the EventSystem selection every frame.
  That rules out keyboard and gamepad menu navigation. No gamepad was connected to this machine
  (`/dev/input/js*` is empty).
- **Placement feedback relies on colour.** The ghost is green (fits) or red (doesn't fit)
  (`GhostOk` / `GhostBad`). The only other cue is the held item floating 0.15 units higher. That
  is the classic red/green pair that deuteranopia and protanopia make hard to tell apart.
  "Fragile" appears only in the packing list and on the held-item card, never on the 3D object.
  On a full blanket you can't tell which pieces are fragile without reading the list.
- **Space closes the trunk at once** as soon as the essentials are in, even with extras still on
  the blanket and room left. Space is also the key that hurries the story texts along, so
  pressing it from habit can end a trip at one star. "Try again" makes this recoverable, but it
  stings.
- **The album keeps the latest photo, not the best one.** `TakeTrunkPhoto` overwrites
  `album/<id>.png` on every close, while the stars keep the maximum (`Mathf.Max`). Replaying a
  3-star trip and closing it early leaves a half-empty trunk in the album next to three stars.
- **The postcard says "Not even the gnome."** for every perfect trip, including 1998's wagon, where
  there's no gnome.
- **Platforms:** only the Linux build exists. The editor has Mac and WebGL support installed, but
  not Windows.

## Ranked improvements

Impact means impact for a real player. Effort: S is under half a day, M is about a day, L is
several days.

| # | Improvement | Impact | Effort | Risk | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | **First-trip onboarding:** contextual "Grandpa's tips" the first time each mechanic matters | High | S–M | Low | Taught when needed: click to pick up; green/red ghost; R turns the item; T/F the first time something is too tall or too long; W/S the first time a column has two shelves; fragile the first time a fragile item is held; Space once the essentials are in; Z to undo. Each tip shows once and is stored in prefs. There's a toggle in Gameplay settings. |
| 2 | **"Ask Grandpa" hint:** shows where one item goes, using the solver's solution | High | M | Low–Med | `solve_levels.py --dump` generates `Resources/Solutions.json` (the autopilot already parses this format). The hint shows a ghost for one unpacked item whose solution cells are free and supported right now. If the player's packing has drifted from every solution, it names the packed item to take out instead. Opt-in by key or button; never automatic. |
| 3 | **Gamepad / Steam Deck support** | High (a Linux release's natural audience is the Deck) | M–L | Med | The left stick drives a virtual cursor that feeds the existing raycast and uGUI paths. A = pick up / drop, B = put back / back, X/Y = turn / tip, LB = roll, triggers or D-pad = shelf, the right stick orbits, Start pauses, View undoes. Key hints switch to controller glyphs. This needs a small input layer instead of direct `Keyboard.current` reads. There's no controller here, so it can only be verified with a simulated gamepad. |
| 4 | **Colour-blind-safe placement and fragile markers** | Med–High (accessibility) | S | Low | A "Placement colours" setting (green/red or blue/orange). Whatever colours are chosen, an invalid ghost also gets diagonal hatching from `Ghost.shader`. A small stamp-style "fragile" glass icon floats over fragile items on the blanket and when hovered. |
| 5 | **macOS build and packaging** | Med (reach; the blog lists macOS) | S–M | Med | The test build already succeeds (see Baseline). Add `build-mac` to `unity.sh` and `BuildScript`, set the architecture to universal (x64+ARM64), and add a macOS zip in `package_release.sh` that keeps the executable bit. The app would be unsigned and un-notarized, so players have to right-click → Open or run `xattr -dr com.apple.quarantine`. It can't be run on a Mac here. |
| 6 | **Small fixes that protect the player:** no accidental close, best photo kept, postcard copy | Med | S | Low | If extras remain and they'd still fit, the first Space shows "Space again to close, 3 extras still on the blanket" and a second press within 2 s closes (the Close button stays one click). Only replace the album photo when the new stars ≥ the saved stars. Only mention the gnome when the trip had one. |
| 7 | **Save isolation for test and capture modes** | Low for players, Med for the owner | S | Low | When `GameController.Automated` is set, use a `ptt.capture.` prefs prefix and an `album-capture` folder instead of wiping or writing the real save. This removes the "capture modes wipe the save" known issue, and the autopilot no longer fills a real player's map with 3-star trips. |
| 8 | **WebGL browser build** | High reach (itch.io), if it works | M–L | Med–High | It suits the game: mouse-driven, cozy, small scenes. But several systems need work: `MasterBus` uses `OnAudioFilterRead` and the muffles use `AudioLowPassFilter` (neither runs in WebGL); album PNGs are encoded with `Task.Run` (no threads by default); persistence needs IndexedDB sync; Quit and the window/resolution settings make no sense in a browser; SSAO + MSAA cost on an iGPU in a browser is unknown; and download size is driven by 9 music tracks. It needs its own round, plus the owner's call on hosting (itch.io or GitHub Pages). |
| 9 | **Windows build** | Med–High reach | S once unblocked | Low–Med | **Blocked:** Windows Build Support (Mono) isn't installed. The owner needs to add the module in Unity Hub. After that it's the same work as #5. |
| 10 | **See into deep trunks:** fade or cut away items above the aimed layer, and fade the lid | Med | M | Med | It's hard to read the lower layers in the minivan, moving truck and SUV trips (see the `07-everyone-everything` screenshot). |
| 11 | **Remappable controls** | Med (accessibility) | L now, M after #3 | Med | It becomes cheap once #3 adds an input layer. |
| 12 | **Post-game replayability** (per-trip challenges such as "no undo" or "pack it in N pickups", or a solver-generated Garage Sale mode) | Med | L | Med | Once a trip has 3 stars, there's no reason to replay it. |
| 13 | **Hide the level-build hitch** (spread the blanket build over frames) | Low | S | Low | It's currently hidden behind the transition. |
| 14 | **Find the showcase recorder's segfault** | Low (dev only) | Unknown | — | It happened once, and the recorder now retries. There's no reproduction. |

## Proposed scope for this round

I'd implement items 1, 2, 4, 6 and 7 (all low-risk and verifiable here), and 3 if the owner
wants controller support now. Item 5 can be done in parallel because it only adds a build target.

### A. First-trip onboarding (item 1)

- **Acceptance:** with a fresh save, trip 1 shows a tip to click an item, and holding an item
  shows the ghost/turn tip. Over trips 1–4, each of the tips listed in item 1 appears exactly once,
  at the moment it applies, and never covers the packing list or the held-item card. The tips
  don't appear again after a restart or on later trips. A "Packing tips" toggle and "Show tips
  again" in Gameplay settings control them. The Trip Map's how-to note stays.
- **Verify:** the autopilot gets a fresh-save pass that asserts each tip's first trigger (as a log
  line and UI visibility) and that none repeat. Screenshots of each tip at 1600×900 and 1920×1080.
  The full autopilot still shows 0 FAIL.

### B. "Ask Grandpa" hint (item 2)

- **Acceptance:** pressing `H` (or a HINT button by Undo) shows a pulsing ghost and the item's
  outline, for an item and position that is valid right now and comes from a 100% solution. If no
  solution position is free, the hint says which packed item to take out. It never auto-places.
  `Solutions.json` is generated by `solve_levels.py` and checked against the level data at load
  (a mismatch is logged, and the hint button is hidden for that trip).
- **Verify:** an autopilot pass on every trip that packs the whole trunk using only hints (press
  H, place what it suggests, repeat) must reach 100%. A second pass that deliberately misplaces an
  item first must get the "take out X" message. The solver still passes.

### C. Colour-blind-safe placement and fragile markers (item 4)

- **Acceptance:** an invalid placement can be told apart from a valid one in grayscale. A
  screenshot of both, desaturated with ffmpeg (`hue=s=0`), must be distinguishable. The blue/orange
  palette is selectable and applies live. Every fragile item on the blanket has a world-space
  marker that hides once it's packed. Ghost cost per frame doesn't change measurably.
- **Verify:** autopilot screenshots (valid / invalid / fragile pile) in both palettes, the
  grayscale comparison, and the full autopilot.

### D. Small fixes that protect the player (item 6)

- **Acceptance:** with extras left that still fit, a single Space doesn't close the trunk and shows
  the prompt; a double press does. With no extras left, or none that fit, a single Space closes
  as it does now. Replaying a 3-star trip for 1 star keeps the 3-star photo. The wagon's
  postcard doesn't mention a gnome.
- **Verify:** new autopilot checks for all three, with the album PNG's modification time and hash
  checked before and after the replay.

### E. Save isolation for automated modes (item 7)

- **Acceptance:** after a full autopilot run, and after `record.sh`, the real prefs file and album
  folder are byte-identical to before. `record.sh` and `record_trailer.sh` still start from a fresh
  (capture) save. The README's "Capture modes wipe the save" known issue is removed.
- **Verify:** `sha256sum` of the save folder before and after `PTT_QUICK=1 Tools/autopilot.sh`, and
  a short `record.sh` run (only if the owner is fine with a recording run on the shared machine).

### F. Optional this round: gamepad (item 3) and macOS build (item 5)

- **Gamepad acceptance:** the whole game can be played on a controller: title, menus,
  settings, story, packing every action, pause, postcard and album. The hints show controller
  glyphs when the gamepad was the last device used. Mouse and keyboard behave exactly as before.
- **Gamepad verification:** an autopilot pass that drives a simulated `Gamepad` device
  (`InputSystem.AddDevice<Gamepad>()`) through one trip and the menus, plus the existing
  keyboard/mouse autopilot unchanged. **I can't test it on real hardware or a Steam Deck here.**
  The README would say so.
- **macOS acceptance:** `Tools/unity.sh build-mac` produces an `.app`, `package_release.sh` makes
  a zip that keeps the executable bit, and the README documents the Gatekeeper step and that the
  build is untested on real Macs until someone runs it.

Every item ends with `build-linux`, the solver, the full autopilot (0 FAIL), a `-pttBench` perf run
on a quiet machine, and README updates where behaviour or known issues change.

## Decisions for the owner

1. **Windows:** install *Windows Build Support (Mono)* for 6000.6.2f1 in Unity Hub if you want a
   Windows build. Nothing else blocks it.
2. **macOS:** are you OK publishing an unsigned, un-notarized build that hasn't been run on a
   real Mac? Signing needs an Apple Developer account.
3. **Gamepad:** do it this round (larger; no hardware to test on), or next round with remapping?
4. **WebGL:** do you want a browser build at all, and where would it be hosted? It needs an audio
   and persistence pass first.
5. **Hints:** are you happy for the game to ship its own solutions? They'd be readable in the
   build's data.

## Round 1 results (2026-10-06)

Everything in the proposed scope landed, plus the macOS target and the gamepad stretch. Each item
was verified with `Tools/unity.sh build-linux` (0 errors) and the autopilot. The final full run
(`Tools/autopilot.sh`, all 33 trips) had **150 PASS, 0 FAIL**, and the real save was
byte-identical afterwards. `solve_levels.py` still proves all 33 levels. Screenshots are in
`docs/media/improvements/`.

| Item | Commit | Verified by |
| --- | --- | --- |
| E. Sandboxed save for test/capture modes | `e956cb4` | `autopilot.sh` hashes the save folder before and after (PASS). A short `-pttShowcase -pttTrailer` capture also left it untouched and started fresh. |
| D. Space-close guard, best album photo, gnome line | `02b2008` | Autopilot replays the wagon with only the essentials: one Space doesn't close, a second does, the 3-star photo's SHA-256 is unchanged after a 1-star close, and the wagon postcard has no gnome. `d-space-close-prompt.jpg` |
| C. Colour-blind-safe ghost + fragile stamps | `79afe2d` | Autopilot checks both ghost states. The grayscale comparison of both palettes shows the stripes clearly. Fragile stamps were checked on Grandma's Big Move (all 7 shown). `c-*.jpg` |
| A. Grandpa's tips | `d60b360` | `TipChecks` triggers all seven tips the way a player would, checks each one retires, and checks none repeats (each shown exactly once). Checked at 16:9 (1600×900) and 4:3 (1440×1080). `a-*.jpg` |
| B. Ask Grandpa hint | `eb8a257` | Following only hints packs **all 33 trips to 100%**. A deliberately wrong start gets a "move it" hint and still reaches 100%. A real H press shows the ghost, and the hinted item picks up pre-turned. `b-*.jpg` |
| Autopilot harness fixes | `d1dc743` | Uncapped frame timing survives setting changes. Hint drops are paced so `[Audio]` isn't skewed. |
| F. macOS build target | `57f12a1` | `build-mac` makes a universal (x86_64 + arm64, checked with `file`) 159 MB `.app`, `com.nearbycoder.packthetrunk`. `package_release.sh <v> mac` keeps the executable bit. **Not run on a Mac.** `build-windows` fails with a clear message (module missing). |
| F. Gamepad (stretch) | `d971e66` | `GamepadChecks` drives a simulated `Gamepad` through 11 checks: pointing, pick up/drop, turn/tip/roll, undo, hint, pause, a menu click, B, and hand-back to the mouse. **Not tested on physical hardware or a Steam Deck.** `f-*.jpg` |

What I found along the way:

- **The "showcase segfault" is in Unity's Wayland backend.** One autopilot run crashed on the
  main thread inside `wl_display_dispatch_queue_pending`. That is Unity's native Wayland event
  dispatch, not game code. The log is kept at
  `Recordings/crashes/autopilot-wayland-segfault-0745.log` (gitignored). `autopilot.sh` now
  retries once, like the recorders.
- **Frame-time numbers on this machine need the window uncapped.** With V-Sync on, a covered
  window gets throttled to ~11 fps by the compositor. The final run, uncapped and at load
  average 25–44, packed at 6.0 ms average, p95 13 ms. That's not comparable to the README's
  quiet-machine figure; a `-pttBench` run on a quiet machine is still owed.
- **The `[Audio]` peak before the limiter changes with the test script.** Many scripted drops
  landing together push it up. The final run measured 1.4 dBFS in and −1.5 dBFS out, against
  2.3 / −1.5 at baseline.

Still open for later rounds: WebGL (#8), Windows (#9, needs the module), seeing into deep trunks
(#10), remappable controls (#11), post-game replayability (#12), and testing the gamepad on real
hardware or a Steam Deck. The README's trailer and screenshots are from v0.1.0, so they don't
show the HINT button, the tips or the fragile stamps.

## Round 2 scope (2026-10-06, branch `improvements-2`)

The round-1 measurement debt is paid first. A `-pttBench` run on a quiet machine (load average
0.8) at round-1 `main` averaged **1.7–1.9 ms on every screen**, with p99 at most 4.4 ms and no
frames over 33 ms (`Recordings/round2/bench-before.log`). So round 1 cost nothing measurable.

Round 2 takes the next player-facing items from the ranked list (#10, #11), one problem round 1
turned up (the HUD only really fits 16:9 at 100% interface size), and README screenshots that
show what the game looks like now. WebGL, Windows, signing and releases stay with the owner.

### A. See into the trunk (ranked #10)

When you tuck something into a gap with W/S or the wheel, the ghost can end up behind or under
packed things, and in the SUV, minivan and moving truck the lower layers are hard to read.

- **Acceptance:** while holding an item, any packed item that hides part of the placement ghost
  from the camera turns into a faint see-through silhouette (ghost material, no outline) and
  goes back to normal when it no longer hides the ghost or the item is dropped. Holding **Tab**
  (gamepad: click the left stick) makes every packed item see-through, and aiming then passes
  through them to the floor or walls, so any column can be targeted. W/S picks the height as
  usual. The key hints, Settings → Controls and the pause card list it. Nothing else changes when
  neither applies.
- **Verify:** an autopilot section builds a known case (an item tucked under an overhang on the
  Garage Sale pickup, as in the trailer). It checks that the occluder is faded only while it
  hides the ghost, that X-ray fades every packed item and lets the aim reach a floor cell under a
  stack, and that all materials are restored afterwards. Before/after screenshots. Quiet-machine
  `-pttBench` on the biggest trunk, plus a holding pass, with no measurable cost.

### B. Remappable keyboard controls (ranked #11)

- **Acceptance:**
  - Settings → Controls lists each keyboard action with its key. Clicking one waits for the next
    key press; Escape cancels.
  - Binding a key that's already used swaps the two actions. "Defaults" restores the original keys.
  - Bindings save, apply immediately, and drive everything that names a key: the key-hint bar,
    the pause card, Grandpa's tips and the "press SPACE again" prompt.
  - Mouse buttons, the wheel and the gamepad are unchanged.
- **Verify:** an autopilot section rebinds turn to `G`. It checks that `G` turns the held item and
  `R` no longer does, that the hint bar shows `G`, that binding `G` to undo swaps the two, and that
  Defaults restores everything (all in the sandboxed save). Screenshot of the Controls tab.

### C. The HUD fits every screen shape and interface size

Round 1 showed the key-hint strip running into the packing list at 4:3. The code suggests worse
at 120% interface size (the canvas becomes 1600×900 units) and on ultrawide screens (about 935
units tall at 21:9): the packing list's row height assumes a 1080-unit canvas.

- **Acceptance:** at 16:9, 16:10, 4:3 and 21:9, and at 80%, 100% and 120% interface size:
  - The trip tag, the top buttons, the packing list, the held-item card, the key-hint strip,
    Grandpa's tip and the toast never overlap.
  - The packing list fits every row of the biggest trip (25 things).
  - Nothing is cut off at the screen edge.
- **Verify:** an autopilot layout pass runs at each screen shape (separate player launches) and
  steps through the interface sizes. It shows every HUD piece at once on the biggest trip and
  asserts the screen rectangles pairwise don't intersect and stay on screen. Screenshots of the
  worst cases before and after.

### D. README screenshots that show the game as it is now

- **Acceptance:**
  - The README gallery is regenerated with the repo's own pipeline (`record_trailer.sh` sections
    plus `make_trailer.py --only stills`), so the HUD shows the HINT button.
  - New stills show Ask Grandpa's ghost, a Grandpa's tip, the fragile stamps with the striped
    "won't fit" ghost, and the X-ray view.
  - The trailer itself is not re-cut.
- **Verify:** look at each image. The README links resolve, and file sizes stay under the script's
  1.4 MB cap.

Each item ends with `build-linux`, the solver, a quick autopilot, and screenshots in
`docs/media/improvements/round2/`. The round ends with a full autopilot and a quiet-machine
benchmark if the machine is quiet.

## Round 2 results (2026-10-06)

All four scope items shipped. The final full autopilot (`Tools/autopilot.sh`, all 33 trips) had
**168 PASS, 0 FAIL**, the real save was untouched, and there was no player crash. The solver
proves all 33 levels. `build-linux` and `build-mac` both build with 0 errors. Screenshots are in
`docs/media/improvements/round2/`.

| Item | Commit | Verified by |
| --- | --- | --- |
| A. See into the trunk | `ea0294a` | `SeeThroughChecks` builds a covered gap on First Snow (skis over it, a thermos in front). The skis go see-through only while the ghost is in the gap. Holding Tab makes everything packed see-through and the aim reaches the gap (`(1, 1, 2)` → `(1, 0, 3)`). Everything is restored afterwards (6 checks). `a-see-through.jpg`, `a-xray-full.jpg` |
| B. Remappable keyboard controls | `f330696`, `a98af16` | `RebindChecks` drives the real Settings UI: G for turn, Escape cancels, G turns and R doesn't, the hints show G, binding G to undo swaps it with turn, and Defaults restores every key (9 checks). `b-controls-rebind.jpg` |
| C. HUD fits every screen | `1eb868b` | A layout pass at 16:9, 16:10, 4:3 and 21:9 × 80/100/120% interface size: 12/12 with no overlaps, nothing off screen, rows fit their text, and the held row in view. Quick autopilot 79/79 at 4:3 and at 21:9. `c-4x3-120-before-after.jpg`, `c-*.jpg` |
| D. README screenshots | `cd85cd6`, `f01efcb` | A stills-only capture (3 min, 19 MB). All 10 screenshots regenerated with the current HUD, plus 11 Ask Grandpa, 12 Grandpa's tip and 13 X-ray, each under 1.4 MB. Checked by eye. |
| /tmp care | `bc312df` | `record.sh` frames and logs, the `package_release.sh` staging folder and the autopilot's default output now live in gitignored repo folders. |

Things round 2 turned up along the way:

- **The layout pass found more than the plan expected.** The key strip, tip and toast collided
  with the packing list at 120%, 16:10 and 4:3. On wide screens the list rows were squashed below
  their text: the canvas used `MatchWidthOrHeight 0.5`, which makes a 21:9 canvas shorter than the
  design. The canvas now uses **Expand**. The first version of the check missed the squashed rows,
  because the 1°-tilted notepad makes screen-space boxes overlap. It now measures in the list's own
  space and compares text height with row height. At 120% the 25-item list now **scrolls** at a
  readable row size instead of shrinking.
- **Ask Grandpa's ghost could be invisible.** Framing the README still showed it hidden behind packed
  things or lost against yellow items. Whatever hides it now goes see-through (the same code as the
  placement ghost), and it is drawn in the HINT button's orange at higher opacity. This is verified
  only by the screenshot; there's no autopilot check for the hint occluder yet.
- **Remapping briefly added per-frame allocations.** The benchmark showed 5–8 GCs per packing
  phase instead of 1, because `Bindings.KeyFor` read PlayerPrefs with a new string on every call.
  It's fixed (`a98af16`), and the GCs are back to about 1 per phase.
- **Benchmark (`-pttBench`, 1600×900, High).** Before round 2 at load 0.8: 1.7–1.9 ms on every
  screen. After, at load 6–9: title, menus, the wagon and pause are unchanged (1.7–1.9 ms). The
  25-item minivan read 2.7 ms average, p99 4.8 ms, against 1.9 ms before. The machine wasn't quiet
  enough to tell whether that's load or the new per-frame work (see-through checks, the scrolling
  list's mask), so it needs a re-run on a quiet machine. There are still no frames over 33 ms.
- **Old scratch cleaned up.** 386 MB of this project's old scratch files were deleted from /tmp, and 69 MB more
  were moved to `Recordings/old-tmp-scratch/`.

Still open: Windows (needs the module), WebGL and hosting, signing and notarization (all owner
decisions); post-game replayability (#12); a hardware gamepad / Steam Deck test; re-cutting the
trailer, poster and teaser so they show the HINT button; and the quiet-machine perf re-check above.

## Round 3 scope (2026-10-06, branch `improvements-3`)

**Measurement debt first.** The machine was quiet when the round started (load average 0.5), so
`-pttBench` ran on the round-2 `main` build straight away (`Recordings/round3/bench-before.log`). The
25-item minivan averaged **1.8 ms** packing (p99 2.7 ms) and **1.8 ms** holding (p99 2.4 ms). Every
other screen was 1.6–1.8 ms, there were no frames over 33 ms, and there was about one GC per phase. Round 2's
2.7 ms reading came from machine load, not the see-through checks or the scrolling list. That item is
closed.

Round 3 is about what a player runs into mid-trip. A misclick can wipe a packed trunk. The star rules
are only written in the README. Hints make three stars easy to get without leaving any reason to replay.
It also pays the last verification debt from round 2.

### A. Restart you can take back

The HUD's **RESTART** sits right next to **UNDO**. Today one click rebuilds the trip, and a 20-item
trunk is gone with no undo. The pause menu's RESTART TRIP does the same.

- **Acceptance:** RESTART (HUD and pause) unpacks everything onto the blanket in place, as one undo
  step. Z / Backspace / View brings the whole trunk back exactly as it was. A toast says so. With nothing packed,
  RESTART only puts the held item back and says there's nothing to unpack. The postcard's TRY AGAIN still
  rebuilds the trip (the car has driven off). Grandpa's "RESTART starts fresh" hint stays true.
- **Verify:** an autopilot section packs half a trip, clicks the real RESTART button, checks that every
  item is on the blanket and the trunk grid is empty, presses Z, and checks that every item is back at
  the same cell in the same orientation. Then the same through the pause menu.

### B. A live star meter

The HUD shows "Essentials 3/5 · Extras 2/6", but not what closing now would earn or what the next
star needs ("at least half the extras").

- **Acceptance:** three small stars on the packing list show the stars you'd get by closing now
  (none until the essentials are in). When a star is earned it pops, and a toast says what the next one
  needs ("Two stars if you close now. Every extra makes three."). The postcard's stars always equal
  the meter at the moment of closing. The HUD layout pass stays clean at every screen shape and
  interface size.
- **Verify:** the autopilot checks the meter at 0 / 1 / 2 / 3 stars on a trip with an odd number of extras
  (half rounds up). It also checks that the postcard agrees on every trip it closes, and the layout pass
  (16:9, 16:10, 4:3, 21:9 × 80/100/120%) still reports no overlaps.

### C. Grandpa's seal: a reason to replay (ranked #12, small version)

Once Ask Grandpa exists, three stars stop meaning "I solved it". A trip packed to three stars
**without asking for a hint** earns Grandpa's seal.

- **Acceptance:** the seal is stamped on the postcard when it's earned and shown on that trip's card
  on the Trip Map. The map and album show how many trips are sealed. Asking for a hint that shows
  something (a ghost, or "X is in the way") rules out the seal for this attempt. A fresh attempt
  (RESTART, TRY AGAIN, or starting the trip again) clears that. Undoing a RESTART brings the hinted
  packing back, so it brings the "hinted" mark back too. Like stars, a seal is never taken away. Existing
  saves start with no seals. Nothing changes for players who don't care: no star is gated on it.
- **Verify:** the autopilot packs a trip to 3 stars without hints and checks for the seal (pref,
  postcard stamp, map card). It replays it with one hint and checks the seal is still there (never
  removed). On a different trip, it packs with one hint and checks no seal is given. Then RESTART
  (clear), Undo (back to "hinted"), RESTART, and a hint-free pack earns it. Screenshots of the postcard
  and the map.

### D. Pay the round-2 verification debt: the hint ghost's see-through

Round 2 made whatever hides Ask Grandpa's ghost go see-through, but it was only checked by eye.

- **Acceptance / verify:** in the covered-gap setup of `SeeThroughChecks`, with empty hands, a hint
  ghost placed in the gap makes the skis see-through. Clearing the hint restores them. Nothing else
  is faded.

### E. Gamepad: ready for a real controller test

There's still no physical controller test. An 8BitDo Pro 3 receiver is plugged into this
machine, but no controller is paired to it (only a `hidraw` node, no `event`/`js` device), and nobody
is here to press buttons.

- **Acceptance:** the player logs every input device it sees, at start and whenever one connects or
  disconnects (`[Input] added Gamepad "…" (layout …)`). A device that shows up only as a generic
  `Joystick` (not a `Gamepad`) gets a log line saying it isn't supported. Then a real test that "does
  nothing" can be diagnosed from `Player.log`. A short checklist, `docs/GAMEPAD-TEST.md`, lets
  the owner (or a Deck player) run the hardware test in about ten minutes.
- **Verify:** the autopilot's simulated gamepad produces the `[Input] added` / `removed` lines. A
  simulated `Joystick` produces the unsupported line. Physical hardware stays **unverified**, and the
  README keeps saying so.

Each item ends with `build-linux` (0 errors), the solver, a quick autopilot, and screenshots in
`docs/media/improvements/round3/`. The round ends with a full autopilot (0 FAIL, save untouched) and
a `-pttBench` re-run.

## Round 3 results (2026-10-06)

All five scope items shipped, plus one fix found along the way (F). The final full autopilot
(`Tools/autopilot.sh`, all 33 trips) had **194 PASS, 0 FAIL**, the real save was untouched, and
there was no player crash. The HUD layout pass is clean at 16:9, 16:10, 4:3 and 21:9 × 80/100/120%
(12/12). The solver proves all 33 levels, and `build-linux` has 0 errors. Screenshots are in
`docs/media/improvements/round3/`.

| Item | Commit | Verified by |
| --- | --- | --- |
| Quiet-machine benchmark (round-2 debt) | `8f2a2b4` | `-pttBench` at load 0.5 on round-2 `main`: the minivan is 1.8 ms packing / 1.8 ms holding, the same as round 1. Round 2's 2.7 ms was machine load. |
| A. Restart you can take back | `345977b` | `RestartChecks` (5 checks) click the real RESTART button and the pause menu's: 9 packed items go back to the blanket, the trunk is empty, and one Z puts all 9 back in the same cells and orientations. With nothing packed it says so and adds no undo step. `a-restart-unpacked.jpg` |
| B. Live star meter | `1f1d8c8` | `StarMeterChecks` (5): empty at first; 3 stars when everything's in; taking Grandma's 5 extras out reads 2,2,1,1,1 (3 of 5 still makes two); putting the third back earns two stars and the toast says what makes three; taking out an essential empties it. The meter matched the postcard on **all 33 trips** and on the 1-star early close. The layout pass now also checks that the counts line fits and that the meter clears the heading. `b-star-meter*.jpg` |
| C. Grandpa's seal | `d36f851` | `SealChecks` (10) plus 2 in the main flow: a hint-free pack is sealed (postcard, pref); a hinted three-star pack isn't, and says how to earn it; TRY AGAIN and RESTART start a fresh attempt, and undoing RESTART restores the "hinted" mark; a hinted replay keeps the seal; a 1-star replay keeps it; the map and album show and count seals (33/33 polaroids in the full run). `c-seal-*.jpg` |
| D. Hint ghost's see-through | `4da8f8e` | `SeeThroughChecks` puts Grandpa's ghost in the covered gap with empty hands: the skis go see-through and are restored when the hint clears (2 new checks). `d-hint-ghost-see-through.jpg` |
| E. Gamepad readiness | `f20acf9` | `InputReportChecks` (2): a simulated gamepad's `[Input] added/removed` lines, and a simulated generic `Joystick` gets the "not supported" line and toast. `docs/GAMEPAD-TEST.md` is the owner's checklist. **Still not tested on physical hardware.** `e-unsupported-joystick-toast.jpg` |
| F. Key hints never wrap | `dd4078b` | Found in the round-3 screenshots: after a rebind or an interface-size change, hint captions wrapped ("und / o"). The layout check now flags wrapped captions: 3 FAILs on the old code in a quick run, 0 after. `f-key-hints-before-after.jpg` |

**Benchmark (`-pttBench`, 1600×900, High)** after round 3, at load 3–7 (`Recordings/round3/bench-after.log`):
the 25-item minivan reads 1.9 ms packing (p99 3.8) and 1.9 ms holding (p99 3.4), against 1.8 / 1.8 ms
before at load 0.5. Every other screen is 1.7–1.8 ms. There are no frames over 33 ms, and there's about one GC per
phase, as before. Within the noise of a machine that wasn't fully quiet: no measurable cost.

Things round 3 turned up along the way:

- **The autopilot is unreliable when the machine is overloaded.** At load average 37–45 (other
  sessions' builds), two quick runs failed every input check from the title screen onwards: queued
  input events never registered. The same build passed 101/101 later at load 20. I rebuilt the
  previous commit to rule out round 3's code (it also passed at the lower load). The runs now wait
  for load < 22 first. Treat an all-FAIL run on a busy machine as environmental and re-run it.
- **A relative `-logFile` path lands in `Builds/Linux/`**, not the working directory, because the
  player resolves it against its own folder. Pass an absolute path (`autopilot.sh` with an absolute
  output folder does).
- **No controller showed up.** The player's `[Input]` report lists the mouse, the keyboard and a
  touchscreen, and nothing for the 8BitDo Pro 3 receiver plugged into this machine. That fits the
  receiver having no paired controller (only a `hidraw` node exists).
- **`[Audio]`:** the full run measured 2.7 dBFS before the limiter and −1.5 dBFS after (round 1: 1.4 / −1.5). As
  noted before, the input peak depends on how many scripted drops land together. Round 3's runs
  also add star chimes when the meter fills and extra pack-everything passes. The limiter holds the
  output at −1.5 dBFS.

Still open after round 3: Windows (needs the module), WebGL and hosting, signing and notarization,
releases and re-cutting the trailer (all owner decisions); a physical gamepad / Steam Deck test
(`docs/GAMEPAD-TEST.md`); README screenshots that show the star meter and the seal (a stills-only
capture, about 3 minutes, when the owner wants the gallery refreshed); and bigger replayability
ideas from ranked #12 (per-trip challenges, a solver-generated Garage Sale mode).

## Round 4 scope (2026-10-06, branch `improvements-4`)

Round 4 is about not losing work and about how the pieces feel in the hand. Today, leaving a trip
for any reason throws the packed trunk away: the pause menu's TRIP MAP and MAIN MENU (there's no
warning), quitting, and the rare Wayland player crash. On a 25-item minivan that can be twenty
minutes of packing. Players coming from other packing games will also try to **drag** things into
the trunk, and the game only understands click-to-pick-up, click-to-drop. Undo has no redo.

### A. Your trunk waits for you

- **Acceptance:** every change to the trunk (a drop, a lift, undo, RESTART) is saved for that trip,
  with the "asked Grandpa" mark so the seal can't be gamed by leaving and coming back. Starting the
  trip again (Continue, the trip map, after quitting or a crash) puts every packed thing back in the
  same cell, turned the same way, once the story texts are done, and a toast says so (RESTART still
  unpacks it). Each trip keeps its own trunk, and the title's Continue line says how many things
  are packed. Closing the trunk and erasing progress clear it. A saved trunk that no longer fits
  the level data (after an update) is dropped with a log line instead of loading half-broken.
- **Verify:** an autopilot section packs half of Grandma's Big Move after asking for a hint, leaves
  through the real pause menu (TRIP MAP), starts the trip again and checks every item's cell and
  orientation, the free-cell count, the hinted mark and the Continue text. It then closes the trunk
  and checks the next start is empty. A **crash test** (`Tools/resume_test.sh`) runs the player
  twice on a sandboxed save file: the first run packs half a trip and is killed with SIGKILL, and
  the second must restore the same trunk. The real save is hashed before and after.

### B. Drag to pack

- **Acceptance:** press on something (on the blanket or in the trunk), drag it, and let go over
  the trunk to drop it where the ghost is. Letting go where it won't fit says why and keeps it in
  your hand. Letting go off the trunk puts it back. Click-to-pick-up, click-to-drop works exactly
  as before (a press without a real drag doesn't drop on release). The gamepad's A works the same
  way. Grandpa's first tip mentions dragging.
- **Verify:** autopilot checks with real mouse events: a press-drag-release from the blanket into
  the trunk packs the item at the ghost's cell; a press-release in place still leaves it held; a
  release on a blocked spot keeps it held and shows the reason; a drag out of the trunk onto the
  driveway puts it back; and the whole existing click-based run still passes.

### C. Redo

- **Acceptance:** **Shift + Undo** (Shift+Z / Shift+Backspace; gamepad LB + View) redoes what undo
  took back, including a RESTART. Anything new (a drop, a RESTART) clears the redo list. Redoing a
  RESTART starts a fresh attempt for the seal, as RESTART does. The pause card, the undo tip and
  the README mention it. Redo follows the Undo binding, so a remapped undo key redoes with Shift.
- **Verify:** autopilot checks: three drops, three Z, three Shift+Z gives back the same trunk;
  Z, then a new drop, then Shift+Z does nothing ("Nothing to redo"); undo a RESTART, then redo it
  empties the trunk again.

### D. Owed from round 3: README screenshots and the macOS build

- **Acceptance:** the README gallery shows the live star meter and Grandpa's seal (stills-only
  capture through the repo's own pipeline; the trailer isn't re-cut). `build-mac` is re-run on
  the round-4 code.
- **Verify:** look at each image, check file sizes stay under the 1.4 MB cap, and check the
  `.app` with `file` (universal x86_64 + arm64). It still can't be run on a Mac here.

Each item ends with `build-linux` (0 errors), the solver, a quick autopilot (load checked first),
and screenshots in `docs/media/improvements/round4/`. The round ends with a full autopilot (0 FAIL,
save untouched) and a `-pttBench` run if the machine is quiet.

## Round 4 results (2026-10-06)

All four scope items shipped. The final full autopilot (`Tools/autopilot.sh`, all 33 trips) had
**216 PASS, 0 FAIL** (194 before, plus 22 new checks). There were no exceptions and no player
crash, and the real save was untouched. The load average was 17 at the start and 53 at the end,
because other sessions were running builds. The solver proves all 33 levels. `build-linux` and
`build-mac` both have 0 errors and 0 warnings. Screenshots are in `docs/media/improvements/round4/`.

| Item | Commit | Verified by |
| --- | --- | --- |
| A. Your trunk waits for you | `52d22ab` | `ResumeChecks` (10). It half-packs Grandma's Big Move after a hint and leaves through the pause menu's TRIP MAP while holding something lifted out of the trunk. Starting the trip again puts all 9 back in the same cells and orientations, with the hint mark and a toast on screen. The title's Continue line reads "2 packed, waiting for you", and CONTINUE brings them back. Closing the trunk clears the saved trunk, and a saved trunk that no longer fits is dropped. **Crash test** `Tools/resume_test.sh`: the player is SIGKILLed mid-trip on a sandboxed save file, and a second player restores 9/9 with the hint mark (run on the first build and again on the final build). `a-resume-*.jpg` |
| B. Drag to pack | `16563f2` | `DragChecks` (7), with real mouse events on Grocery Run. Press, drag and release drops the item at the ghost's cell. A plain click still only picks up. Releasing on a blocked spot keeps the item in hand and shows the reason. Releasing off the trunk puts it back, and packed things drag to a new spot. The existing click-based run is unchanged. `b-drag-holding.jpg` |
| C. Redo | `30aefb5` | `RedoChecks` (4) plus 1 gamepad check. Three Z then three Shift+Z gives back the same trunk. A new drop clears redo ("Nothing to redo"). Redoing an undone RESTART empties the trunk again and clears the hint mark, and Z restores both. LB + View redoes on the simulated pad. `c-pause-redo.jpg` |
| D. Screenshots and the macOS build | `a618280` | A stills-only capture regenerated 11 README screenshots and added `14-grandpas-seal`. The meter, the seals on the map and album, and the sealed postcard were checked by eye. All are under the 1.4 MB cap (the largest is 670 KB). `build-mac` on the final code produces a universal (x86_64 + arm64, checked with `file`) 161 MB `.app`. **Still not run on a Mac.** |

Things round 4 turned up along the way:

- **The restore toast was invisible at first.** `ShowHud` clears the toast, and the restore called
  `Toast` just before it. The first check only looked for the text in the hierarchy, so it passed.
  The toast now comes after `ShowHud`, and the check requires a visible toast (`ToastShowing`).
- **Grandpa's tips timed out early in captures.** Tips measured time with `Time.unscaledTime`, while
  a frame-locked capture follows `UiTime`. On a loaded machine the README tip still had faded half
  away. Tips now use `UiTime.Now`, which is the same clock for a player.
- **`record_trailer.sh` used to copy a backup over the real save on exit**, and kept its scratch in
  `/tmp`. Since round 1 the capture runs on a sandbox, so the script now only hashes the save before
  and after, and its scratch stays in the capture folder.
- **`make_trailer.py` needs Pillow, and the system Python doesn't have it.** Round 2 left a venv
  with Pillow at `Recordings/.venv` (gitignored); use `Recordings/.venv/bin/python
  Tools/make_trailer.py`. I missed it this round and briefly made a second throwaway venv in
  `Recordings/round4/`, now deleted. Nothing was installed system-wide.
- **No quiet-machine benchmark this round.** The load average stayed at 15–50 all session. The
  full run's `[Perf]` table (packing 9.8 ms average) reflects that load, not the game: round 3
  measured 1.9 ms at load 3–7. The new per-frame work is one mouse-distance check while dragging.
  Saving the trunk writes the prefs file once per drop, undo, redo or RESTART, never per frame.
  A `-pttBench` run on a quiet machine is still owed.
- **`[Audio]`:** 2.6 dBFS before the limiter and −1.5 dBFS after (round 3: 2.7 / −1.5).

Known limits of the new features: the title screen's parked car shows the next trip's blanket, not
the waiting trunk. A restored trunk has no undo history (RESTART still unpacks it as one undoable
step). Releasing a drag over the HUD keeps the item in hand rather than putting it back. (Round 5 fixed all three.)

Still open after round 4: Windows (needs the module), WebGL and hosting, signing and notarization,
releases and re-cutting the trailer (all owner decisions); a physical gamepad / Steam Deck test
(`docs/GAMEPAD-TEST.md`); a quiet-machine `-pttBench`; and the bigger replayability ideas from
ranked #12.

## Round 5 scope (2026-10-06, branch `improvements-5`)

**Measurement debt first.** The machine was quiet at the start of the round (load average 0.5), so
round-4 `main` was rebuilt and benchmarked straight away (`-pttBench`, 1600×900, High, load 1.0 at the
start and 5 at the end; `Recordings/round5/bench-before.log`). Every screen averaged **1.6–2.0 ms**. The
25-item minivan was **1.7 ms** packing (p99 2.6) and **1.8 ms** holding (p99 2.7). There were no frames
over 33 ms, and there was about one GC per phase. That matches round 3 (1.8 / 1.8 ms), so rounds 3 and
4 cost nothing measurable, and round 4's 9.8 ms reading was load. That debt is closed.

Round 5 finishes the "your trunk waits for you" story from round 4. Today a player who comes back to a
half-packed trunk loses their undo history. They also have to sit through the chapter card and the
texts again, and they can't see the waiting trunk anywhere before they open the trip. One rough edge
from drag-to-pack is also fixed.

### A. Undo and redo survive leaving (and crashes)

- **Acceptance:** the saved trunk also keeps the undo and redo history (the newest 50 steps, with
  which steps were RESTARTs and which RESTARTs undo the hint mark). After leaving, quitting or a crash, Z and
  Shift+Z walk through the same history as before. A RESTART is recoverable even across leaving:
  RESTART, leave, come back, and Z puts the whole trunk back. That trip opens with an empty trunk and a
  toast saying Z brings it back. The title's "waiting" line still only counts packed things. A
  history that doesn't fit the level any more is dropped together with the trunk (logged). Saving
  stays one prefs write per change, never per frame.
- **Verify:** `ResumeChecks` gains: pack 5 things one by one, leave through the pause menu, come back,
  Z ×5 matches each layout from before leaving in reverse, and Shift+Z ×5 gives the full trunk back. Then
  RESTART, leave, come back (empty), and Z brings the trunk and the hint mark back. The crash test
  (`resume_test.sh`) also checks that the undo depth survives SIGKILL and that one Z takes out the
  last thing packed. The saved size for the biggest trunk is logged.

### B. A waiting trunk you can see, and get back to quickly

- **Acceptance:**
  - The title and menu screens park the next trip's car with its waiting trunk already packed
    (those things aren't on the blanket).
  - On the Trip Map, a trip with a waiting trunk says so on its label ("9 packed, waiting").
  - Starting a trip with a waiting trunk skips the chapter card, shows the texts at once instead
    of typing them, and the trip card says what's waiting. The button reads BACK TO PACKING.
  - A fresh trip is unchanged.
- **Verify:** autopilot checks: after leaving Grandma's Big Move half-packed and opening the title
  (the preview is Grandma's when it's the next trip, else checked directly), the parked car has the
  same packed cells. The map label shows the count. On a trip that opens a chapter, the start button
  appears within about 1.5 s with a waiting trunk and not before the chapter card on a fresh start.
  Screenshots of the title, the map label and the story card.

### C. Letting go of a drag over the HUD puts the item back

- **Acceptance:** after a real drag, releasing over the HUD (the packing list, the buttons) puts the
  item back on the blanket, as releasing anywhere off the trunk already does. A plain click on the HUD
  while holding something is unchanged.
- **Verify:** a `DragChecks` addition drags a blanket item onto the packing list and releases: it is
  back on the blanket and nothing is held.

Each item ends with `build-linux` (0 errors), the solver, a quick autopilot (load checked first), and
screenshots in `docs/media/improvements/round5/`. The round ends with a full autopilot (0 FAIL, save
untouched), the crash test, and a `-pttBench` run if the machine is still quiet.

## Round 5 results (2026-10-06)

All three scope items shipped, and the measurement debt is paid. The final full autopilot
(`Tools/autopilot.sh`, all 33 trips) had **228 PASS, 0 FAIL** (216 before, plus 12 new checks). There
were no exceptions and no player crash, and the real save was untouched. The load average was 20
at the start and 29 at the end. The crash test (`Tools/resume_test.sh`) passed 5/5. The solver
proves all 33 levels, and `build-linux` has 0 errors and 0 warnings. Screenshots are in
`docs/media/improvements/round5/`.

| Item | Commit | Verified by |
| --- | --- | --- |
| Quiet-machine benchmark (owed since round 2) | `a017c98` | `-pttBench` on round-4 `main` at load 1–5: every screen 1.6–2.0 ms, and the minivan 1.7 ms packing / 1.8 ms holding (see the scope above). |
| A. Undo and redo survive leaving | `08e8c49` | `ResumeHistoryChecks` (7). It packs 5 things on Grandma's Big Move and leaves through the pause menu. Coming back gives undo depth 5, Z walks back through each of the 5 layouts, and Shift+Z redoes all five. The redo list survives leaving too. After RESTART and leaving, the trip opens empty with a toast, and Z brings the trunk and the hint mark back. The **crash test** now also checks that all 9 of 9 undo steps survive a SIGKILL and that one Z takes out the last thing packed. A fully packed 25-item minivan with its history saves **5.5 KB**. `a-empty-trunk-undo-toast.jpg` |
| B. A waiting trunk you can see | `3541b4a` | `WaitingTrunkChecks` (4). After leaving Into the Woods with 3 packed, the title's parked car has the same 3 in the same cells. The map label reads "3 PACKED, WAITING". On Weekend Getaway (it opens chapter II), BACK TO PACKING is ready 2.3 s after the trip starts when a trunk is waiting, while a fresh start is still on the chapter card at that point. The button then restores the trunk. All checked by eye too. `b-waiting-title.jpg`, `b-waiting-map.jpg`, `b-waiting-story.jpg` |
| C. Drag released over the HUD | `f8b44d9` | A `DragChecks` addition drags a blanket item onto the packing list and lets go: it's back on the blanket and nothing is held. |

Things round 5 turned up along the way:

- **The first quick run failed two checks because of the test itself.** An earlier section (the seal
  checks) leaves Weekend Getaway with a saved trunk, so the "fresh start" wasn't fresh, and
  `StoryToPacking`'s Space presses then closed the restored, full trunk. The section now forgets that
  trip's trunk first (`AutoForgetTrunk`). It shows that sections share one sandboxed save: a new
  section that relies on a fresh trip has to clear it.
- **Frame hitches follow the machine, not the build.** The first quick run logged 597 `[Perf] hitch`
  lines against about 70 in round 3, spread across old sections (menus, layout, results) as well as
  new ones. The re-run of the same code at the same load logged 124. Round 5 adds no per-frame work:
  the title's waiting trunk is static objects, and the saved history is written once per change.
- **The saved trunk now has a version 2 format.** It keeps the item list's signature, the packed
  layout and the newest 50 undo and redo steps, and every step is checked against the packing rules
  before anything is applied. Round-4 (v1) saves still load (without history). One rule is unchanged:
  an empty trunk after asking Grandpa is still a fresh start for the seal, so that case keeps no
  history. Otherwise undo could bring back a hinted packing without the hint mark.
- **`[Audio]`:** 2.3 dBFS before the limiter and −1.5 dBFS after (round 4: 2.6 / −1.5).
- **The full run's `[Perf]` table** (packing 18.7 ms average) reflects load 20–29, like round 4's.
- **No quiet-machine benchmark of the round-5 build.** After the full run I waited 45 minutes for
  load < 6, and it never got there (15–56). One `-pttBench` at load 13–15
  (`Recordings/round5/bench-after.log`) read about 16 ms on **every** screen, including the wagon,
  pause and settings, which round 5 doesn't touch, against 1.6–2.0 ms before at load 1–5. The frame
  times were flat across screens with CPU time per frame at 2.4–3.2 ms, which points at the shared iGPU
  being busy, not at the game. It says nothing about round 5's cost either way. The before numbers
  stand, and an after run on a quiet machine is still owed. The save was untouched.

Not done this round: `build-mac` wasn't re-run (round 5 changed only platform-independent C#; it
still can't be run on a Mac here). No README gallery refresh (the title screenshot would now show a
waiting trunk only on a save that has one).

Still open after round 5: Windows (needs the module), WebGL and hosting, signing and notarization,
releases and re-cutting the trailer (all owner decisions); a physical gamepad / Steam Deck test
(`docs/GAMEPAD-TEST.md`); gamepad button remapping; and the bigger replayability ideas from ranked
#12 (per-trip challenges, a solver-generated Garage Sale mode).

## Round 6 scope (2026-10-07, branch `improvements-6`)

**Measurement debt first.** The machine was quiet at the start of the round (load average 0.3), so
round-5 `main` was rebuilt and benchmarked straight away (`-pttBench`, 1600×900, High, load 0.8 at
the start and 6.4 at the end; `Recordings/round6/bench-round5-main.log`). Every screen averaged
**1.6–1.9 ms**. The 25-item minivan was **1.9 ms** packing (p99 3.0) and **1.9 ms** holding (p99
3.0). There were no frames over 33 ms and about one GC per phase. Round 4 `main` measured 1.7 / 1.8 ms
at load 1–5, so round 5 cost nothing measurable, and its 16 ms reading was GPU contention. That debt
is closed.

Rounds 1–5 covered how to play, getting unstuck, and not losing work. Round 6 is about players who
are less well served so far: people who want to look back at what they packed, people who can't
comfortably hold a key or stick while aiming, Steam Deck–sized screens, and people who get
uncomfortable with a lot of motion. Gamepad button remapping and D-pad menu navigation stay
deferred: Steam Input already remaps buttons on a Deck, the virtual cursor works in every menu, and
neither can be tried on real hardware here.

### A. Look at any album photo up close

The album promises "a photo of every trunk you close", but each polaroid is about 130 px wide and
can't be opened.

- **Acceptance:** clicking a polaroid that has a photo opens it large (at least three times the
  polaroid's width), with the trip's title, year, vehicle, stars and seal. ← / → (and A / D, the
  D-pad and the bumpers) flip to the previous or next photo, skipping trips without one. Esc, B, or a
  click outside the photo closes it, back to the album; Esc then leaves the album as before. Trips
  without a photo can't be opened. The finale's album behaves the same way.
- **Verify:** autopilot `AlbumChecks`: click a real polaroid, check the close-up shows that trip,
  press → and check it moved to the next trip with a photo, Esc closes it and the album is still
  open, clicking a photo-less polaroid does nothing. Screenshot of the close-up.

### B. X-ray: hold or toggle

X-ray needs Tab held while you aim with the mouse, or the left stick **held in while you move the
same stick**, which is awkward on a pad and hard for anyone using one hand.

- **Acceptance:** Settings → Gameplay → "X-ray" chooses **Hold** (the default, as now) or **Toggle**.
  In Toggle, one press turns X-ray on and the next turns it off; a small "X-RAY ON" tag shows while
  it is on, and it switches off by itself when the trunk closes, the trip ends or you leave. The key
  hints, the pause card and Settings → Controls say "hold" or "press" to match.
- **Verify:** autopilot checks: in Toggle, one Tab press (and one simulated left-stick click) turns
  X-ray on with everything packed see-through and stays on after release; a second press turns it off
  and restores everything; leaving the trip turns it off; Hold behaves as before (the existing
  `SeeThroughChecks` still pass). Screenshot of the tag.

### C. Readable on a Steam Deck–sized screen

The HUD is laid out against 1920×1080 and shrinks on smaller screens. At 1280×800 the smallest
labels (the FRAGILE / EXTRA stamps, years, small captions) come out around 9 px.

- **Acceptance:** a new legibility check measures every visible text's rendered size in screen
  pixels on the title, menus, settings, packing HUD, pause, postcard and album. At 1280×800 with
  default settings, no text is smaller than **12 px** (Valve recommends 12 px for the smallest text at
  1280×800). Until the player picks an interface size, the default follows the screen (bigger on
  small screens), and picking one in Settings still wins. The HUD layout pass stays clean at
  1280×800 and at the existing 16:9 / 16:10 / 4:3 / 21:9 sizes.
- **Verify:** the legibility check (minimum size logged per screen) at 1280×800 before and after,
  the layout pass at 1280×800 and 1600×900, and screenshots at 1280×800.

### D. Reduce motion

- **Acceptance:** Settings → Gameplay → "Reduce motion" (off by default). When on: panels and cards
  fade in instead of sliding, scaling or bouncing; idle bobbing and pulsing buttons stay still; the
  paper-wipe transition becomes a short fade; screen shake is off; confetti still pops but the camera
  doesn't shake. Off changes nothing.
- **Verify:** autopilot checks with it on: a panel's intro keeps its position and scale while it
  fades, the pulsing close button and the title letters stay still, a transition doesn't move the
  wipe, and the camera doesn't shake on a slam. Off: the existing checks pass unchanged.

### E. Owed housekeeping

`build-mac` is re-run on the round-6 code (it wasn't in round 5) and checked with `file`. It still
can't be run on a Mac here.

Each item ends with `build-linux` (0 errors), the solver, a quick autopilot (load checked first), and
screenshots in `docs/media/improvements/round6/`. The round ends with a full autopilot (0 FAIL, save
untouched), the crash test, and a `-pttBench` run if the machine is still quiet.

## Round 6 results (2026-10-07)

All four scope items shipped, and the round-5 benchmark debt is paid (see the scope above). The final
full autopilot (`Tools/autopilot.sh`, all 33 trips) had **262 PASS, 0 FAIL** (228 before, plus 34 new
checks). There were no exceptions and no player crash, and the real save was untouched. The load average
was 12 at the start and 8 at the end. The solver proves all 33 levels, and `build-linux` has 0 errors and
0 warnings, and `build-mac` gives a universal (x86_64 + arm64, checked with `file`) 161 MB `.app` with 0
errors (still not run on a Mac). The crash test (`Tools/resume_test.sh`) passed 5/5. Screenshots are in
`docs/media/improvements/round6/`.

**Benchmark (`-pttBench`, 1600×900, High).** The first run of the round-6 build read 3.2–3.9 ms on every
screen at load 6–9, against 1.6–1.9 ms for round-5 `main` in the morning, but that slowdown was flat across
screens round 6 doesn't touch and in boot steps it doesn't change (audio setup 50 → 126 ms), with 0 KB
allocated per frame. So both builds were benchmarked back to back at load 4–7 (`Recordings/round6/`
`bench-main-ab.log`, `bench-after-3.log`): **round-5 `main` and round 6 both average 1.6–1.8 ms on every
screen**, and the 25-item minivan is 1.8 ms packing and 1.8 ms holding in both. No frames over 33 ms, about
one GC per phase. Round 6 costs nothing measurable; the first reading was the shared machine.

| Item | Commit | Verified by |
| --- | --- | --- |
| B. X-ray: hold or toggle | `2c0c1e7` | `SeeThroughChecks` + 1 gamepad check (5). In Toggle, one Tab press turns X-ray on and it stays on after the key is let go (every packed thing see-through, the aim reaches the covered gap, the key hint reads "x-ray on", a toast says how to turn it off); a second press turns it off; the pause card says "(press)"; leaving through the pause menu turns it off. A simulated pad's L3 click does the same. Hold is unchanged (the existing checks pass). `b-xray-toggle-on.jpg`, `b-settings-accessibility.jpg` |
| A. Album close-ups | `7581bf2` | `AlbumChecks` (6). Clicking a polaroid opens its photo 6.1× as wide as the polaroid, with title, date, car, stars, seal and "photo 1 of N". → goes to the next photo, A back, ← from the first wraps to the last; Escape and a click outside close it with the album still open; a polaroid without a photo doesn't open; Escape then leaves the album as before. `a-album-zoom.jpg` |
| C. Readable on a Steam Deck–sized screen | `ed91295` | New `LegibilityChecks` measure every visible text in screen pixels on 15 screens. **Before** at 1280×800: the packing list's stamps 8.9 px, album captions 8.7 px, map dates and the now-playing caption 10 px. **After**: 12 px or more on every screen at 1280×800, 1440×900, 1600×900, 1200×900 and 2100×900 (layout-only runs, 25/25 each). New `MenuLayoutChecks` (main menu and trip map at 80/100/120%) and the now-playing cassette in the HUD layout pass, all clean at those five sizes. `c-1280x800-packing-before-after.jpg`, `c-1280x800-album.jpg`, `c-1600x900-120-menus.jpg` |
| D. Reduce motion | `ac0f13b` | `ReduceMotionChecks` (3) run the same steps with it off and on. Off: the title letters bob, the settings card slides in, the wipe moves, the camera sweeps in, CLOSE THE TRUNK pulses and the slam shakes. On: none of those move, while the card and the scene change still fade (sheet alpha down to 0). `d-settings-reduce-motion.jpg` |

Things round 6 turned up along the way:

- **Interface sizes above 100% had never worked on the menus.** The round-2 layout pass only covered the
  HUD. At 110–120% on a 16:9 screen the canvas is shorter than 1080 units, so the main menu ran into the
  tagline, and the trip map's page covered the story note. The now-playing cassette also covered the
  HINT button whenever the canvas was 1600 units wide (120% at 16:9), for the 4.5 s it shows. All three
  now fit (the menu and page shrink, the cassette moves under the trip tag or gives way to Grandpa's
  tip), and checks cover them.
- **So the automatic interface size stops at 110%.** The first version gave a 1280×800 screen 120%,
  which exposed the menu problems above. It now goes only as far as the menus keep their full layout
  (110% at 16:10 and 4:3, 100% at 16:9 and 21:9). The handful of fixed small labels went up to 18 units,
  and the packing list and polaroids size their smallest text from the screen, so 110% is enough.
- **Accessibility settings moved to their own tab.** The Gameplay tab had no room for two more rows, so
  placement colours, X-ray, screen shake, reduce motion and story text speed are now under
  Settings → Accessibility (six tabs, slightly narrower).
- **The full autopilot now takes about 14 minutes**, close to `autopilot.sh`'s 15-minute player timeout,
  so the timeout is now 20 minutes.
- **A section-order dependency bit again.** `InputReportChecks` assumed the section before it left a trip
  in progress; after the new legibility pass (which ends on the title) its toast check failed in a quick
  run. It now starts its own trip.
- **`[Audio]`:** 3.7 dBFS before the limiter and −1.5 dBFS after (round 5: 2.3 / −1.5). The new sections
  close extra trunks (more slams and star chimes); the limiter holds the output at −1.5 dBFS.

Known limits: Reduce motion leaves the car's own movement (arriving in the driveway, driving off after the
slam), confetti, and the small pops of a ticked row or an earned star. The album photos are still captured
at 480×360, so the close-up is a little soft. The legibility pass measures font size times scale; it
doesn't judge contrast. The L3 toggle is checked with a simulated pad only, and the close-up's D-pad and
bumper flipping isn't exercised by the autopilot at all (the keyboard arrows and A / D are).

## Round 7 scope (2026-10-07, branch `improvements-7`)

**Baseline.** Round-6 `main` was rebuilt (0 errors) and benchmarked at the start of the round
(`Recordings/round7/bench-main.log`). Load climbed from 9 to 17 during the run as other sessions started,
and every screen read 6–10 ms (1.6–1.9 ms on a quiet machine in round 6), with 0 KB allocated per frame. That
is contention, not a regression; the round ends with a back-to-back A/B instead.

Rounds 1–6 covered how to play, getting unstuck, not losing work, and accessibility of motion, size and
colour. Round 7 picks up what's still rough for a real player: menus on a controller, the album's soft
close-ups, text contrast (the legibility check measures size only), and a reason to go back to a trip you
didn't finish.

### A. D-pad menu navigation (controller)

On a pad, every menu works only by steering the stick cursor onto a button. On a Deck that's slow, and the
D-pad does nothing outside packing.

- **Acceptance:** whenever you're not packing (title, menus, settings, trip map, story, pause, postcard,
  album), a D-pad press jumps the gamepad cursor to the nearest button, switch, slider or polaroid in that
  direction that's on top and can be clicked, and A clicks it as before. Holding a direction repeats.
  On a settings slider, D-pad left/right changes the value in steps instead of moving away. While packing
  (no menu open) the D-pad keeps its packing jobs (shelf, hint, close); the pause menu over a trip
  navigates. In the album close-up the D-pad and bumpers still flip photos. The stick cursor is unchanged.
- **Verify:** `GamepadChecks` gains: on the main menu, D-pad down moves the cursor onto a different
  button and A on it opens that screen; in Settings → Audio, D-pad onto the master volume slider and
  D-pad right raises it (restored afterwards); in the pause menu, D-pad down reaches a button. The
  close-up's D-pad right, D-pad left and RB/LB flipping (owed from round 6) gets checks too. The
  keyboard/mouse checks pass unchanged.

### B. Sharper album photos

Each trunk is photographed at 480×360 and shown up to 920 units wide in the close-up, so it's soft, and
the 130-unit polaroids are minified without mipmaps.

- **Acceptance:** new photos are 1024×768, rendered with 4× MSAA, and saved as JPEG (quality 92) so a full
  album stays small and decodes quickly; textures get mipmaps so the small polaroids don't shimmer. Old
  480×360 PNG photos still load and show until that trip's trunk is closed again (then the PNG is
  replaced). Closing the trunk still never stalls a frame (read-back and encoding stay asynchronous).
- **Verify:** autopilot check that a freshly closed trunk's photo is 1024×768 on disk and in the album; a
  legacy PNG placed in the sandboxed album loads; file size and decode time logged. Close-up screenshot
  before/after at 1600×900.

### C. Text contrast check (and fixes)

- **Acceptance:** a contrast check runs alongside `LegibilityChecks` on the same 15 screens. For every
  visible text it reads the rendered pixels: the background is the median colour of a thin band just
  outside the glyphs, the text colour the glyph pixels furthest from it, and the WCAG contrast ratio is
  computed. Body text must reach **4.5:1**, large text (24 px or more on screen) **3:1**. Anything below is
  fixed (colour or backing), or listed in the results with a reason if it's decorative.
- **Verify:** the check's per-screen minimum before and after at 1600×900 and 1280×800 (layout-only
  runs), and screenshots of anything changed.

### D. Your best so far, on the trip card

Once a trip is closed, nothing tells you what you'd gain by packing it again.

- **Acceptance:** starting a trip you've closed before, the trip card adds one line: with fewer than 3
  stars, the best stars and what that best run left on the curb ("Best: 2 stars. Left on the curb: tuba,
  lamp"); with 3 stars and no seal, that Grandpa's seal is still there to earn; with both, nothing extra.
  A fresh trip is unchanged. The left-behind list is saved with the best result only (a worse replay
  doesn't overwrite it) and fits the card at every interface size.
- **Verify:** autopilot checks: close the wagon with an extra left out, start it again, the card names
  that extra and the star count; close it with everything, the line asks for the seal (or is gone if
  sealed). Layout and legibility passes stay clean. Screenshot.

### E. Housekeeping

`build-mac` is re-run on the round-7 code and checked with `file`. The round ends with `build-linux`
(0 errors), the solver, a full autopilot (0 FAIL, real save untouched, load noted), the crash test, and a
back-to-back `-pttBench` A/B against round-6 `main` if the machine is quiet enough.

Each item ends with `build-linux`, the solver and a quick autopilot (load checked first), with
screenshots in `docs/media/improvements/round7/`. If an item turns out bigger or riskier than planned,
the others land first and it's reported rather than half-landed.

## Round 7 results (2026-10-07)

All four scope items shipped. The final full autopilot (`Tools/autopilot.sh`, all 33 trips) had **304 PASS,
0 FAIL** (262 before, plus 42 new checks). There were no exceptions and no player crash, and the real save was
untouched. Load average was 15.6 at the start and 9.3 at the end. The crash test (`Tools/resume_test.sh`) passed
5/5. The solver proves all 33 levels. `build-linux` has 0 errors and 0 warnings. `build-mac` gives a universal
(x86_64 + arm64, checked with `file`) 160 MB `.app` with 0 errors; it still hasn't been run on a Mac. Screenshots
are in `docs/media/improvements/round7/`.

**Benchmark (`-pttBench`, 1600×900, High), back to back at load 10–16** (`Recordings/round7/bench-ab-*.log`).
Round 7 averaged 3.5–4.6 ms on every screen and round-6 `main` 2.5–5.7 ms, with CPU time per frame 1.6–2.2 ms
in both, 0 KB allocated per frame, and no frames over 33 ms. That's no measurable difference. The absolute
numbers are higher than round 6's quiet-machine 1.6–1.8 ms because the machine was busy; it never got below
load 8 this round.

| Item | Commit | Verified by |
| --- | --- | --- |
| B. Sharper album photos | `c7fb330` | `PhotoChecks` (3) and an `AlbumChecks` addition. A closed trunk is saved as a 960×720 JPEG (100 KB) and a 320×240 thumbnail (17 KB). Read back, the polaroid gets the thumbnail with 9 mip levels (2.0 ms to decode) and the close-up the full photo (8.8 ms, load 10.5). An old 480×360 PNG still loads for both, and the close-up shows the full 960×720 photo. `b-album-closeup-before-after.jpg` |
| A. D-pad menu navigation | `b6447ab` | `PadMenuChecks` (8) with a simulated pad. While packing, the D-pad doesn't move the cursor. The D-pad walks the pause menu to RESUME and A resumes. It walks the main menu to SETTINGS and A opens it. It reaches the AUDIO tab and the master volume slider, where right raises it 0.90 → 0.95 and left lowers it back. B closes Settings. It reaches a polaroid and A opens the close-up, where D-pad right and RB go forward and LB and D-pad left go back (owed since round 6); B closes it. `docs/GAMEPAD-TEST.md` has the matching hardware steps. |
| C. Text contrast | `572c39f`, `29652a4`, `f9e02af` | The legibility pass now also checks contrast on 20 screens (every settings tab and all six trip-map pages) at 1600×900, 1280×800, 1440×900, 1200×900 and 2100×900 (layout-only runs, 50/50 at each size on the final code), plus in the full run with all trips open. Every text passes 4.5:1, or 3:1 for large or bold text. `c-contrast-story-before-after.jpg`, `c-contrast-postcard-before-after.jpg` |
| D. Best so far on the trip card | `35ad733` | `BestSoFarChecks` (5) on Grocery Run. A fresh trip has no line. Closed with an extra left out, the card reads "Best so far: 2 stars. On the curb: Birthday Bouquet." and the details still fit. A worse replay (1 star) leaves the line as it was. Three stars after a hint asks for the seal; with the seal there's no line. |

What the contrast check found on the old colours (WCAG ratio of the colours involved):

- White labels on the bright orange, teal and green buttons: 1.9–2.6:1. The pills tint their face ×1.2 and
  add a white shine, so on screen they're lighter than the theme colour. Darkening the faces enough would
  have meant about 30% darker buttons everywhere. Instead the labels got a thin ink outline, like the
  title's lettering. WCAG counts a narrow border as part of the letter, and ink on those faces is 5–7:1.
- Orange text on paper (the trip card's date line, the postcard's place, Grandpa's tip header, the keyboard
  and gamepad headers): 2.4:1. Teal ON: 2.7:1. Both now use deeper inks (`AccentInk` 4.8:1, `TealInk` 5.2:1).
- The small EXTRA stamp: 1.9:1. Stamps now deepen their ink against their own fill (4.6:1 or more).
- The version line 3.6:1, "or press SPACE" over the driveway 3.8:1, and the main menu's captions over bright
  grass 3.5:1. Respectively: brighter, a dark outline, and a soft shade behind the menu.
- Trip-map pins wear their car's colour, so the white number was 1.2–2.8:1 on 8 of 33 trips (the yellow
  Mini, the orange clown car, the pink wedding car, the white house car). Those now use ink.
- At 4:3 the interface is smaller, so ON and ERASE fell below the bold-large size (18.7 px). TealInk went
  deeper and the settings row button's label went from 24 to 28.

Things round 7 turned up along the way:

- **`Texture2D.Compress` broke the album.** The first version compressed decoded photos to DXT1 to save
  memory. In the Linux player those came out black in the polaroid and blank in the close-up. The album now
  loads small thumbnails (with mipmaps) and decodes one full photo at a time for the close-up, so it never
  compresses. A full album takes about 10–13 MB for the polaroids plus 2–3 MB for the close-up. The scope's
  1024×768 became 960×720 (twice the old size, and about as big as the close-up shows it at 1080p).
- **The album checks used a fixed 11 s wait.** The album deals out 33 polaroids at a quarter of a second
  each (at least 9 s), and at load 26 a seal count check failed. Album checks now wait until the album is
  done.
- **The best-so-far test closed nothing at first.** Leaving out an extra that something else rests on left
  essentials unplaced, so the trunk couldn't close. The test now leaves out only extras with nothing on top,
  and checks that the trunk closed.
- **The contrast check has rules worth knowing.** It reads the background from the screen (median of a band
  1–3 px outside the glyphs) and uses the text's own colour, as WCAG does. It also accepts the rendered glyph
  pixels or a solid outline when those stand out more. It skips text under an overlay, text faded below 60%,
  and parts of controls that can't be used (locked trips), which WCAG doesn't hold to a ratio. Stamps are
  checked against their known fill, because their letters nearly touch the border.
- **Commits.** The four items were built in one working tree and split into per-item commits afterwards.
  Only the final commit was built and tested. The intermediate commits weren't compiled on their own.

Known limits:

- Photos taken before round 7 stay 480×360 until that trunk is closed again.
- The contrast check doesn't check a `<color>` part inside a line separately (the waiting line's accent
  uses `AccentInk`). It only covers the screens the self-test visits.
- D-pad navigation picks the nearest control by geometry. It's been tried only with a simulated pad.
  Gamepad button remapping is still not offered (Steam Input covers it on a Deck).
- The deeper orange text and outlined button labels are a visible change to the look. Both are a
  one-line change in `UiTheme` if the owner prefers the old style.

Still open after round 7:

- **Owner decisions:** Windows (needs the module), WebGL and hosting, signing and notarization, licences,
  releases and re-cutting the trailer (the trailer and README stills predate the outlined button labels).
- **Needs hardware:** a physical controller and Steam Deck test (`docs/GAMEPAD-TEST.md`) and a Mac run.
- **Bigger ideas:** replayability from ranked #12 (per-trip challenges, a solver-generated Garage Sale mode).
- **Measurement:** a benchmark on a quiet machine (load < 6).

## Round 8 scope (2026-10-07, branch `improvements-8`)

**Measurement debt first.** The machine was quiet at the start of the round (load average 0.8), so round-7
`main` was rebuilt and benchmarked straight away (`-pttBench`, 1600×900, High, load 3 at the start and 8 at the
end; `Recordings/round8/bench-main.log`). Every screen averaged **1.7–2.6 ms** (the main menu and pause had the
highest p99, 8.2 and 6.2 ms). The 25-item minivan was **2.3 ms** packing (p99 4.6) and **2.1 ms** holding (p99
5.1). There were no frames over 33 ms, 0 KB allocated per frame and about one GC per phase. Round 6 measured
1.6–1.9 ms at load 4–7, so round 7 costs nothing measurable. That debt is closed.

**A first look at 1280×720.** No round had measured the most common small window (and a docked Deck's 720p).
A layout-only run at 1280×720 on round-7 `main` (`Recordings/round8/layout-1280x720-before`) passed the HUD
and menu layout at 80/100/120% and every text was 12 px or more, but the title screen's tagline failed contrast
(3.5:1 on the title, 4.3:1 on the main menu; it needs 4.5:1).

Rounds 1–7 covered how to play, getting unstuck, not losing work, motion, size and colour, and menus on a
controller. Round 8 picks up a player the game still can't serve at all (someone who can only use the mouse),
the 720p gap above, gamepad button remapping (open since round 5), and a README that shows the game as it looks
now.

### A. Play with the mouse alone

Turning works with a right click and the wheel picks a shelf, but tipping, rolling and X-ray need the keyboard
(or a pad), so a one-handed or mouse-only player can't pack anything taller than it is wide.

- **Acceptance:** the card for the thing in your hands gets a row of four small buttons: **TURN**, **TIP**,
  **ROLL** and **X-RAY**. TURN, TIP and ROLL do exactly what R, T and F do (around the same camera-snapped axes;
  Shift+click turns the other way, like Shift+key). X-RAY turns X-ray on for as long as you're holding
  something and the button shows that it's on; clicking it again, dropping or putting the item back turns it
  off, so it can never leave a mouse-only player unable to pick packed things back up. The keys, the setting
  for X-ray (hold / toggle) and the gamepad are unchanged. Grandpa's turn tip mentions the buttons. The card
  still fits every screen shape and interface size without covering the toast, the key hints or the list.
- **Verify:** autopilot `MouseOnlyChecks` with real mouse clicks on a trip with a non-cube item: clicking
  TURN, TIP and ROLL gives the same orientation as pressing R, T and F from the same start (checked against the
  key result); Shift+click reverses; X-RAY makes every packed thing see-through and lets the aim reach a covered
  cell, a second click restores it, and dropping the item restores it too; a whole small trip is packed with no
  keyboard event at all (mouse clicks and the wheel only). The HUD layout pass (16:9, 16:10, 4:3, 21:9 ×
  80/100/120%), the legibility and the contrast checks stay clean with the card showing. Screenshot.

### B. Readable at 1280×720

- **Acceptance:** the title tagline reaches 4.5:1 at 1280×720 (and stays at every other measured size), without
  changing the contrast style that's waiting on the owner (outlined labels, deeper orange). 1280×720 joins the
  sizes the layout-only pass is run at, and the README's list of measured sizes says so.
- **Verify:** layout-only runs (`PTT_LAYOUT=1`) at 1280×720 before (2 FAIL) and after (0 FAIL), and at
  1600×900, 1280×800 and 1200×900 after. Before/after screenshot of the title.

### C. Gamepad button remapping (riskiest; lands only if it's clean)

- **Acceptance:** Settings → Controls lets each packing action on the pad (turn, tip, roll, undo, Ask Grandpa,
  X-ray, close the trunk, shelf up and down) be given another button, the same way keys are: pick the action,
  press the button; a button that's already used swaps; Defaults restores. A (click), B (back) and Menu (pause)
  stay fixed so the menus can always be driven. The packing loop, the pad key hints, the pause card and the
  toasts that name a pad button all follow the bindings. Keyboard bindings are unchanged.
- **Verify:** autopilot `PadRebindChecks` with a simulated pad, through the real Controls tab: bind turn to LB…
  (or another free button), check the new button turns and the old one doesn't, the hint strip names it, a
  clash swaps, Defaults restores, all in the sandboxed save. The existing `GamepadChecks` and `PadMenuChecks`
  pass unchanged. **Still not tested on a physical controller.**

### D. README screenshots that show the current look

- **Acceptance:** the README gallery is regenerated with the repo's own stills pipeline
  (`PTT_STILLS_ONLY=1 Tools/record_trailer.sh` + `make_trailer.py --only stills`), so it shows the outlined button
  labels, the deeper inks and the held card's new buttons. The trailer, poster and teaser are not re-cut (owner
  decision).
- **Verify:** look at every image; README links resolve; each file stays under the script's 1.4 MB cap; the
  real save is hashed before and after.

### E. Housekeeping

Each item is built (`build-linux`, 0 errors) and tested on its own commit before the next starts: the solver, a
quick autopilot (load checked first) and screenshots in `docs/media/improvements/round8/`. The round ends with a
full autopilot (0 FAIL, real save untouched, load noted), the crash test, and a back-to-back `-pttBench` A/B
against round-7 `main` if the machine is quiet enough. If an item turns out bigger or riskier than planned, the
others land first and it's reported rather than half-landed.

## Round 8 results (2026-10-07)

Three of the four scope items shipped as planned and one (A) shipped in a different form than planned (see
below). The final full autopilot (`Tools/autopilot.sh`, all 33 trips) had **317 PASS, 0 FAIL** (304 before, plus
13 new checks). There were no exceptions and no player crash, and the real save was untouched. Load average was
5.3 at the start and 18 at the end. The crash test (`Tools/resume_test.sh`) passed 5/5. The solver proves all 33
levels. Each item's commit was built on its own (`build-linux`, 0 errors, 0 warnings) and tested with a quick
autopilot (load 17–21) before the next item started. `build-mac` wasn't re-run (round 8 is platform-independent
C#). Screenshots are in `docs/media/improvements/round8/`.

| Item | Commit | Verified by |
| --- | --- | --- |
| Quiet-machine benchmark (round-7 debt) | `26a5562` | `-pttBench` on round-7 `main` at load 3–8: every screen 1.7–2.6 ms, the minivan 2.3 ms packing / 2.1 ms holding, 0 KB per frame (see the scope). |
| A. Play with the mouse alone | `a675a28` | `MouseOnlyChecks` (5). With empty hands, clicking turn says to pick something up first. Clicking the R / T / F hints gives exactly the orientation the keys give, and Shift + click matches Shift + key (6 of 6). Clicking the X-ray hint makes everything packed see-through and the hint reads "x-ray on"; a second click turns it off; putting the item back or dropping it turns it off too. **Weekend Getaway packed 8/8 and closed for three stars with mouse events only** (10 hint clicks, 6 of them tip or roll). The HUD layout pass is 50/50 at 1600×900, 1200×900, 2100×900 and 1280×800. `a-mouse-only-xray-on.jpg`, `a-mouse-only-packed.jpg` |
| B. Readable at 1280×720 | `4656bd9` | Layout-only runs on round-7 `main` at 1280×720: 2 FAIL (the title tagline, 3.5:1 and 4.3:1). After: 50/50 at 1280×720, 1600×900, 1280×800, 1200×900 and 2100×900. |
| C. Gamepad button remapping | `eec672e` | `PadRebindChecks` (8) with a simulated pad and the pad only: Settings → Controls lists the pad buttons; A on an action waits for a button and A itself isn't taken; B cancels and Settings stays open; R3 binds turn; R3 then turns the held item and X doesn't; the controller hints show R3; binding R3 to undo swaps it with turn (turn becomes VIEW); Defaults restores every button and the hints. Every existing gamepad and D-pad menu check still passes. Controls tab legibility and contrast pass at 1280×720 and 1200×900. **Not tested on a physical controller.** `c-pad-controls-rebound.jpg` |
| D. README screenshots | `f6c00bd` | A stills-only capture (16 stills; it took about 25 minutes rather than 3, since it waited for load and ran under load) and `make_trailer.py --only stills`. All 14 gallery images were regenerated with the current look and checked by eye. The alt texts still match. The largest is 586 KB, under the 1.4 MB cap. The capture script confirmed the save was untouched. |

**How A changed from the plan.** The scope put TURN / TIP / ROLL / X-RAY buttons on the held-item card. That
needed a taller card. At 1280×720 the card already reaches the minivan's rear bumper, and 50 more units would
have covered the near-left cells of the biggest trunk's floor. So the existing key hints along the bottom became
the buttons instead. They take no new space, they show the key as they teach it, and the layout already fits
them at every size. The turn tip now says the keys can be clicked. One trade-off: with Settings → Gameplay → Key
hints switched off, there are no on-screen tip / roll / X-ray buttons. The X-ray hint now outranks undo and
shelf when the strip is too narrow, so it stays visible.

**What B actually fixed.** The tagline is ink on a pale yellow card, about 13:1. It "failed" only because the
check sampled the background in a screen-aligned box around the text. The card is tilted 2°, so at 720p that
box's corners fell off the card onto the dark road. The check now classifies each pixel in the text's own
(rotated) space, so the band hugs the card. No colours changed, and the contrast style waiting on the owner is
untouched. Every other tilted text (the luggage tag, the packing list, the tip card) is measured the same way now,
and all still pass at five sizes.

**Benchmark (`-pttBench`, 1600×900, High), back to back.** The machine never stayed quiet through an A/B. Round 8
read 1.9–2.2 ms on most screens at load 6–13. The album (3.8 ms) and credits (2.7 ms, 3 frames over 33 ms) were
higher, at the end of the run as load climbed. Round-7 `main` straight after read a flat 3.1–3.4 ms at load
8–10. A second round-8 run read a flat 4.6–5.3 ms at load 7.5, with no frames over 33 ms. All three runs had 0 KB
allocated per frame and 1.3–2.4 ms of CPU per frame in both builds. The spread follows the shared GPU, not the
build (`Recordings/round8/bench-ab-*.log`). Round 8 adds no per-frame work beyond reading cached pad bindings.
No measurable cost.

Things round 8 turned up along the way:

- **The toast's x position was overwritten every frame.** `PlaceToast` centres it in the space left of the list,
  but `UpdateHudMotion` reset x to −200 each frame. It's harmless today: the intended position works out to −250
  at every width, and the layout pass is clean. A fix was written for the held-card layout and then dropped
  with it, so the code is unchanged.
- **The full-trip mouse check never needed the wheel.** On Weekend Getaway, aiming at the right column already
  gave the solver's height for all 8 placements (0 wheel steps). The wheel's shelf choice is covered by the
  existing `SeeThroughChecks` (W/S) and is unchanged, but this section doesn't exercise it.
- **`[Audio]`:** 2.8 dBFS before the limiter and −1.5 dBFS after (round 7's full run: 3.7 / −1.5).

Known limits:

- Mouse only: redo still needs Shift + Z. Clicking a hint moves the pointer off the trunk, so the held item
  follows it down to the strip until you point back in.
- Gamepad remapping covers the packing actions only. A, B, Menu, LB, the sticks and the triggers can't be moved,
  and menu navigation stays on the D-pad. The labels are Xbox names (PlayStation and Nintendo pads show the same
  positions under other names). It has only been driven by a simulated pad.
- The contrast check still doesn't check a `<color>` part inside a line separately, and windows smaller than
  1280×720 aren't measured.

Still open after round 8:

- **Owner decisions:** the contrast look from round 7 (now also in the README gallery), Windows (needs the
  module), WebGL and hosting, signing and notarization, licences, releases and re-cutting the trailer (the
  trailer, poster and teaser are still the v0.1.0 cut).
- **Needs hardware:** a physical controller and Steam Deck test (`docs/GAMEPAD-TEST.md`, now including
  remapping) and a Mac run.
- **Bigger ideas:** replayability from ranked #12 (per-trip challenges, a solver-generated Garage Sale mode).
- **Measurement:** an A/B benchmark with both builds on a quiet machine (load < 6 throughout).

## Round 9 scope (2026-10-07, branch `improvements-9`)

**Measurement debt first: the round-7 vs round-8 A/B.** The machine was nearly idle at the start of the round
(load 0.5), so both builds were benchmarked back to back (`-pttBench`, 1600×900, High) in a private, headless
nested KWin (see E), which keeps the window off the shared desktop and away from its compositor
(`Recordings/round9/bench-nested-*.log`):

| Build | Load | Every screen (avg) | Minivan packing / holding | p99 worst | >33 ms | Alloc/frame |
| --- | --- | --- | --- | --- | --- | --- |
| Round 8 (`cdfb912`) | 0.7 → 6.8 | 1.7–2.0 ms | 1.8 / 1.9 ms | 4.2 ms (credits) | 0 | 0 KB |
| Round 7 (`90f0170`) | 7.9 → 10.3 | 1.8–2.4 ms | 1.9 / 2.4 ms | 4.9 ms (story) | 1 (album) | 0 KB |

Round 8 costs nothing measurable (it was the faster of the two, at the lower load). A third run (round 8 again)
at load 10–14 read a flat 2–7 ms on every screen as other sessions started, which is contention again; it's in
the same folder. That debt is closed.

**A first look below 1280×720.** Settings → Display offers every resolution down to 1024 pixels wide, but no
round had measured one. A layout-only run at 1024×768 on round-8 `main` (`Recordings/round9/layout-1024x768-before`)
kept every HUD and menu layout check clean, but **19 of 50 checks failed**: text came out at 10.6–11.7 px on 18 of
the 20 screens (the version line, trip-map dates and captions, the trip card, the FRAGILE / EXTRA stamps, key-hint
captions, the postcard's key line), and Settings → Gameplay's ERASE reached 4.4:1 (it needs 4.5).

Rounds 1–8 covered how to play, getting unstuck, not losing work, motion, size and colour, controllers and the
mouse alone. The biggest group the game still can't serve is **players who use only the keyboard**: no menu
reacts to the arrow keys or Enter, and nothing can be picked up or aimed without pointing. Round 9 is about them,
plus the two rough edges round 8 left for mouse-only players and the small-window gap above.

### A. Menus with the keyboard

- **Acceptance:** on every screen outside packing (title, main menu, settings, trip map, story, pause, postcard,
  album, credits), the arrow keys move a visible focus cursor to the nearest button, switch, slider, map pin or
  polaroid in that direction, holding repeats, and **Enter** (or Space) clicks what it's on. On a slider, switch or
  choice, ← / → change the value. While the keyboard cursor is on a control, Enter and Space click it instead of
  their usual screen-wide job (so Enter on the postcard's TRY AGAIN tries again, not "next trip"); with no control
  under it, they do what they did before. Esc is unchanged. Moving the mouse hands straight back (the cursor hides),
  and using a pad hands over to the pad. Key hints keep showing keyboard keys. Arrow keys being rebound to packing
  actions doesn't affect menus. Mouse and gamepad behave as before.
- **Verify:** autopilot `KeyMenuChecks` with keyboard events only: from the main menu, ↓ moves the cursor to another
  button and Enter opens it; in Settings → Audio the arrows reach the master volume and → / ← change it (restored);
  in the pause menu ↓ + Enter resumes; on the postcard ←/→ + Enter on TRY AGAIN retries; a mouse move hides the
  cursor. The existing `GamepadChecks` / `PadMenuChecks` pass unchanged. Screenshot.

### B. Pack with the keyboard alone (riskiest; lands only if it's clean)

- **Acceptance:** with empty hands, the arrow keys walk the packing list and the HUD buttons (HINT, UNDO, RESTART,
  CLOSE THE TRUNK) and Enter clicks them, so Enter on a row picks that thing up (from the blanket, or back out of
  the trunk, as a click on the row does now). Holding something, the arrow keys move the landing spot **one cell at
  a time** across the trunk, relative to the camera (↑ away, ↓ towards you, ← / → sideways), the ghost shows it as
  it does for the mouse, Enter drops it there (or says why it can't), and the existing keys turn, tip, roll, pick a
  shelf (W / S), see through (Tab), undo and close. Esc puts it back. Touching the mouse hands aiming straight back.
  An arrow key bound to a packing action keeps that job. Grandpa's tips and the README say how.
- **Verify:** autopilot `KeyboardOnlyChecks`: a whole small trip (Weekend Getaway, from the solver's solution) is
  packed and closed for three stars with **keyboard events only** (no mouse event at all), checking that each arrow
  step moves the ghost by exactly one cell in the camera-relative direction and that Enter on a packed row lifts it
  back out. The HUD layout, legibility and contrast passes stay clean. Screenshot.

### C. Mouse only: redo, and a steady hand over the key hints

Round 8's known limits: redo needs Shift + Z, and clicking a key hint moves the pointer off the trunk, so the held
thing drops down to the strip until you point back in (and the ghost doesn't follow the turn you just clicked).

- **Acceptance:** a **REDO** button sits with UNDO and shows only while there's something to redo; it does what
  Shift + Z does. While you hold something and the pointer is over the HUD, the held thing and its ghost stay at the
  spot you last aimed at, and turning, tipping or rolling with the key-hint buttons updates the ghost there; clicking
  the trunk still drops as before, and clicking off the trunk still puts it back. The HUD still fits every screen
  shape and interface size.
- **Verify:** `MouseOnlyChecks` additions with mouse events only: REDO hidden with nothing to redo, appears after an
  undo, a click redoes the same layout as Shift + Z; hold an item over the trunk, move to the TIP hint, click it: the
  ghost is still over the same column with the tipped shape, and a click back on the trunk drops it there. The
  mouse-only trip also makes one shelf choice with the **wheel** (owed since round 8). Layout pass at 16:9, 16:10,
  4:3, 21:9 × 80/100/120%.

### D. Readable at 1024×768

- **Acceptance:** at 1024×768 (the smallest window Settings offers on a 4:3 monitor) every text is 12 px or more and
  passes contrast on all 20 screens, and the HUD and menu layouts stay clean, without changing the round-7 contrast
  style that's waiting on the owner (no new outlines, no new or deeper inks). The smallest labels grow only as much as
  a small screen needs; at 1280×720 and up nothing changes size. 1024×768 joins the measured sizes in the README.
- **Verify:** layout-only runs at 1024×768 before (19 FAIL) and after (0 FAIL), and after at 1280×720, 1280×800,
  1600×900, 1200×900 and 2100×900. Before/after screenshots.

### E. Housekeeping: test windows off the shared desktop

- **Acceptance:** `Tools/play.sh` runs the player inside a private, headless nested KWin (`kwin_wayland --virtual`)
  whenever it's started in an automated mode (`-pttAutopilot`, `-pttBench`, `-pttShowcase`), so the autopilot,
  the benchmark, the crash test and the recorders never open a window on the desktop. Each flag stays its own word.
  `PTT_NESTED=0` opts out (a visible window, as before); without `kwin_wayland` it falls back to the old way.
  Normal play is unchanged. The crash test still SIGKILLs the player itself, not the compositor.
- **Verify:** the benchmark above, a quick autopilot and the crash test run nested, with the hardware renderer in
  the log (radeonsi), and no new window on the desktop.

Each item is built (`build-linux`, 0 errors) and tested on its own commit before the next starts: the solver, a quick
autopilot (load checked first) and screenshots in `docs/media/improvements/round9/`. The round ends with a full
autopilot (0 FAIL, real save untouched, load noted) and the crash test. If an item turns out bigger or riskier than
planned, the others land first and it's reported rather than half-landed.

## Round 9 results (2026-10-07)

All five scope items shipped. The final full autopilot (`Tools/autopilot.sh`, all 33 trips, run nested) had
**330 PASS, 0 FAIL** (317 before, plus 13 new checks), with no exceptions, no player crash, and the real save
untouched. It took 17 minutes, at load 17 at the start and 13 at the end. The crash test (`Tools/resume_test.sh`) passed 5/5. The solver proves all 33 levels.
Every item's commit was built on its own (`build-linux`, 0 errors, 0 warnings) and tested with a quick autopilot
before the next item was committed. The layout-only pass on the final build is **50/50 at all seven sizes**:
1024×768, 1280×720, 1280×800, 1440×900, 1600×900, 1200×900 and 2100×900 (load 7–11). `build-mac` wasn't re-run
(round 9 is platform-independent C#). Screenshots are in `docs/media/improvements/round9/`.

| Item | Commit | Verified by |
| --- | --- | --- |
| A/B benchmark, round 7 vs round 8 (owed) | `b76b397` | Both builds back to back, nested, at load 0.7–10: round 8 1.7–2.0 ms on every screen, round 7 1.8–2.4 ms (see the scope). |
| E. Test windows off the shared desktop | `5c0f985` | `play.sh` nests automated runs in `kwin_wayland --virtual`. The benchmark, a nested quick autopilot (229/229, renderer radeonsi in the log), the crash test (5/5, the SIGKILL hits the game's own PID) and every later run in this round ran nested; no test window opened on the desktop. The nested KWin used about 7% of one core. |
| D. Readable at 1024×768 | `392bb7e` | Layout-only at 1024×768 on round-8 `main`: 19 FAIL (text at 10.6–11.7 px on 18 screens, ERASE at 4.4:1). After: 50/50, and 50/50 at the five sizes measured before. `d-1024x768-packing-before-after.jpg`, `d-1024x768-map-before-after.jpg` |
| C. Mouse only: REDO and a steady hand | `83e61ac` | `MouseRedoChecks` (3), mouse events only. The Big Suitcase aimed at (1, 0, 1); a click on the tip hint keeps its ghost at (1, 0, 1), tipped (3, 1, 2) → (3, 2, 1) and still fitting, and a click back on the trunk drops it there. REDO is hidden on a fresh trip, shows after UNDO, and a click gives exactly Shift + Z's layout, then hides. **The wheel** (owed since round 8): over the skis on First Snow, one step down tucks a small thing into the gap under them and one step up puts it back on top. The layout pass measures the button row with REDO showing. Quick autopilot on this commit: 232/232. `c-mouse-only-steady-hand.jpg`, `c-mouse-only-redo.jpg` |
| A. Menus with the keyboard | `ca4f40d` | `KeyMenuChecks` (5), keyboard events only. An arrow shows the cursor (keyboard hints stay) and Enter on SETTINGS opens it; on the master volume slider → raises it 0.90 → 0.95 and ← lowers it back; the arrows reach RESUME and Enter resumes; on the postcard the arrows reach TRY AGAIN and Enter retries the wagon instead of going on; moving the mouse hides the cursor and hands back. The D-pad menu checks pass unchanged. Quick autopilot on this commit: 237/237. `a-keyboard-menu.jpg`, `a-keyboard-postcard.jpg` |
| B. Pack with the keyboard alone | `b86c20f` | `KeyboardOnlyChecks` (5). **Weekend Getaway packed 8/8 and closed for three stars with keyboard events only**: ↓ / ↑ to each row, Enter to pick up, R / T / F to turn it the solver's way, 16 arrow steps (each moved the ghost exactly one cell the way the camera faces, 16 of 16), W / S for the shelf, Enter to drop, Space to close. Enter on a packed row lifts it out and Escape puts it back in the same cell. While the arrows aim, the hint strip reads ARROWS move, ENTER grab / drop, W S shelf and fits on one line; moving the mouse brings back CLICK and WHEEL. Quick autopilot on this commit: 242/242. `b-keyboard-aiming.jpg`, `b-keyboard-packed.jpg` |

**Benchmark (`-pttBench`, 1600×900, High, nested), back to back with round-8 `main`** (`Recordings/round9/bench-ab-*.log`).
Round 9 read **1.7–2.2 ms on every screen** at load 6.4–8.0, with the minivan at 2.1 ms packing and 2.2 ms holding, 0 KB
allocated per frame and no frames over 33 ms. Round-8 `main`, built and run straight after at load 9.5–10.2, read 1.8–4.7 ms,
higher on the story, the wagon, the album and holding. Those are screens round 9 barely changes, so that's the busier machine,
not a cost of round 9. Round 9's new per-frame work is one float comparison (the text floor) and reading four arrow keys.

How the round differed from the plan:

- **Space doesn't click.** The scope said Enter "or Space" would click the control under the keyboard cursor. In practice
  that made the postcard's own "SPACE next trip" line untrue while the cursor was showing, so only **Enter** clicks, and
  Space keeps its jobs (skip texts, start packing, next trip, close the trunk) everywhere.
- **↑ / ↓ only aim once aiming has started.** They were already spare shelf keys (beside W / S) for mouse players. So
  keyboard aiming starts when something is picked up with Enter, or with ← / →, and only then do ↑ / ↓ move instead of
  picking a shelf. An arrow key bound to a packing action keeps that job.
- **The hint strip follows the keyboard.** This wasn't in the scope. While the arrows aim, the strip says ARROWS move, ENTER
  grab / drop and W S shelf instead of CLICK and WHEEL, because a keyboard-only player would otherwise be told to click.
- **The 12 px floor is measured at the default interface size.** The first version used the chosen size, which made 80%
  at 1024×768 grow its labels to 28 units and pushed the counts line off the list. Picking a smaller interface size now
  shrinks everything evenly, as before.
- **REDO and the now-playing cassette.** The cassette assumed the button row was always 546 units wide, so with REDO
  showing it overlapped the row at 1600×900. It now uses the row's real width.

Things round 9 turned up along the way:

- **A nested KWin is a better place to measure.** The first nested benchmark was the tightest yet (p99 2.3–4.2 ms), and the
  desktop compositor's throttling of covered windows can't affect it. Earlier rounds' numbers came from desktop windows,
  so compare nested runs with nested runs.
- **The commits were split after building.** C, A and B were written together, then split into three stages with a script
  (`Recordings/round9/split.py`). Each stage was built and quick-tested on its own before it was committed, so unlike
  round 7 every intermediate commit has been compiled and run.
- **`[Audio]`:** 2.5 dBFS before the limiter and −1.5 dBFS after (round 8: 2.8 / −1.5).

Known limits:

- Keyboard-only play has only been driven by the self-test. The cursor picks the nearest control by geometry, like
  the D-pad. With the key hints switched off, nothing on screen mentions the arrows except Grandpa's first tip.
- The keyboard can't orbit the camera with the arrows (Q / E do that, as before) and can't drag.
- Windows narrower than 1024 pixels aren't measured. Settings doesn't offer them, but a resized window can be that small.
- The scroll wheel is now covered by its own check. The full mouse-only trip still doesn't need it.

Still open after round 9:

- **Owner decisions:** the round-7 contrast look (outlined labels, deeper orange), Windows (needs the module), WebGL and
  hosting, signing and notarization, licences, releases and re-cutting the trailer (still the v0.1.0 cut). Also whether
  automated runs should stay nested by default (`PTT_NESTED=0` restores a visible window).
- **Needs hardware or people:** a physical controller and Steam Deck test (`docs/GAMEPAD-TEST.md`), a Mac run, and someone
  who plays with the keyboard alone.
- **Bigger ideas:** replayability from ranked #12 (per-trip challenges, a solver-generated Garage Sale mode).

## Round 10 scope (2026-10-07, branch `improvements-10`)

**Baseline.** Round-9 `main` (`2529f85`) was rebuilt (0 errors) and benchmarked nested at the start of the round
(`-pttBench`, 1600×900, High; `Recordings/round10/bench-main.log`). Load went from 3 to 11 during the run as other
sessions started. Every screen averaged **1.9–2.9 ms**, and the 25-item minivan was **2.3 ms** packing and **3.4 ms**
holding (p99 7.0). There were no frames over 33 ms and 0 KB allocated per frame.

**A first look below 1024×768.** Settings stops at 1024 wide, but a window can be dragged smaller. A layout-only run
at 800×600 on round-9 `main` (`Recordings/round10/layout-800x600-before`) kept the menus and most of the HUD clean,
but **9 checks failed**. At 80% and 100% the packing list's counts line doesn't fit on one line. The trip map's seal
card is at 9.6 px on all six pages, and the postcard's counts line and seal text are at 9.6–10.1 px; all of these
set their size inside the text (`<size=…>`), which the 12 px floor doesn't reach. The postcard's title drops to
4.0:1, because at that size it no longer counts as large text.

Rounds 1–9 covered how to play, getting unstuck, not losing work, motion, size and colour, controllers, the mouse
alone and the keyboard alone. Round 10 picks up the oldest open idea on the ranked list, **a reason to keep
playing once the story's done** (ranked #12, deferred since round 1), plus two smaller gaps: the keyboard-only
controls are written down nowhere a player can look them up, and windows below 1024×768.

### A. The pause card lists the keyboard-only controls

Round 9 made the whole game playable with the arrow keys and Enter, but the pause card's HOW TO PACK list (the
one place that lists every control) starts with CLICK and never mentions them. With the key hints turned off,
only Grandpa's first tip does.

- **Acceptance:** the pause card's keyboard list has an **ARROWS · ENTER** row (walk the list and the buttons, aim
  one square at a time, pick up and drop). Every row still fits inside the card at every interface size, and the
  pad list is unchanged. Legibility and contrast stay clean.
- **Verify:** an autopilot check opens the pause menu, finds the row, and checks that every row of the keyboard
  list sits inside the card at 80/100/120%. Layout-only runs at 1024×768 and 1600×900. Screenshot.

### B. Readable in an 800×600 window

- **Acceptance:** at 800×600 every text is 12 px or more and passes contrast on all 20 screens, and the HUD and menu
  layouts stay clean at 80/100/120%. Sizes set inside a text (`<size=…>`) get the same floor as the text itself.
  Nothing changes size at 1280×720 and up, and the round-7 contrast style (waiting on the owner) is unchanged.
  800×600 joins the measured sizes in the README; smaller windows stay unmeasured.
- **Verify:** layout-only runs at 800×600 before (9 FAIL) and after (0 FAIL), and after at 1024×768, 1280×720 and
  1600×900. Before/after screenshots.

### C. Favours for the neighbours (ranked #12)

Once the 33 trips are packed, the only thing left is replaying them for a seal, and CONTINUE replays the finale.
A packing puzzle can make new puzzles. Here a new pile is built by packing it under the game's own rules, so a
complete packing of every new pile exists by construction, and Grandpa can hint it.

- **Acceptance:**
  - **Where it is.** The trip map gets a seventh page, **The Neighbours**. It unlocks once chapter II is packed
    (trip 9); before that the page says what unlocks it. It shows the neighbour who needs a hand now (a pin and a
    label: who, which car, and "PACKED, WAITING" if you left it half-packed) and a line of totals (favours done,
    how many with three stars).
  - **The puzzle.** A favour borrows the car of a trip you've closed (trunks of 24 cells or more). Its pile comes
    only from things you've already packed in the story, so there are no spoilers. The game builds the pile by
    packing it into that trunk under the game's rules: 6 to 22 things filling 82–95% of the trunk, the biggest
    of them (about 60% of the volume) essentials and the rest extras. That packing is what Ask Grandpa hints. A
    favour is saved once it's made, so leaving, quitting or a crash brings back the same pile, and a half-packed
    favour waits like a trip does (undo history and all).
  - **Playing it.** The neighbour texts you first (a short note from a small cast of neighbours). Stars work as
    on any trip. A favour takes no album photo, gives no seal, and doesn't change the trip counts on the title or
    the map. The postcard's TRY AGAIN replays the same favour, and NEXT FAVOUR makes a new one. Erasing progress
    clears the favours.
  - **After the story.** Once every trip has stars, the title's CONTINUE goes to the neighbours' current favour
    instead of the last trip.
- **Verify:**
  - Autopilot `FavourChecks`, generator: hundreds of favours made across every eligible car. Each one is checked
    against the game's `TrunkGrid` rules: in range for size and fill, drawn only from closed trips, made in a few
    milliseconds. They're written to a file that `solve_levels.py --check-favours` checks again with the Python
    solver's own rules.
  - Through the real UI: the page is locked before chapter II and opens after; clicking the pin goes to the texts,
    then packing; the favour packs to 100% by following hints only and closes for three stars, with no seal and no
    polaroid; NEXT FAVOUR gives a different pile; leaving half-packed through the pause menu and coming back restores
    it; TRY AGAIN gives the same pile; with every trip packed, CONTINUE opens the favour.
  - The layout, legibility and contrast passes cover the neighbours page and a favour's texts, HUD and postcard.
    The existing checks pass unchanged.
- This is the riskiest item, so it's built last. If it can't land cleanly, A and B ship and it's reported instead.

### D. Housekeeping

`build-mac` is re-run (last run in round 7) and checked with `file`. The round ends with `build-linux` (0 errors),
the solver, a full autopilot (0 FAIL, real save untouched, load noted), the crash test, and a nested `-pttBench`
compared with the baseline above. Each item is built and quick-tested on its own commit before the next starts,
with screenshots in `docs/media/improvements/round10/`.

## Round 10 results (2026-10-07)

All three scope items shipped. Item B grew once its new checks started finding more than the 9 FAILs the scope
counted. The final full autopilot (`Tools/autopilot.sh`, all 33 trips, nested) had **378 PASS, 0 FAIL**
(330 before; `Recordings/round10/full.out`), with no exceptions, no player crash and the real save untouched. It took 19 minutes, at load 23 at the start and 51 at the end (other sessions were busy).
The crash test (`Tools/resume_test.sh`) passed 5/5. The solver proves all 33 levels (`Recordings/round10/solver.log`). Each item's commit was built on its
own (`build-linux`, 0 errors, 0 warnings) and tested on that build before the next item was committed: A and B with
layout-only runs, C with a quick autopilot. `build-mac` gives a universal (x86_64 + arm64, checked with `file`) 160 MB `.app` with 0 errors; it still hasn't been run on a Mac. The layout-only pass on the final build is **88/88 at all eight sizes** (load 6–20; `Recordings/round10/final-layout-*`). Screenshots are in
`docs/media/improvements/round10/`.

| Item | Commit | Verified by |
| --- | --- | --- |
| Baseline benchmark | `8cf0d3e` | `-pttBench` on round-9 `main`, nested, at load 3 → 11: every screen 1.9–2.9 ms, the minivan 2.3 ms packing / 3.4 ms holding, 0 KB per frame (see the scope). |
| A. The pause card lists the keyboard-only controls | `a65178f` | The layout pass now pauses at 80/100/120% and checks the HOW TO PACK list. It has 9 keyboard rows including ARROWS · ENTER (ESC and the music key share a row), and every row sits inside the card. Layout-only runs on this commit were 53/53 at 1600×900 and at 1024×768 (load 20–27), and the check passed in every later run at all eight sizes. `a-pause-keyboard-rows.jpg` |
| B. Readable in an 800×600 window | `352f791` | Layout-only at 800×600 on round-9 `main`: 9 FAIL. Three new checks went in: no word broken across lines, no text that wraps onto lines it wasn't written with and is taller than its box, and no packing-list name running into its stamps. With them, the first after-run still had 7 FAIL at 800×600 (names into stamps at 80/100%, the widened list over Grandpa's tip, the luggage tag, the Controls tab's pad line, the seal note, polaroid captions and placeholders, the close-up's date line). At 1600×900 they flagged two texts that look fine (the album's two written lines, and the Controls hint at 4 units over), which is where the check's slack of a third of a line comes from. Final: **73/73 at all eight sizes** (800×600, 1024×768, 1280×720, 1280×800, 1440×900, 1600×900, 1200×900, 2100×900; load 19–25; `Recordings/round10/b4-layout-*`). `b-800x600-packing-before-after.jpg`, `b-800x600-postcard-map-before-after.jpg` |
| C. Favours for the neighbours | `15e669d`, `5710e46` | Quick autopilot on this commit: 290/290 (load 21, `Recordings/round10/c1-quick.out`). `FavourChecks` (10), through the real UI. The page is locked before trip 9 and says what opens it. Once open, its pin opens the neighbour's texts and then packing. Following only hints packs favour 1 to 100% (18 of 18). Closing it gives three stars, no seal and no album photo, and counts it once. NEXT FAVOUR gives favour 2, a different car and pile. Leaving favour 2 with 3 packed through the pause menu, the page says "3 PACKED, WAITING" and the pin restores the same 3 cells. TRY AGAIN replays favour 2 fresh without counting it twice. With every trip packed, the title's CONTINUE opens favour 3. The reset clears everything. The maker made 80 favours in the quick run (7 cars, 3.9 ms average, 38 ms at most) and 300 in the full run (all 28 cars with 24 or more cells, 3.6 ms on average, 15.9 ms at most); every one was re-placed with `TrunkGrid.Check` in the order it was made, and `solve_levels.py --check-favours` passed all of them with its own rules (`check-favours.log`). The legibility pass now measures the neighbours page (locked and open) and a favour's texts, HUD and postcard. `c-*.jpg` |

**Benchmark (`-pttBench`, 1600×900, High, nested)** on the final build at load 4 → 8 (`Recordings/round10/bench-after.log`):
every screen **1.9–2.3 ms**, the minivan 2.2 ms packing and 2.3 ms holding (p99 3.7 ms), 0 KB allocated per frame and no
frames over 33 ms. Round-9 `main` read 1.9–2.9 ms at load 3 → 11 at the start of the round, so round 10 costs nothing
measurable. The benchmark doesn't open the neighbours page. Making a favour takes about 4 ms (at most 33 ms in the full
run, and 72 ms for the first one of a session, while the shapes' turns are worked out). It happens when the map or the
title opens (behind the transition), and when a favour's postcard comes up, where it can cost one frame of up to
about 30 ms.

How the round differed from the plan:

- **B found more than the scope said.** The 9 FAILs in the scope were only what the old checks could see. The new
  wrap and crowding checks are the main result: the list widening, the shortened lines and the taller boxes all
  came from them.
- **The packing list widens a little at 80% too.** The scope said nothing would change size at 1280×720 and up.
  That holds at the default interface size: no `[Layout]` line at any size from 1280×720 up. Picking 80% at
  1200×900, 1280×720, 1280×800 or 1440×900 widens the list by 11–58 units, because the list keeps its names at
  12 px even there, and a long name beside two stamps had nowhere left to go. At 1024×768 it widens by 9–107
  units, depending on the interface size. The game logs each widening as `[Layout]`.
- **Shorter lines rather than smaller text.** At 800×600 the map note's CLICK line now reads "pick something up,
  drop it in the trunk" at every size. On small windows only, the luggage tag drops the car and then the month,
  the postcard's seal note says "Not a single hint.", and a polaroid without a photo leaves out its car name if
  a word of it can't fit.
- **A waiting trunk comes before the neighbours.** The first full run (`Recordings/round10/full-1.out`, 374 PASS) failed
  4 resume checks. With every trip
  packed, CONTINUE went to the neighbours even though the last trip's trunk was half-packed and the title said
  so. Now CONTINUE goes to the neighbours only when no trip's trunk is waiting.
- **The map opens on the neighbours page only after a favour.** Opening there whenever every trip is packed would
  have hidden the chapter pages (and their seals) behind it. With every trip packed, CONTINUE already goes to the
  neighbours.

Things round 10 turned up along the way:

- **A layout bug caught by the layout pass.** The first version widened the list by moving its left edge while
  the list was still sliding in, which stretched it (the star meter ran 13 units into the heading at 1600×900,
  100%). It now resizes by width, and the slide-in lands on the new size.
- **A pale avatar.** The phone avatar's colour comes from a hash of the sender's name, and "Coach Dana" landed on
  a pale yellow with a white letter (1.5:1). A letter on a pale avatar is now ink. The story's own senders, which
  passed before, are unchanged.
- **Commits.** B was committed from the source it was built and tested from (a copy is in
  `Recordings/round10/b-src`), while its eight-size verification ran on that build. C's work was stashed while B
  was built and tested on its own.
- **`autopilot.sh`'s player timeout is now 30 minutes.** The full run took 17 minutes in round 9, and the
  favour checks add about two more.
- **`[Audio]`:** 2.5 dBFS before the limiter and −1.5 dBFS after (round 9: 2.5 / −1.5).

Known limits:

- **Favours haven't been played by a person.** The piles come from a greedy packer that fills 82–95% of the trunk,
  so every one can be packed completely, but nobody has judged how hard or how fun they are. The words come from a
  small cast: 10 neighbours, 12 errands and 4 sign-offs, so they repeat. One favour waits at a time. Favours have
  no album, no seal and no best-so-far line. The first favour of a session takes up to about 70 ms to make while
  the shapes' turns are worked out, and that happens behind the map's transition.
- Windows smaller than 800×600 aren't measured. A text the 12 px floor changes is sized again the next time its
  screen opens, not while it's showing.
- The wrap check reads line breaks only in texts without markup, and its "taller than its box" test allows a third
  of a line of slack.

Still open after round 10:

- **Owner decisions:** the round-7 contrast look (outlined labels, deeper orange), Windows (needs the module),
  WebGL and hosting, signing and notarization, licences, releases and version tags, and re-cutting the trailer
  (still the v0.1.0 cut; it doesn't show favours either). Also whether favours should get their own album page or
  seals.
- **Needs hardware or people:** a physical controller and Steam Deck test (`docs/GAMEPAD-TEST.md`), a Mac run,
  someone who plays with the keyboard alone, and a few people playing favours.

## Round 11 scope (2026-10-08, branch `improvements-11`)

**Baseline.** Round-10 `main` (`0bba573`) was rebuilt (0 errors) and benchmarked nested at the start of the round
(`-pttBench`, 1600×900, High; `Recordings/round11/bench-main.log`). Load was 13–14 (other sessions were building), so
frame times read a flat 10–12 ms on every screen (GPU contention, as in earlier rounds), while the CPU column read
**2.0–2.9 ms**, with 0 KB allocated per frame. The round ends with a back-to-back A/B instead of comparing with this.

**A first look at the favours as a player meets them.** Round 10 shipped favours checked only by machine. Reading the
300 favours its full self-test made (`Recordings/round10/full/favours.json`) the way a player would meet them:

- **The neighbours are carrying the family's things.** A favour's pile is drawn from everything packed in the story,
  so the first one in that file asks you to pack **Biscuit (in Carrier)** (the family's cat), **Grandma's Veil** and
  **Sam's Turntable** for a neighbour. Grandpa's Note, Mr. Buttons, the Ring and the Portrait of Grandpa can turn up
  the same way: 259 of the 300 piles have at least one of the family's things. In a game about one family's
  heirlooms that reads as a bug.
- **The words don't follow the pile.** The errand is picked from the favour's number alone, so "The Beach Trip" can be
  a moving truck of baby things. With 10 neighbours, 12 errands and 4 sign-offs, they also repeat soon.
- **Most piles have exactly three fragile things** (the maker's cap): 170 of the 300. The story's trips run
  from none to ten.
- **There's no way to turn a favour down.** One favour waits at a time; if it's a 22-thing moving truck when you wanted
  a quick one, the only way on is to close it.
- **Closing a favour can hitch the postcard.** The next favour is made on the frame the postcard comes up (about 4 ms,
  up to ~30 ms), and the first one in a session takes ~70 ms while every shape's turns are worked out.
- **How hard are they?** By the solver's own effort to find a complete packing from scratch, favours sit where the
  story's trips do (most take under 50 search steps, like most trips; a few take tens of thousands, like Grandma's Big
  Move or Wedding Day). Fill (82–95%) and size (6–22 things) match the story's range too. That says nothing about fun;
  it only says they aren't wildly off. (`Recordings/round11/difficulty-r10.txt`)

Round 11 is about those: favours that read like the neighbours' own errands, a choice of which one to do, and no hitch.

### A. The neighbours pack their own things, and the words fit the pile

- **Acceptance:**
  - The family's own things never appear in a favour: items marked `"family": true` in `PackTheTrunkData.json` (Mr.
    Buttons, Grandpa's guitar, the grandfather clock, the gnome, Grandpa's note, the portrait, the ring, the veil, Sam's
    turntable and armchair, Grandma's armchair, Biscuit, Rosie's suitcase, Grandpa's sled, the cutout of you, Dad's
    grill, the second clown, the IT'S A GIRL balloons and the photo album). The page still unlocks after chapter II and
    every favour is still 6–22 things at 82–95% fill.
  - The errand fits the pile: errands carry the things that suggest them (a beach trip wants surfboards and umbrellas,
    a baby shower wants a stroller), and a pile with at least two of an errand's things gets that errand; otherwise
    it's one that fits anything (a yard sale, moving day). One of the texts names something in the pile (and a fragile
    thing's line reminds you nothing goes on top of it).
  - Less repetition: a bigger cast (16 neighbours, 20+ errands, 8 sign-offs), and the neighbours take turns, so the
    same neighbour never asks twice in a row.
  - Piles vary: each favour allows one to four fragile things instead of always up to three.
  - A favour saved by round 10 keeps its pile; only its words follow the new rules.
- **Verify:** the generator check (`FavourMakerChecks`, 80 favours in the quick run, 300 in the full one) also checks
  that no pile has a family thing, counts how many favours got a matching errand, how many neighbours and errands
  appear, the longest run without a repeated neighbour, the spread of fragile counts, and that every text line names
  only things in that pile. `solve_levels.py --check-favours` also rejects a family thing. The legibility, wrap and
  contrast passes cover a favour's texts as before. Screenshots of a favour's texts.

### B. Ask someone else

- **Acceptance:** the neighbours page has an **ASK SOMEONE ELSE** button under the waiting favour. It replaces the
  waiting favour with another neighbour, another car (when there's a choice) and a new pile, with the same favour
  number. If that favour's trunk is half-packed, the first click asks ("UNPACK IT?") and only a second click within a
  few seconds swaps it (and forgets that trunk). It works with the mouse, the keyboard cursor and the pad's D-pad like
  any other button. A swapped favour is saved like any other, so quitting or a crash brings back the new one. The
  counts (favours done, three stars) don't change.
- **Verify:** `FavourChecks` additions through the real UI: on the page, a click swaps favour 1 for a different
  neighbour, car and pile with the same number; a second swap differs again; the page shows the new neighbour; with 3
  packed, the first click only asks and the waiting trunk survives, the second swaps and the trunk is gone; the new
  favour opens from the pin; Enter on the button through the keyboard cursor swaps too. Layout, legibility and
  contrast passes measure the page with the button at every size. Screenshot.

### C. No hitch when the next favour is made

- **Acceptance:** closing a favour costs the postcard frame no more than a millisecond or so of favour work: the next
  favour is made on a worker thread while the postcard shows (and saved on the main thread when it's ready), or, if the
  game is quit first, made the next time it's needed, behind a transition. Every shape's turns are worked out on a
  worker at boot, so the first favour of a session costs the same as any other. Nothing changes about which favour
  comes next.
- **Verify:** the game logs the main-thread cost of closing a favour (`[Favours] closing favour N took X ms on the main
  thread`) and where the next one was made; the favour checks require it under 2 ms, and that the next favour is
  ready (saved) by the time the postcard's buttons can be clicked. Quick autopilot before (round-10 `main`'s
  `[Favours] made` lines at the postcard) and after.

### D. Housekeeping

The round ends with `build-linux` (0 errors), the solver, a full autopilot (0 FAIL, real save untouched, load noted),
the crash test, layout-only runs at the eight measured sizes, and a nested back-to-back `-pttBench` A/B with round-10
`main`. Each item is built and quick-tested on its own commit before the next starts, with screenshots in
`docs/media/improvements/round11/`. After each nested run, the helper processes (`ksecretd`, portals, D-Bus) are
counted to make sure the run left none behind.

Not in this round: playing favours with people (still the real test of their difficulty and fun), album pages or
seals for favours (owner), re-laying out a screen live while its window is being resized (texts already follow the
window; the packing list's widening and a few shortened lines wait for the screen to open again).

## Round 11 results (2026-10-08)

All three scope items shipped. The final full autopilot (`Tools/autopilot.sh`, all 33 trips, nested) on `2f496c2` had
**384 PASS, 0 FAIL** (378 in round 10, plus 6 new checks; 19 minutes, load 15 at the start and 11 at the end) (`Recordings/round11/final-full.out`), with no exceptions, no player crash and the real save untouched.
The crash test (`Tools/resume_test.sh`) passed 5/5 (`Recordings/round11/resume.out`) and the solver proves all 33
levels (`Recordings/round11/solver.log`). Each item's commit was built on its own (`build-linux`, 0 errors, 0 warnings)
and quick-tested on that build before the next was applied. The layout-only pass on the final build is **89/89 at all eight sizes** (800×600, 1024×768, 1280×720, 1280×800, 1440×900, 1600×900, 1200×900, 2100×900; load 8–16)
(`Recordings/round11/final-layout-*`). `build-mac` wasn't re-run (round 11 is platform-independent C# and data).
Screenshots are in `docs/media/improvements/round11/`.

| Item | Commit | Verified by |
| --- | --- | --- |
| Baseline benchmark | `f07c4f8` | `-pttBench` on round-10 `main`, nested, at load 13–14 (see the scope). |
| A. The neighbours pack their own things, and the words fit the pile | `cb89b5f` | Round 10's own 300 favours, checked with the new rule: `solve_levels.py --check-favours` rejects **265 of 300** for a family thing (`a-check-favours-round10-file.log`). Quick autopilot on this commit: **291/291** (load 23 at the start, 77 at the end; `a2-quick.out`). The generator check made 80 favours from the first 10 trips: none with a family thing, all packing completely (and 80/80 with the solver's rules); 63 got an errand that suits the pile, 15 different errands, all 16 neighbours taking turns (no neighbour again within 16 favours), every third text names something in its pile, and no held card names the family. Fragile things per pile: 0: 11, 1: 35, 2: 17, 3: 12, 4: 5. In the full run (all 33 trips closed, 300 favours in all 28 cars): 196 suited errands, 25 different errands, fragile 0: 23, 1: 118, 2: 100, 3: 46, 4: 13, and `--check-favours` 300/300. `a-favour-texts-fit-the-pile.jpg`, `a-favour-packing.jpg` |
| B. Ask someone else | `8f5bef3` | Quick autopilot on this commit: **295/295** (load 20 → 19; `b-quick.out`). Through the real UI: two clicks swap favour 1 from Coach Dana's pickup (17 things) to Mr. Pickering's station wagon (12), then Mr. Haskins's pickup (19); same number, the page and the save follow, nothing counted. On favour 3 with 3 packed, the first click only asks ("UNPACK AND ASK?") and keeps the trunk, the ask lapses after 3 s, and a second click within it unpacks and asks The Nguyens instead of Mrs. Alvarez, with the done count unchanged. The keyboard cursor reaches the button (↓ walked Prev Page, then the pin, then the button) and Enter asks someone else. A new layout check puts the button inside the page and clear of the pin, its card and the note, at each window size. `b-ask-someone-else.jpg`, `b-unpack-and-ask.jpg` |
| C. No hitch when the next favour is made | `8bb4f39` | Quick autopilot on this commit: **296/296** (load 23 → 15; `c-quick.out`). Closing favour 1 cost the main thread **1.82 ms**, and later closes 0.4–0.8 ms; the next favour was made on a worker in 16–19 ms while the postcard showed, and was saved before the postcard's buttons could be clicked. Every shape's 646 turns are worked out on a worker at boot (45–56 ms). Before, on round-10 `main`, the next favour was made on the postcard's frame (4.2–6.5 ms in round 10's logs) and the first favour of a session took 33 ms. |
| Self-test timing fix | `2f496c2` | See below. Layout-only at 1600×900 on this build: 89/89 (`layout2-1600x900.out`). |

**Benchmark (`-pttBench`, 1600×900, High, nested), back to back with round-10 `main`** (`Recordings/round11/bench-after.log`,
`bench-main-ab.log`), at load 14–16 with other sessions busy, so frame times are GPU contention: round 11 read 5.1–12.4 ms on
every screen and round-10 `main` straight after it 10.5–16.0 ms. The CPU column, which contention touches less, read 1.9–2.9 ms
for round 11 and 2.2–3.3 ms for `main`. Both allocated 0 KB per frame. Round 11 costs nothing measurable; its only per-frame
addition is one null check for a favour being made.

How the round differed from the plan:

- **An errand needs three of its things, not two.** With two, 78 of 80 favours got a themed errand (almost any pile has two
  things from one of 16 lists) and the errands that fit anything nearly never came up. With three it's 63 of 80 in the quick
  run and 196 of 300 in the full one.
- **The legibility pass measured a favour's story too early.** Four texts instead of three bring LET'S PACK in later than
  the pass's fixed 7-second wait, so it measured the button's "or press SPACE" while it was still scaling in: 10.1 px at
  1600×900 in one layout run (FAIL), 12.7 px in the first full run, where the contrast check also read it at 3.9:1 (FAIL)
  while it was still sliding in. The pass now waits for the button. Settled, on the same favour (Coach Dana's sedan), it's
  15.0 px and every text on that screen passes contrast (lowest 5.4:1).
  The first full run on `8bb4f39` (`Recordings/round11/full.out`: 383 PASS, that 1 FAIL) and its layout runs
  (`layout-*.out`: 89/89 at seven sizes, 88/89 at 1600×900) are kept.
- **A test-order bug from round 10.** The first quick run on A failed one check: with every trip packed, CONTINUE opened the
  finale instead of the neighbours, because the quick run's earlier sections leave the finale half-packed, and a waiting trip
  rightly comes first (round 10's own rule; round 10's final full run doesn't leave one). The favour check now clears that
  trunk first.
- **A small fix that came with the words.** Grandpa's hints said "the The Kitchen Sink", "the The Ring" and "the Mr. Buttons".
  Names now get "the" only where it reads right.
- **The neighbours page is measured at the default interface size.** The new button's layout check runs in the legibility
  pass, at every window size, at the interface size each window starts with; 80% and 120% aren't measured on that page.

Things round 11 turned up along the way:

- **Family descriptions.** Many things' held-card lines name the family ("Dad's.", "Uncle Rick named it Bruce."), so 34 things
  got a second, neighbour line (`neighbourDesc` in the data), and the self-test checks no held card in a favour names the family.
- **Helpers.** After each nested run the `ksecretd` and portal counts were checked. None of this round's runs left one: the
  new ones that appeared during the round belong to another session's private D-Bus.
- **`[Audio]`:** 2.9 dBFS before the limiter and −1.5 dBFS after (round 10: 2.5 / −1.5).

Known limits:

- **Favours still haven't been played by a person.** Their difficulty by the solver's effort is in line with the story's trips
  (`Recordings/round11/difficulty-r10.txt`), but whether they're fun is untested. The words repeat over a long run (16
  neighbours, 26 errands, 8 sign-offs), a pile can still be an odd mix (a camp trip with two kitchen sinks), and a pile line
  can read a little stiffly ("the Last Year's Trophy").
- ASK SOMEONE ELSE was driven by the mouse and the keyboard cursor; the pad's D-pad uses the same cursor geometry but wasn't
  run against it.
- ASK SOMEONE ELSE avoids only the car you're swapping away from, so two swaps can come back to the same car. After seven
  swaps of one favour, its neighbour can match the next favour's.
- The main-thread cost of closing a favour is measured on the sandboxed save, so it leaves out writing the save file to disk
  (which closing any trip does).
- A text the 12 px floor changes is still sized again only when its screen opens, not while a window is being resized.

Still open after round 11:

- **Owner decisions:** the round-7 contrast look, Windows (needs the module), WebGL and hosting, signing and notarization,
  licences, releases and version tags, re-cutting the trailer (still v0.1.0; no favours in it), and album pages or seals for
  favours.
- **Needs hardware or people:** a physical controller and Steam Deck test (`docs/GAMEPAD-TEST.md`), a Mac run, someone who plays
  with the keyboard alone, and a few people playing favours.

## Round 12 scope (2026-10-08, branch `improvements-12`)

**Focus: AAA polish and a graphics fidelity slider.** The owner wants the game to look and feel like a polished release.
Earlier rounds kept the look off-limits; this round may raise its quality as long as the style (toon shading, ink outlines,
paper-craft UI, the round-7 contrast look) stays.

**Baseline.** Round-11 `main` (`59fd05e`) was rebuilt (0 errors) and benchmarked nested with `-pttBench -pttBenchPreset n`
for each of today's four presets, back to back (`Recordings/round12/bench-main-p0..3.log`). Load rose from 2.5 to 15.6 during
the four runs as other sessions started, so these are a guide, not the comparison: the round ends with the fidelity table
measured in one process (below). Average frame times (ms) on the busiest screens:

| Preset (today) | Title | Minivan packing | Minivan, half packed, holding | Load |
| --- | --- | --- | --- | --- |
| Low | 1.1 | 2.8 | 2.8 | 2.5 |
| Medium | 11.3 (contention spike) | 2.0 | 1.8 | 8.8 |
| High (default) | 3.6 | 2.3 | 2.3 | 14.8 |
| Ultra | 3.0 | 3.8 | 3.8 | 15.6 |

**What a player sees today.** Reading the README stills and the round-11 self-test screenshots as a player would:

- **The ground is most of every frame, and it's flat.** The driveway is one untextured mauve-grey box with a faint grain;
  the lawn is one flat green. Everything else (cars, items, blanket, props) is modelled and shaded; the surface they sit on
  reads as a placeholder.
- **Ultra isn't much more than High.** Today's Ultra preset is High plus 125% render scale and a longer (so blurrier)
  shadow distance. Settings has a "Quality preset" chooser (Low / Medium / High / Ultra / Custom) above seven fine-tune rows.
- **The main menu sits in a dark box.** Round 7's scrim behind the menu (for text contrast) has short soft edges, so on the
  title it reads as a translucent rectangle with visible corners.
- Owed from round 11: ASK SOMEONE ELSE was driven by the mouse and the keyboard cursor, not the pad's D-pad.

### A. Graphics fidelity slider (required)

- **Acceptance:**
  - The Graphics tab's "Quality preset" chooser grows into one **Graphics fidelity** slider with four steps: **Low, Medium,
    High, Ultra** (no second control). It snaps to steps, shows the step's name, works by mouse (click or drag), keyboard
    (arrows on the row) and pad (D-pad left / right), and is saved with the other settings. The fine-tune rows stay below
    it; changing one shows "Custom" on the slider, which keeps the step's own detail settings. DEFAULTS puts it back to High.
    A save from round 11 keeps its choice (its preset becomes the step; a Custom save keeps its rows on High).
  - **High (the default) stays today's look and cost.**
  - **Ultra** goes well past today: 150% render scale (supersampled), an 8192 sun shadow map, 12-sample ambient occlusion
    (High uses 8), bokeh depth of field, high-quality bloom filtering, a blanket texture at twice the texel density with
    16x anisotropic filtering, and 60% more particles (landing dust, sparkles, confetti).
  - **Medium** is today's Medium with half-resolution ambient occlusion added; **Low** is today's Low (75% render scale,
    FXAA, low shadows, no AO / bloom / depth of field) plus half the particles and the cheapest surface shading, so it stays
    smooth on weak hardware.
  - Shader variants that Ultra needs (12-sample AO) are kept in the build through URP's "include assets by label" setting,
    not a second quality level; the build size is reported.
- **Verify:** a new benchmark mode (`-pttBench -pttFidelity <dir>`) holds the same two scenes (the title and the half-packed
  minivan while holding something) at each step in one process, logs `[Perf]` for each and saves a screenshot of each
  after measuring, so the table compares like with like (load noted). The full `-pttBench` also runs at each step. A new
  self-test section (`FidelityChecks`) drives the slider through the real UI with the mouse, the arrow keys and the
  simulated pad, checks what each step applied (render scale, MSAA, shadow map size, AO samples, DOF mode, particle
  density), that a fine-tune change reads Custom, that DEFAULTS restores High, and that the setting survives a save. The
  results get a table of the steps, what each changes and its frame time, with the four screenshots side by side.

### B. A street that looks finished

- **Acceptance:** the driveway gets an asphalt surface (fine aggregate that fades with distance, darker resurfaced patches,
  a few tar-sealed cracks), the lawn gets mowing stripes and clumps, and the curbs get joints, all procedural in the toon
  shader (no new textures or downloaded assets). The palette and the overall brightness under the HUD stay the same, so
  every text keeps its measured contrast. On Low the surfaces fall back to today's flat grain.
- **Verify:** before/after stills of the same frames (title, packing, the minivan), the self-test's legibility and
  contrast passes at every window size (no new FAIL), and the benchmark's cost at High.

### C. The main menu without a box

- **Acceptance:** the title menu's scrim becomes a wide, soft falloff with no visible edges or corners, still dark enough
  behind every caption that the contrast check passes (WCAG 4.5:1); the pause menu's shade matches it.
- **Verify:** before/after stills; the contrast pass measures the main menu at every size as before.

### D. Owed from round 11: ASK SOMEONE ELSE with the pad

- **Acceptance / verify:** a `FavourChecks` addition walks the simulated pad's D-pad to ASK SOMEONE ELSE on the neighbours
  page and presses A, and the favour changes neighbour.

### E. Housekeeping

The round ends with `build-linux` (0 errors), the solver, a full autopilot (0 FAIL, real save untouched, load noted), the
crash test, layout-only runs at the eight measured sizes, the fidelity table, and a `-pttBench` at High back to back with
round-11 `main`. Each item is built and quick-tested on its own commit before the next. Screenshots go to
`docs/media/improvements/round12/`. Helper processes are counted after nested runs.

Not in this round: re-cutting the trailer or re-shooting the README gallery (owner's call, and the trailer still shows v0.1.0),
Windows / WebGL, live re-layout while a window is resized.
