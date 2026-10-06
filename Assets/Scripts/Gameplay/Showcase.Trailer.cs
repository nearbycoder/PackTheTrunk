using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PackTheTrunk
{
    /// <summary>
    /// Trailer capture: <c>-pttShowcase &lt;dir&gt; -pttTrailer -pttSolutions &lt;file&gt;</c>.
    /// Plays one short, scripted clip per feature and records only those clips, with the
    /// soundtrack muted (the trailer lays its own music bed) and no captions (Tools/make_trailer.py
    /// adds them). Writes beats.tsv (name, first frame, end frame) and clean stills for the README.
    /// Run it through Tools/record_trailer.sh.
    /// </summary>
    public partial class Showcase
    {
        bool trailer;

        /// <summary>-pttStillsOnly: play the script but save only the stills (no video frames or audio).</summary>
        bool stillsOnly;
        Canvas overlay;
        string pendingStill;
        int stillCount;
        bool cursorShown = true;
        string beatsPath;
        string beatName;
        int beatStart;
        RectTransform nowPlaying;

        IEnumerator TrailerScript()
        {
            Directory.CreateDirectory(Path.Combine(outDir, "stills"));
            beatsPath = Path.Combine(outDir, "beats.tsv");
            File.WriteAllText(beatsPath, "");
            GameSettings.Preset = 3; // Ultra: supersampled, best shadows
            var music = FindAnyObjectByType<MusicDirector>();
            if (music != null && !music.Muted) music.ToggleMute();
            nowPlaying = FindObjectsByType<RectTransform>(FindObjectsInactive.Include).FirstOrDefault(r => r.name == "Now Playing");
            ids = GameDatabase.Levels.Select(l => l.Id).ToList();
            // -pttTrailerOnly fragile,clown re-shoots just those sections (each sets up its own trip).
            var args = Environment.GetCommandLineArgs();
            int o = Array.IndexOf(args, "-pttTrailerOnly");
            var only = o >= 0 && o + 1 < args.Length ? new HashSet<string>(args[o + 1].Split(',')) : null;
            Log("trailer: recording" + (only != null ? " " + string.Join(",", only) : ""));
            cursorShown = false;
            yield return Hold(0.8f); // let the soundtrack fade out

            var sections = new (string, Func<IEnumerator>)[]
            {
                ("intro", Intro), ("fragile", FragileSection), ("clown", ClownSection), ("arrivals", Arrivals),
                ("album", MapAndAlbum), ("speed", Escalation), ("heights", Shelves), ("coldopen", ColdOpen),
                ("features", FeatureStills),
            };
            foreach (var (name, section) in sections)
                if (only == null || only.Contains(name))
                    yield return section();

            yield return null;
            FinishAudio();
            Log($"done, {frame} frames, {wavSamples / Mathf.Max(1, channels)} audio frames, {stillCount} stills");
            yield return null;
            Application.Quit();
        }

        List<string> ids;

        /// <summary>Title, menus and settings, Chapter I, then the whole Weekend Getaway trip.</summary>
        IEnumerator Intro()
        {
            // ---- Title screen: restarted once the soundtrack has faded, so the logo drops in on camera.
            game.AutoShowTitle();
            Beat("title");
            yield return Hold(5.2f);
            yield return Still("title");
            yield return Hold(1.6f);

            // ---- Main menu, settings (graphics tab, a live toggle).
            mouse = new Vector2(Screen.width * 0.62f, Screen.height * 0.32f);
            Push();
            cursorShown = true;
            Beat("menu");
            yield return Press(Key.Space, false);
            yield return Hold(1.3f);
            foreach (var entry in new[] { "Continue", "Trip Map", "Album", "Settings" })
            {
                yield return MoveTo(UiPoint(entry), 0.32f);
                yield return Hold(0.22f);
            }
            yield return Click();
            yield return Hold(1.2f);
            yield return MoveTo(UiPoint("Tab GRAPHICS"), 0.45f);
            yield return Hold(0.15f);
            yield return Click();
            yield return Hold(1.1f);
            yield return MoveTo(NamedPoint("Ink outlines", "Switch"), 0.5f);
            yield return Hold(0.25f);
            yield return Click();
            yield return Hold(1.0f);
            yield return Click();
            yield return Hold(0.8f);
            Cut();
            yield return Still("settings");
            yield return ClickButton("Settings Back", 0.4f, 0.2f);
            yield return Hold(0.9f);

            // ---- Chapter I: the paper-wipe, the chapter card and Grandpa's note.
            Beat("chapter");
            yield return ClickButton("Continue", 0.6f, 0.3f);
            yield return Hold(9.5f);
            Cut();
            cursorShown = false;

            // ---- Trip 2: the car backs in while Mom texts.
            game.AutoTransitionTrip(ids.IndexOf("weekend"));
            Beat("story");
            for (float t = 0f; t < 40f && !ButtonVisible("Start"); t += UiTime.Delta) yield return null;
            yield return Hold(1.4f);
            Cut();
            yield return Still("story");

            // ---- Packing: essentials first (the game cheers when they're in), then the extras.
            var weekend = EssentialsFirst("weekend");
            cursorShown = true;
            Beat("core");
            yield return ClickButton("Start", 0.6f, 0.25f);
            yield return Hold(1.5f);
            yield return PackStep(weekend[0]);
            yield return PackStep(weekend[1], aimStill: "packing");
            yield return Hold(0.3f);
            Cut();

            Beat("essentials");
            yield return PackStep(weekend[2], 0.85f);
            yield return PackStep(weekend[3], 0.85f);
            yield return Hold(2.6f);
            Cut();

            Beat("rotate");
            yield return PackStep(weekend[4], 1f, spins: true);
            yield return Hold(0.5f);
            Cut();

            // ---- Undo, and lifting something back out of the trunk.
            Beat("undo");
            yield return Press(Key.Z, false);
            yield return Hold(1.3f);
            var lift = game.Items.Where(i => i.State == ItemState.Packed && i.Def.Id == weekend[3].Item1).FirstOrDefault();
            if (lift != null)
            {
                yield return MoveTo(PointOnItem(lift), 0.55f);
                yield return Hold(0.9f);
                yield return Click();
                yield return Hold(0.9f);
                // Esc puts it back where it was (with empty hands it would pause instead).
                if (game.Held != null) yield return Press(Key.Escape, false);
                yield return Hold(0.9f);
            }
            Cut();
            Place(weekend[4]);
            Place(weekend[5]);
            yield return Hold(1.0f);

            // ---- Close it: slam, confetti, honk, drive off, and the postcard (two extras left on the curb).
            Beat("close");
            yield return ClickButton("Close", 0.6f, 0.25f);
            cursorShown = false;
            yield return Hold(2.23f);
            yield return Still("slam");
            yield return Hold(5.17f);
            yield return Still("postcard");
            yield return Hold(1.2f);
            Cut();
        }

        /// <summary>The cake goes in (it's fragile), then a grocery bag tries to sit on it.</summary>
        IEnumerator FragileSection()
        {
            var groceries = solutions["groceries"];
            game.AutoStartLevel(ids.IndexOf("groceries"));
            cursorShown = false;
            yield return Hold(2.4f);
            Place(groceries[0]);
            Place(groceries[1]);
            yield return Hold(1.2f);
            mouse = new Vector2(Screen.width * 0.45f, Screen.height * 0.5f);
            Push();
            cursorShown = true;
            Beat("fragile");
            yield return PackStep(groceries[2]);
            // A one-cell grocery bag fits the gap above the cake, so the only thing stopping it is the rule.
            var bag = groceries.First(p => p.Item1 == "grocery_bag");
            yield return PackStep(bag, 1f, beforeAim: TryOnFragile);
            yield return Hold(0.4f);
            Cut();
        }

        /// <summary>The Clown Car already has a clown in it; then Esc pauses and the world blurs.</summary>
        IEnumerator ClownSection()
        {
            var clown = solutions["clown"];
            game.AutoStartLevel(ids.IndexOf("clown"));
            cursorShown = false;
            yield return Hold(2.6f);
            // Things can rest on the clown's head, so a flat trombone would fit above him. The tuba
            // is two tall: over the clown there's no height that works, and the game says so.
            Place(clown[0]);
            yield return Hold(0.8f);
            mouse = new Vector2(Screen.width * 0.4f, Screen.height * 0.45f);
            Push();
            cursorShown = true;
            // The default framing looks straight into the trunk, where the clown is sitting.
            Beat("clown");
            yield return Hold(0.6f);
            yield return PackStep(clown[1], 1f, beforeAim: TryBlocked, blockedStill: "clown");
            yield return Hold(0.6f);
            Cut();

            Beat("pause");
            yield return Press(Key.Escape, false);
            yield return Hold(1.6f);
            yield return MoveTo(UiPoint("Pause Settings"), 0.45f);
            yield return Hold(0.5f);
            yield return ClickButton("Resume", 0.45f, 0.3f);
            yield return Hold(0.7f);
            Cut();
        }

        /// <summary>Every ride, sweeping in with its pile of stuff landing on the blanket.</summary>
        IEnumerator Arrivals()
        {
            cursorShown = false;
            foreach (var id in new[] { "beach", "house", "honeymoon", "mini", "grandma", "camping", "everything", "groceries", "talent" })
            {
                game.AutoStartLevel(ids.IndexOf(id));
                Beat("arrive_" + id);
                yield return Hold(2.8f);
                Cut();
            }
        }

        /// <summary>Pack and close every trip off camera, then show the trip map and the family album.</summary>
        IEnumerator MapAndAlbum()
        {
            cursorShown = false;
            for (int i = 0; i < ids.Count; i++)
            {
                if (!solutions.TryGetValue(ids[i], out var sol)) continue;
                game.AutoStartLevel(i);
                yield return Hold(0.4f);
                foreach (var p in sol) Place(p);
                yield return Hold(0.9f);
                game.AutoClose();
                for (float t = 0f; t < 12f && !game.IsShowingResults; t += UiTime.Delta) yield return null;
                yield return Hold(0.5f);
            }
            Log("trailer: every trip packed");

            game.AutoShowMenu();
            cursorShown = true;
            mouse = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Push();
            Beat("map");
            yield return Hold(1.6f);
            for (int k = 0; k < 4; k++)
            {
                yield return ClickButton("Prev Page", k == 0 ? 0.5f : 0.15f, 0.15f);
                yield return Hold(1.1f);
            }
            Cut();
            yield return Still("map");

            // The polaroids deal themselves out one by one; wait for all 33 before the still.
            game.AutoShowMenuAlbum();
            cursorShown = false;
            Beat("album");
            yield return Hold(11f);
            yield return Still("album");
            yield return Hold(0.5f);
            Cut();
        }

        /// <summary>Bigger and bigger loads, packed fast; the last one slams shut.</summary>
        IEnumerator Escalation()
        {
            cursorShown = false;
            foreach (var id in new[] { "festival", "house", "everything" })
            {
                game.AutoStartLevel(ids.IndexOf(id));
                yield return Hold(2.6f);
                Beat("speed_" + id);
                yield return Hold(0.3f);
                foreach (var p in solutions[id])
                {
                    Place(p);
                    yield return Hold(0.17f);
                }
                yield return Hold(0.9f);
                if (id == "everything")
                {
                    game.AutoClose();
                    yield return Hold(4.6f);
                }
                Cut();
            }
        }

        /// <summary>
        /// Shelves and gaps (W / S): in the Garage Sale's pickup bed the futon has an overhang, so the
        /// beanbag can sit on top of it or tuck in underneath.
        /// </summary>
        IEnumerator Shelves()
        {
            var garage = solutions["garage"];
            game.AutoStartLevel(ids.IndexOf("garage"));
            cursorShown = false;
            yield return Hold(2.6f);
            Place(garage[0]);
            Place(garage[1]);
            FrameOn(garage[2].Item1);
            yield return Hold(2.0f);
            mouse = new Vector2(Screen.width * 0.45f, Screen.height * 0.5f);
            Push();
            cursorShown = true;
            Beat("heights");
            yield return PackStep(garage[2], 1f, beforeAim: held => ShowShelves(held, garage[2].Item2));
            yield return Hold(0.5f);
            Cut();
        }

        /// <summary>The cold open: the last two things go into the biggest trunk, and it slams shut.</summary>
        IEnumerator ColdOpen()
        {
            var reunion = solutions["reunion2"];
            game.AutoStartLevel(ids.IndexOf("reunion2"));
            cursorShown = false;
            yield return Hold(2.6f);
            for (int i = 0; i < reunion.Count - 2; i++) Place(reunion[i]);
            // The blanket is huge here; move in on the trunk and the last two things.
            FrameOn(reunion[reunion.Count - 2].Item1, reunion[reunion.Count - 1].Item1);
            yield return Hold(2.2f);
            yield return Still("late");

            Beat("coldopen");
            yield return PackStep(reunion[reunion.Count - 2], 0.9f);
            yield return PackStep(reunion[reunion.Count - 1], 0.9f);
            yield return Hold(1.1f);
            yield return ClickButton("Close", 0.55f, 0.2f);
            cursorShown = false;
            yield return Hold(0.75f);
            yield return Still("slam-wide");
            yield return Hold(7.4f);
            Cut();
        }

        /// <summary>
        /// README stills for the newer features (no video): a Grandpa's tip on a new player's second
        /// trip, Ask Grandpa's ghost on a half-packed SUV, and X-ray in a mostly packed minivan.
        /// </summary>
        IEnumerator FeatureStills()
        {
            // ---- Grandpa's tip while aiming the first suitcase (tips are normally off in captures).
            GameController.CaptureTips = true;
            game.AutoResetTips();
            var weekend = EssentialsFirst("weekend");
            game.AutoStartLevel(ids.IndexOf("weekend"));
            cursorShown = true;
            yield return Hold(3f);
            var (firstId, firstCells) = weekend[0];
            var first = game.Items.First(i => i.Def.Id == firstId && i.State == ItemState.Pile);
            yield return MoveTo(PointOnItem(first), 0.5f);
            yield return Click();
            yield return Hold(0.4f);
            if (game.Held != first) game.AutoHold(first);
            first.SetOrientation(AutoPilot.FindOrientation(first.Def.Shape, firstCells));
            yield return MoveTo(TrunkPoint(firstCells.Aggregate(Vector3Int.Min), first.Shape.Size), 0.6f);
            yield return Hold(1.6f);
            yield return Still("tip");
            GameController.CaptureTips = false;
            yield return Press(Key.Escape, false);
            yield return Hold(0.4f);

            // ---- Ask Grandpa on a half-packed Into the Woods.
            var camping = solutions["camping"];
            game.AutoStartLevel(ids.IndexOf("camping"));
            cursorShown = false;
            yield return Hold(2.8f);
            for (int i = 0; i < camping.Count / 2; i++) Place(camping[i]);
            yield return Hold(1.2f);
            yield return Press(Key.H, false);
            yield return Hold(0.2f);
            FrameOn();
            yield return Hold(2.0f);
            yield return Still("hint");

            // ---- X-ray: Everyone, Everything nearly packed, holding the next thing, Tab held down.
            var reunion = solutions["reunion2"];
            game.AutoStartLevel(ids.IndexOf("reunion2"));
            yield return Hold(2.8f);
            int packed = reunion.Count - 5;
            for (int i = 0; i < packed; i++) Place(reunion[i]);
            FrameOn();
            yield return Hold(2.2f);
            var (nextId, nextCells) = reunion[packed];
            var next = game.Items.First(i => i.Def.Id == nextId && i.State == ItemState.Pile);
            game.AutoHold(next);
            next.SetOrientation(AutoPilot.FindOrientation(next.Def.Shape, nextCells));
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Tab));
            yield return Hold(0.3f);
            // Aim at the lowest open spot X-ray can reach (scan the floor of every column).
            var size = game.TrunkSize;
            int bestY = int.MaxValue;
            for (int z = size.z - 1; z >= 0; z--)
            for (int x = 0; x < size.x; x++)
            {
                var point = TrunkPoint(new Vector3Int(x, 0, z), next.Shape.Size);
                if (game.PreviewTarget(point, out var at, out var ok) && ok && at.y < bestY)
                {
                    mouse = point;
                    bestY = at.y;
                }
            }
            Log(bestY < int.MaxValue ? $"x-ray still: aiming {next.Def.Id} at height {bestY}" : "x-ray still: no open spot found");
            Push();
            yield return Hold(1.0f);
            yield return Still("xray");
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return Hold(0.3f);
            game.AutoPutBack();
        }

        /// <summary>
        /// Screen point that makes the game aim something this big with its corner at <paramref name="cell"/>
        /// (it centres what you hold on the cell under the cursor, rounding down).
        /// </summary>
        Vector2 TrunkPoint(Vector3Int cell, Vector3Int size)
        {
            var world = game.CurrentVehicle.transform.TransformPoint(new Vector3(cell.x + (size.x - 1) / 2 + 0.5f, cell.y, cell.z + (size.z - 1) / 2 + 0.5f));
            var s = game.Camera.WorldToScreenPoint(world);
            return new Vector2(s.x, s.y);
        }

        /// <summary>Glide the camera in on the trunk plus the named pile items (instead of the whole blanket).</summary>
        void FrameOn(params string[] itemIds)
        {
            var t = game.CurrentVehicle.transform;
            var size = (Vector3)game.TrunkSize;
            var b = new Bounds(t.TransformPoint(size * 0.5f), Vector3.zero);
            b.Encapsulate(t.TransformPoint(new Vector3(-1.2f, -0.6f, -1.6f)));
            b.Encapsulate(t.TransformPoint(size + new Vector3(1.2f, 0.6f, 1.2f)));
            foreach (var item in game.Items.Where(i => i.State == ItemState.Pile && itemIds.Contains(i.Def.Id)))
                foreach (var r in item.GetComponentsInChildren<Renderer>())
                    b.Encapsulate(r.bounds);
            game.Rig.Frame(b, false, 0.74f);
        }

        // ------------------------------------------------------------------ beats and stills

        void Beat(string name)
        {
            if (stillsOnly) return;
            if (beatName != null) Cut();
            beatName = name;
            beatStart = frame;
            capturing = true;
        }

        void Cut()
        {
            if (beatName == null) return;
            capturing = false;
            File.AppendAllText(beatsPath, $"{beatName}\t{beatStart}\t{frame}\n");
            Log($"beat {beatName}: frames {beatStart}-{frame}");
            beatName = null;
        }

        /// <summary>Save this frame without the cursor/key overlay (the video frame loses the cursor for one frame).</summary>
        IEnumerator Still(string name)
        {
            pendingStill = $"{++stillCount:00}-{name}";
            overlay.enabled = false;
            yield return null;
        }

        void SaveStill()
        {
            File.WriteAllBytes(Path.Combine(outDir, "stills", pendingStill + ".png"), grab.EncodeToPNG());
            Log("still " + pendingStill);
            pendingStill = null;
            overlay.enabled = true;
        }

        void TrailerLateUpdate()
        {
            // The trailer has its own soundtrack; don't announce tracks that aren't playing.
            if (nowPlaying != null && nowPlaying.gameObject.activeSelf) nowPlaying.gameObject.SetActive(false);
            cursor.gameObject.SetActive(cursorShown);
            ring.gameObject.SetActive(cursorShown);
        }

        // ------------------------------------------------------------------ packing

        /// <summary>The solver's placements with the essentials first (only where that order is still valid).</summary>
        List<(string, List<Vector3Int>)> EssentialsFirst(string levelId)
        {
            var level = GameDatabase.Levels.First(l => l.Id == levelId);
            var required = level.Required.Select(d => d.Id).ToList();
            var first = new List<(string, List<Vector3Int>)>();
            var rest = new List<(string, List<Vector3Int>)>();
            foreach (var p in solutions[levelId])
            {
                if (required.Remove(p.Item1)) first.Add(p);
                else rest.Add(p);
            }
            return first.Concat(rest).ToList();
        }

        /// <summary>Pack instantly, off camera.</summary>
        void Place((string, List<Vector3Int>) placement)
        {
            var (itemId, cells) = placement;
            var item = game.Items.FirstOrDefault(i => i.Def.Id == itemId && i.State == ItemState.Pile);
            if (item == null) return;
            if (!game.AutoPlace(item, AutoPilot.FindOrientation(item.Def.Shape, cells), cells.Aggregate(Vector3Int.Min)))
                Log("could not place " + itemId);
        }

        /// <summary>Pack one item the way a player would: pick it up, turn it, aim, choose the height, drop.</summary>
        IEnumerator PackStep((string, List<Vector3Int>) placement, float pace = 1f, bool spins = false,
            Func<PackItem, IEnumerator> beforeAim = null, string aimStill = null, string blockedStill = null)
        {
            var (itemId, cells) = placement;
            var item = game.Items.FirstOrDefault(i => i.Def.Id == itemId && i.State == ItemState.Pile);
            if (item == null)
            {
                Log("no " + itemId + " on the driveway");
                yield break;
            }
            var min = cells.Aggregate(Vector3Int.Min);
            var want = new HashSet<Vector3Int>(cells.Select(c => c - min));

            yield return MoveTo(PointOnItem(item), 0.5f * pace);
            yield return Hold(0.12f * pace);
            yield return Click();
            yield return Hold(0.3f * pace);
            if (game.Held != item)
            {
                // The click landed on a neighbour in the pile (or missed): take the one we meant.
                Log($"click missed {itemId}, picking it up directly");
                game.AutoHold(item);
                yield return Hold(0.2f);
            }
            if (game.Held != item)
            {
                Fallback(item, cells, min);
                yield return Hold(0.3f);
                yield break;
            }

            if (spins)
            {
                // Show off all three turns right where it was picked up (the top of the screen is
                // where the trailer's captions go).
                foreach (var key in new[] { Key.R, Key.T, Key.F })
                {
                    yield return Press(key, false);
                    yield return Hold(0.6f);
                }
            }
            foreach (var (key, shift) in PlanRotation(item, want))
            {
                yield return Press(key, shift);
                yield return Hold(0.32f * pace);
            }

            pendingBlockedStill = blockedStill;
            if (beforeAim != null) yield return beforeAim(item);

            var aim = FindAim(min, item.Shape.Size);
            if (aim == null)
            {
                Log($"no aim for {itemId} at {min}");
                Fallback(item, cells, min);
                yield return Hold(0.3f);
                yield break;
            }
            yield return MoveTo(aim.Value, 0.6f * pace);
            yield return Hold(0.18f * pace);
            for (int k = 0; k < 4; k++)
            {
                if (!game.CurrentTarget(out var t, out var valid) || (t == min && valid)) break;
                if (t.x != min.x || t.z != min.z) break;
                yield return Press(t.y < min.y ? Key.W : Key.S, false);
                yield return Hold(0.25f);
            }
            if (aimStill != null)
            {
                yield return Hold(0.35f);
                yield return Still(aimStill);
            }
            if (game.CurrentTarget(out var target, out var ok) && target == min && ok)
            {
                yield return Click();
                yield return Hold(0.6f * pace);
            }
            else
            {
                Fallback(item, cells, min);
                yield return Hold(0.3f);
            }
        }

        string pendingBlockedStill;

        /// <summary>Hover the held item over a fragile one and try to drop it: the game says no.</summary>
        IEnumerator TryOnFragile(PackItem held)
        {
            // Aim at the top face of every exposed cell of every packed fragile item until the game
            // answers "fragile" (rather than "no room" or "too tall").
            var points =
                from candidate in game.Items.Where(i => i.Def.Fragile && i.State == ItemState.Packed)
                let cells = new HashSet<Vector3Int>(candidate.Shape.Voxels.Select(v => v.Pos + candidate.GridPos))
                from cell in cells.Where(c => !cells.Contains(c + Vector3Int.up)).OrderByDescending(c => c.z)
                from offset in new[] { new Vector2(0.5f, 0.5f), new Vector2(0.3f, 0.7f), new Vector2(0.7f, 0.3f) }
                select (Vector2)game.Camera.WorldToScreenPoint(
                    game.CurrentVehicle.transform.TransformPoint(new Vector3(cell.x + offset.x, cell.y + 1.02f, cell.z + offset.y)));
            foreach (var point in points.ToList())
            {
                if (!game.PreviewTarget(point, out _, out var valid, out var problem) || valid || problem == null || !problem.Contains("fragile"))
                    continue;
                yield return MoveTo(point, 0.6f);
                yield return Hold(0.5f);
                yield return Click();
                yield return Hold(0.5f);
                yield return Still("fragile");
                yield return Hold(1.1f);
                yield break;
            }
            Log("no fragile spot to demo");
        }

        /// <summary>Hover the held item over something that's in the way (the clown, a wheel well) and try it.</summary>
        IEnumerator TryBlocked(PackItem held)
        {
            // Aim just above the obstructions that stand on the trunk floor (the clown, wheel wells),
            // middle of the trunk first; the corners up top are only bodywork.
            var size = game.TrunkSize;
            var centre = new Vector2((size.x - 1) * 0.5f, (size.z - 1) * 0.5f);
            var tops = new List<Vector3Int>();
            for (int x = 0; x < size.x; x++)
            for (int z = 0; z < size.z; z++)
            {
                if (!game.IsWall(new Vector3Int(x, 0, z))) continue;
                int y = 0;
                while (y + 1 < size.y && game.IsWall(new Vector3Int(x, y + 1, z))) y++;
                tops.Add(new Vector3Int(x, y, z));
            }
            // Aim at the obstruction and the floor around it (so the held item's footprint overlaps it
            // in different ways). First pass: somewhere the game says "No room". Second: any refusal.
            var aims = new List<Vector3>();
            foreach (var cell in tops.OrderBy(c => (new Vector2(c.x, c.z) - centre).sqrMagnitude))
            {
                aims.Add(new Vector3(cell.x + 0.5f, cell.y + 1.02f, cell.z + 0.5f));
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if ((dx != 0 || dz != 0) && cell.x + dx >= 0 && cell.x + dx < size.x && cell.z + dz >= 0 && cell.z + dz < size.z)
                        aims.Add(new Vector3(cell.x + dx + 0.5f, 0.02f, cell.z + dz + 0.5f));
            }
            foreach (bool strict in new[] { true, false })
            foreach (var aim in aims)
            {
                var world = game.CurrentVehicle.transform.TransformPoint(aim);
                var point = (Vector2)game.Camera.WorldToScreenPoint(world);
                if (!game.PreviewTarget(point, out _, out var valid, out var problem) || valid || problem == null ||
                    (strict && !problem.StartsWith("No room")))
                    continue;
                yield return MoveTo(point, 0.6f);
                yield return Hold(0.5f);
                yield return Click();
                yield return Hold(0.45f);
                if (pendingBlockedStill != null) yield return Still(pendingBlockedStill);
                yield return Hold(1.0f);
                yield break;
            }
            Log("no blocked spot to demo");
        }

        /// <summary>Where the held item could rest at two heights, show both with W and S.</summary>
        IEnumerator ShowShelves(PackItem held, List<Vector3Int> cells)
        {
            // The solver's spot for this item is the column with the shelf.
            var min = cells.Aggregate(Vector3Int.Min);
            var heights = game.HeldRestingHeights(min.x, min.z);
            if (heights.Count < 2)
            {
                Log("no shelf here");
                yield break;
            }
            var high = FindAim(new Vector3Int(min.x, heights[heights.Count - 1], min.z), held.Shape.Size);
            if (high == null)
            {
                Log($"no aim at the shelf for {held.Def.Id} ({min}, heights {string.Join(",", heights)})");
                yield break;
            }
            yield return MoveTo(high.Value, 0.6f);
            yield return Hold(0.6f);
            foreach (var key in new[] { Key.S, Key.W, Key.S })
            {
                yield return Press(key, false);
                yield return Hold(0.75f);
            }
        }
    }
}
