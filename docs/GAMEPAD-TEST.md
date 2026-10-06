# Gamepad hardware test (about ten minutes)

The gamepad support has only been tested with a simulated pad in the autopilot. This checklist
is for the first test on a real controller or a Steam Deck. Write down what happened at each step
and keep `Player.log`. Where the log lives:

- Linux: `~/.config/unity3d/Nearby Games/Pack The Trunk/Player.log`
- Steam Deck (desktop mode): the same path, under the `deck` user

## Before you start

1. Connect the controller and switch it on **before** launching the game. If it has a mode switch
   (8BitDo pads, for example), use X-input / Xbox mode.
2. Launch with `./PackTheTrunk.sh`. On a Steam Deck in Game Mode, add it as a non-Steam game.
   Steam Input then presents the Deck's controls as an Xbox pad.
3. Quit after the test and look for the `[Input]` lines near the top of `Player.log`:
   - `[Input] found Gamepad "…"` or `[Input] added Gamepad "…"` means Unity sees a gamepad. Good.
   - `[Input] … Joystick "…"` followed by "is a generic joystick" means Unity doesn't recognise it as a
     gamepad, and the game can't use it. If it connects while you're packing, the game also shows a toast saying so. Try X-input mode or
     Steam Input, and send the line to the developer.
   - No `[Input]` line for the controller at all means the player never saw it. Check that `/dev/input`
     has an `event*` node for it (`ls -l /dev/input/by-id`) and that your user can read it.

## Checklist

| # | Do this | Expect |
| --- | --- | --- |
| 1 | On the title, push the left stick | A round cursor appears, the mouse pointer hides |
| 2 | Steer onto **Continue** / **Begin the story**, press **A** | The menu button clicks |
| 3 | On the story texts, press **Menu** (Start) | The texts hurry along, then packing starts |
| 4 | Point at something on the blanket, press **A** | It's picked up, and the key hints at the bottom show pad buttons |
| 5 | Press **X**, **Y**, **RB** (hold **LB** for the other way) | Turn, tip over, roll sideways |
| 6 | Point into the trunk, press **A** | It drops in |
| 7 | Press **View** (Back/Select) | Undo |
| 8 | Press **D-pad left** | Grandpa's hint (orange ghost) |
| 9 | Hold something over a stack, press **D-pad up / down** | The ghost moves between shelves |
| 10 | Click the **left stick** in and hold it | X-ray: packed things go see-through |
| 11 | Move the **right stick**, pull the **triggers** | Camera orbits, zooms (empty hands) |
| 12 | Press **B** holding something, then again with empty hands | Puts it back, then pauses (like Esc); B again resumes |
| 13 | Pack the essentials, press **D-pad right** twice | The trunk closes (once if no extra still fits) |
| 14 | On the postcard, **Menu** / **X** / **B** | Next trip / try again / map |
| 15 | Press **Menu** while packing, steer to **Resume**, **A** | Pauses, resumes |
| 16 | Move the real mouse | The pad cursor hides, the hints go back to keys |
| 17 | Unplug or switch off the controller mid-trip, then reconnect | Log shows `[Input] removed` then `added` (or `disconnected` / `reconnected`); the game keeps working with the mouse, and the pad works again after reconnecting |

Things worth noting even if everything works: stick dead-zone (does the cursor drift?), cursor
speed, whether **A**'s and **B**'s positions match the labels on your pad (Nintendo-layout pads swap
them), and whether the Deck's on-screen text is readable at 1280×800 (Settings → Display →
Interface size).
