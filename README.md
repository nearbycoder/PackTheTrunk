# Pack The Trunk

Fit suitcases, groceries, camping gear, and ridiculous objects into increasingly awkward spaces.

A voxel packing puzzle built in Unity 6.6 (URP). Each level parks a car in a driveway next to a
pile of stuff. Pack every **essential** into the trunk, then close it. Each **extra** you squeeze
in raises your star rating, and everything you don't fit gets left on the curb.

## Controls

| Input | Action |
| --- | --- |
| Left click | Pick up an item (from the driveway, the list, or back out of the trunk) / drop it in |
| `R` or right click | Turn (hold `Shift` to reverse) |
| `T` | Tip it over, away from the camera |
| `F` | Roll it sideways |
| Mouse wheel / `W` `S` | Choose between resting heights (shelves and gaps) in that spot |
| `Esc` or click off the trunk | Put the item back (`Esc` with empty hands pauses) |
| `Z` / `Backspace` | Undo |
| `Space` / `Enter` | Close the trunk once the essentials are packed |
| Right-drag, `Q` `E` | Orbit the camera, wheel zooms when your hands are empty |
| `M` | Music on / off |
| `Space` / click during story texts | Hurry the texts along; `Space` / `Enter` then starts packing |
| `Space` / `R` / `Esc` on the postcard | Next trip / try again / trip map |

## Menus and settings

The game boots to a title screen (the next trip's car parked in the driveway, slow camera drift)
and a main menu: **Continue**, **Trip Map**, **Family Album**, **Settings**, **Credits**, **Quit**.
`Esc` pauses while packing (resume, restart, settings, trip map, main menu) and blurs the scene
behind the menu (it also pauses by itself if the window loses focus mid-trip). Screens change
behind a paper-wipe transition with a little car driving across; input waits until it has passed,
so nothing can be pressed twice.

Settings are saved and applied live (`Scripts/Core/GameSettings.cs`):

- **Audio**: master, music, sound effects and ambience volume; mute when the window loses focus.
- **Display**: window mode, resolution, V-Sync, frame-rate limit, field of view, interface size.
- **Graphics**: quality preset (Low/Medium/High/Ultra/Custom), render resolution, anti-aliasing
  (off, FXAA, SMAA, MSAA 4x + SMAA), shadow quality, ambient occlusion, ink outlines, depth of
  field, bloom. These drive a runtime copy of the URP asset and its renderer features.
- **Gameplay**: camera speed, invert tilt, screen shake, key hints, story text speed, erase progress.
- **Controls**: the full key list.

## Rules

- Items snap to a grid and can't overlap the car, wheel wells, sloped hatch glass, toolboxes,
  or the clown who was already in the car.
- Everything has to rest on something. You can't leave items floating.
- **Fragile** items (eggs, cake, gnome, lava lamp…) can't have anything on top of them.
- Stars: ★ every essential packed, ★★ at least half the extras, ★★★ everything.

## Story and levels

33 trips across 30 years of one family's life, told through texts from relatives and handwritten
notes. It starts in 1998 when Grandpa Joe teaches you to pack his little red wagon for a picnic
at the creek. His rule: *big things first, fragile on top, and always leave room for one more
thing.* After that you're the family's packer. Heirlooms come back from trip to trip: Mr. Buttons
the teddy, Grandpa's guitar and portrait, the grandfather clock, Grandma Rose's armchair, the
garden gnome, the lava lamp, and the wagon itself.

| Chapter | Years | Trips |
| --- | --- | --- |
| I. Big Things First | 1998 | The Little Red Wagon |
| II. The Summer of Everything | 2010 | Weekend Getaway, Grocery Run, Into the Woods, Duck Derby, Grandma's Big Move, Surf's Up, The Clown Car, Everything But... |
| III. Leaving the Nest | 2011 to 2014 | Dorm Bound, Festival Weekend, Move-Out Day, The Big City, Cap and Gown |
| IV. Two of Us | 2015 to 2019 | Six Hundred Records, Two of Everything, The Question, Wedding Day, Just Married, Gone Fishing |
| V. Our Own Front Door | 2020 to 2023 | Our Own Front Door, Grandma's 90th, Flat-Pack Nursery, Coming Home, Everything a Baby Needs, First Snow, The Sunny Pines Talent Show |
| VI. One More Thing | 2024 to 2027 | Little Helper, Coming Home to Us, Lake Mirabel, Again, The Garage Sale, Everyone, Everything, One More Thing |

The trip map is a family album paged by chapter, and each chapter opens with a title card. When
you close a trunk the game snaps a photo of it (saved under `persistentDataPath/album`). The
finale puts you back in Grandpa's wagon, teaching your daughter Rosie. Then Grandma Rose's last
note leads to the family album: one polaroid for every trip you packed.

`Tools/solve_levels.py` proves every level can be packed 100% under the game's rules, and prints
a solution if you're stuck (spoilers): `python3 Tools/solve_levels.py grandma`.

## Project layout

```
Assets/
  Resources/PackTheTrunkData.json   all items and levels (shapes are ASCII layers)
  Resources/Materials/              URP Lit templates (opaque + transparent ghost)
  Scripts/Data/                     JSON loading, VoxelShape (rotations)
  Scripts/Gameplay/                 GameController, TrunkGrid (rules), PackItem, CameraRig
  Scripts/Visuals/                  voxel mesher, procedural cars, material cache
  Scripts/Core/                     GameSettings (options, applied live), UiTime (menu clock), OwnedAssets
  Scripts/UI/                       runtime-built uGUI: title, menus, settings, HUD, results, album
  Scripts/Audio/                    MusicDirector (soundtrack), Sfx (foley, stingers, ambience), MasterBus (limiter)
  Resources/Audio/                  sound effects and ambience loops (see CREDITS.md there)
  Editor/BuildScript.cs             "Pack The Trunk" menu + batch build entry points
Tools/
  solve_levels.py                   level solver / validator
  unity.sh                          editor launcher (works around libxml2 on CachyOS)
```

## Music

Chill lo-fi from **TAD's "lofi Compilation"** ([OpenGameArt](https://opengameart.org/content/lofi-compilation),
CC0 public domain), trimmed and loudness-normalised in `Assets/Resources/Music` (see `CREDITS.md`
there). `MusicDirector` gives each trip its own track (`"music"` in the level JSON), crossfades
between screens, loops by crossfading into itself, muffles the music behind a low-pass filter while
the story texts are on screen, and ducks it under the trunk slam, honk and win jingle.

## Sound

Recorded foley and UI sounds come from Kenney's CC0 packs; the driveway ambience (birdsong and a
breeze) is a CC0 park recording by Thimras cut into seamless loops. The warm moments (trip
complete, chapter cards, star bells, the "press any key" chime) plus the horn, engine and whooshes
are synthesized offline by `Tools/audio/render_stingers.py` (FM electric piano and bells through a
convolution reverb). `Sfx` pools 16 voices, pans each sound to where it happens on screen, picks a
landing sound from the item's material and size (soft bags, wood, metal, glass for fragile things),
randomises pitch and never repeats the same variation twice in a row. Every clip is loaded at boot;
when all voices are busy the oldest ordinary sound is stolen, never a stinger. The final mix runs
through `MasterBus` on the audio listener: +4 dB make-up gain (the raw mix sat near -25 LUFS),
a gentle 2:1 bus compressor above -10 dBFS and a -1.5 dBFS peak limiter, so stacked one-shots
can't clip. `Tools/audio/build_sfx.sh`
rebuilds the folder; sources and licences are in `Assets/Resources/Audio/CREDITS.md`.

## Models (Blender)

All 3D art is generated by Python scripts run in Blender 4.5 (`Tools/blender/`), exported as FBX
into `Assets/Resources/Models/`, and loaded at runtime by `ModelLibrary`:

- `items.py`: one builder per item (60). Each model fills exactly the grid cells its item occupies
  in the JSON, so what you see is what you pack. Rotations turn the model with the item, and an
  invisible voxel mesh stays as the click target.
- `vehicles.py`: one body per level, wrapped around that level's exact trunk size. `Lid`, `LidFlap`,
  `Tailgate` and `Wheel_*` are separate objects so they can animate.
- `props.py`: trees, bushes, the house at the end of the driveway, fence, mailbox, street lamps.
- `ptt_lib.py`: primitives authored in Unity coordinates. Materials are named `col_/metal_/glass_/glow_`
  plus a hex colour and get swapped for shared URP materials on load.

Rebuild with `Tools/build_models.sh`. To review models, render Eevee previews from the game-camera
angle and the reverse angle, then lay them out as labelled contact sheets:

```sh
Tools/build_models.sh items --only duck,tuba --preview /tmp/prev
python3 Tools/blender/contact_sheet.py /tmp/prev items
```

Exported objects carry the display names ("Giant Rubber Duck", "Sedan (Weekend Getaway)"); file
names stay as the JSON ids. Items without a
builder fall back to coloured voxels, so new JSON items work before they're modelled.

Everything is created at runtime from `GameController` (bootstrapped automatically by
`RuntimeInitializeOnLoadMethod`), so the `Main` scene only holds the camera, sun, and
post-processing volume. To add an item or level, edit the JSON. In item layers, `|` separates
rows (far to near), each layer string is one slice from bottom to top, letters map to the
`palette`, and `.` is empty.

## Running

```sh
Tools/play.sh                # play the built game
Tools/unity.sh               # open in the editor, then press Play
Tools/unity.sh build-linux   # Builds/Linux/PackTheTrunk.x86_64
Tools/unity.sh build-webgl   # Builds/WebGL
Tools/autopilot.sh           # self-test: menus, settings, pause and all 33 trips; PASS/FAIL + screenshots
PTT_QUICK=1 Tools/autopilot.sh  # same, but only three trips (about a minute)
Tools/record.sh [name]       # gameplay video (title, menus, settings, three trips, pause, finale, album, credits)
Tools/play.sh -pttBench      # benchmark: holds each screen uncapped, logs frame times ([Perf] lines in the player log)
```

Performance: the self-test and benchmark log per-phase frame times (average, p50/p95/p99, max,
frames over 33 ms), level build times and the audio mix peak (`[Perf]` and `[Audio]` lines). On
the development machine (Ryzen AI Max+ 395 / Radeon 8060S, 1600×900, High) every screen averages
2–3 ms uncapped with no frames over 33 ms during play, a trip builds in 25–100 ms behind the
transition, and the mix peaks around -2 dBFS with nothing clipping. Runtime-made meshes, textures
and materials are freed with their trip (`OwnedAssets`), album photos are read back from the GPU
asynchronously and saved as PNG on a worker thread, and the packing loop allocates nothing per frame.

The autopilot (`Assets/Scripts/Gameplay/AutoPilot.cs`) only runs when the player is launched
with `-pttAutopilot`. It clicks, rotates, drops and undoes using real input events, packs all
33 trips from the solver's solutions, closes every trunk, opens the ending and the album, and
writes screenshots to `/tmp/ptt-autopilot` (about 5 minutes).

Machine notes (CachyOS, Wayland):
- Unity Hub is installed per-user in `~/Applications/unityhub` (from Unity's official repo).
  The editor uses the Personal license on the signed-in account.
- The editor needs `libxml2.so.2`. `sudo pacman -S libxml2-legacy` fixes that properly;
  `Tools/unity.sh` uses a local copy in `~/.local/share/ptt-unity-libs` meanwhile.
- The player hangs at startup through XWayland, so `Tools/play.sh` passes `-force-wayland`.
