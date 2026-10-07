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
step). Releasing a drag over the HUD keeps the item in hand rather than putting it back.

Still open after round 4: Windows (needs the module), WebGL and hosting, signing and notarization,
releases and re-cutting the trailer (all owner decisions); a physical gamepad / Steam Deck test
(`docs/GAMEPAD-TEST.md`); a quiet-machine `-pttBench`; and the bigger replayability ideas from
ranked #12.
