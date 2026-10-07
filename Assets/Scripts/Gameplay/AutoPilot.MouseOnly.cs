using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PackTheTrunk
{
    public partial class AutoPilot
    {
        static string ShapeKey(VoxelShape shape) => string.Join(";", shape.Voxels.Select(v => v.Pos.ToString()).OrderBy(x => x));

        static string HintChip(Bindings.Action action) => "Key " + Bindings.Label(action);

        /// <summary>
        /// The shortest run of clicks on the turn / tip / roll key hints (around the camera's snapped axes,
        /// the way the game turns things) that gets <paramref name="item"/> from its current orientation to
        /// <paramref name="want"/>'s cells.
        /// </summary>
        List<Bindings.Action> ClicksToOrient(PackItem item, string want)
        {
            var moves = new[]
            {
                (Bindings.Action.Turn, Quaternion.AngleAxis(90f, Vector3.up)),
                (Bindings.Action.Tip, Quaternion.AngleAxis(90f, game.Rig.SnappedRight())),
                (Bindings.Action.Roll, Quaternion.AngleAxis(90f, game.Rig.SnappedForward())),
            };
            var queue = new Queue<(Quaternion Q, List<Bindings.Action> Path)>();
            var seen = new HashSet<string>();
            queue.Enqueue((item.Orientation, new List<Bindings.Action>()));
            while (queue.Count > 0)
            {
                var (q, path) = queue.Dequeue();
                string key = ShapeKey(item.Def.Shape.Rotated(q));
                if (!seen.Add(key)) continue;
                if (key == want) return path;
                foreach (var (action, turn) in moves) queue.Enqueue((turn * q, new List<Bindings.Action>(path) { action }));
            }
            return null;
        }

        IEnumerator Scroll(float y)
        {
            var p = Mouse.current.position.ReadValue();
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = p, scroll = new Vector2(0f, y) });
            yield return null;
            yield return null;
            yield return Wait(0.12f);
        }

        /// <summary>
        /// Play with the mouse alone: the turn, tip, roll and X-ray key hints are buttons too. Clicking them
        /// does exactly what the keys do, and Weekend Getaway (six of its eight things have to be tipped
        /// or rolled) packs to three stars with clicks and the wheel only.
        /// </summary>
        IEnumerator MouseOnlyChecks(Dictionary<string, List<(string, List<Vector3Int>)>> solutions)
        {
            PerfProbe.Begin("playing");
            // Grandpa's tips sit over the top of the scene; this section aims all over the trunk.
            bool tipsWere = GameSettings.Tips;
            GameSettings.Tips = false;
            Uncap();
            int weekend = LevelIndex("weekend");
            game.AutoStartLevel(weekend);
            yield return Wait(2.5f);

            // 1. With empty hands a click on the turn hint says what to do instead of nothing happening.
            yield return ClickUi(HintChip(Bindings.Action.Turn));
            yield return Wait(0.2f);
            Check(game.Held == null && game.Ui.ToastShowing("Pick something up first"), "mouse only: clicking turn with empty hands says to pick something up first");

            // 2. Turn, tip and roll by click match the keys (and Shift + click matches Shift + key).
            var item = game.Items.First(i => i.State == ItemState.Pile && i.Def.Shape.Voxels.Length > 1);
            var results = new List<string>();
            bool same = true;
            foreach (var (action, key) in new[] { (Bindings.Action.Turn, Key.R), (Bindings.Action.Tip, Key.T), (Bindings.Action.Roll, Key.F) })
            {
                foreach (bool shift in new[] { false, true })
                {
                    game.AutoHold(item);
                    yield return Wait(0.15f);
                    if (shift) yield return Press(key, Key.LeftShift);
                    else yield return Press(key);
                    var byKey = item.Orientation;
                    game.AutoPutBack();
                    yield return Wait(0.2f);
                    game.AutoHold(item);
                    yield return Wait(0.15f);
                    if (shift) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.LeftShift));
                    yield return ClickUi(HintChip(action));
                    if (shift) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                    yield return Wait(0.15f);
                    var byClick = item.Orientation;
                    bool ok = Quaternion.Angle(byKey, byClick) < 1f && Quaternion.Angle(byClick, Quaternion.identity) > 45f;
                    same &= ok;
                    results.Add($"{(shift ? "shift+" : "")}{key}{(ok ? "" : " differs")}");
                    game.AutoPutBack();
                    yield return Wait(0.2f);
                }
            }
            Check(same, $"mouse only: clicking the turn, tip and roll hints turns the {item.Def.Name} exactly as the keys do ({string.Join(", ", results)})");

            // 3. The X-ray hint: a click turns it on while you hold something, a second click turns it
            // off, and letting go of the item (dropping it or putting it back) turns it off as well.
            var packed = game.Items.Where(i => i.State == ItemState.Pile && i != item).Take(2).ToList();
            foreach (var p in packed)
            {
                var (_, cells) = solutions["weekend"].First(s => s.Item1 == p.Def.Id);
                game.AutoPlace(p, FindOrientation(p.Def.Shape, cells), cells.Aggregate(Vector3Int.Min));
                yield return Wait(0.05f);
            }
            yield return Wait(0.6f);
            game.AutoHold(item);
            yield return Wait(0.2f);
            yield return ClickUi(HintChip(Bindings.Action.XRay));
            yield return Wait(0.3f);
            bool on = game.XRayActive && game.Items.Where(i => i.State == ItemState.Packed).All(i => i.SeeThrough)
                && game.Ui.KeyboardHintCaptions().Contains("x-ray on") && game.Ui.ToastShowing("X-ray on");
            yield return Shot("mouse-only-xray-on");
            yield return ClickUi(HintChip(Bindings.Action.XRay));
            yield return Wait(0.3f);
            bool off = !game.XRayActive && game.SeeThroughCount == 0 && game.Ui.KeyboardHintCaptions().Contains("x-ray");
            Check(on && off, "mouse only: clicking the X-ray hint makes everything packed see-through (\"x-ray on\"), a second click turns it off");
            yield return ClickUi(HintChip(Bindings.Action.XRay));
            yield return Wait(0.2f);
            bool onAgain = game.XRayActive;
            yield return ClickUi("Put Back");
            yield return Wait(0.3f);
            bool offAfterPutBack = !game.XRayActive && game.SeeThroughCount == 0 && game.Held == null;
            game.AutoHold(item);
            yield return Wait(0.2f);
            yield return ClickUi(HintChip(Bindings.Action.XRay));
            yield return Wait(0.2f);
            bool onThird = game.XRayActive;
            bool dropped = FindAim(true, out var aim, out _);
            if (dropped)
            {
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = aim });
                yield return null;
                yield return null;
                yield return Wait(0.2f);
                yield return Click();
                yield return Wait(0.6f);
            }
            Check(onAgain && offAfterPutBack && onThird && dropped && item.State == ItemState.Packed && !game.XRayActive && game.SeeThroughCount == 0,
                "mouse only: a clicked X-ray goes off when the item is put back or dropped (empty hands can still pick packed things out)");

            // 4. A whole trip with the mouse alone: pick up from the blanket, click the hints to turn it
            // the way the solver packs it, aim, use the wheel for the shelf, click to drop, and close the
            // trunk with its button. No keyboard events from here on.
            game.AutoStartLevel(weekend);
            yield return Wait(2.5f);
            int clicks = 0, tipsRolls = 0, wheel = 0, placed = 0;
            var placements = solutions["weekend"];
            string failure = null;
            foreach (var (itemId, cells) in placements)
            {
                var next = game.Items.FirstOrDefault(i => i.Def.Id == itemId && i.State == ItemState.Pile);
                if (next == null) { failure = $"no {itemId} on the blanket"; break; }
                yield return MoveMouse(Top(next));
                yield return Wait(0.1f);
                yield return Click();
                yield return Wait(0.25f);
                if (game.Held != next) { failure = $"clicking the {next.Def.Name} picked up {(game.Held != null ? game.Held.Def.Name : "nothing")}"; break; }

                var min = cells.Aggregate(Vector3Int.Min);
                string want = ShapeKey(next.Def.Shape.Rotated(FindOrientation(next.Def.Shape, cells)));
                var path = ClicksToOrient(next, want);
                if (path == null) { failure = $"no clicks turn the {next.Def.Name} the solver's way"; break; }
                foreach (var action in path)
                {
                    yield return ClickUi(HintChip(action));
                    yield return Wait(0.12f);
                    clicks++;
                    if (action != Bindings.Action.Turn) tipsRolls++;
                }
                if (ShapeKey(next.Shape) != want) { failure = $"the {next.Def.Name} isn't turned the solver's way after {path.Count} clicks"; break; }

                // Aim at the column (the game centres the item on the pointed-at cell), then wheel to the shelf.
                var size = next.Shape.Size;
                int cx = min.x + (size.x - 1) / 2, cz = min.z + (size.z - 1) / 2;
                var trunk = game.CurrentVehicle.transform;
                bool aimed = false;
                for (int y = game.TrunkSize.y; y >= 0 && !aimed; y--)
                {
                    var s = game.Camera.WorldToScreenPoint(trunk.TransformPoint(new Vector3(cx + 0.5f, y, cz + 0.5f)));
                    if (!game.PreviewTarget(new Vector2(s.x, s.y), out var at, out _) || at.x != min.x || at.z != min.z) continue;
                    InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = new Vector2(s.x, s.y) });
                    yield return null;
                    yield return null;
                    yield return Wait(0.1f);
                    aimed = game.CurrentTarget(out var now, out _) && now.x == min.x && now.z == min.z;
                }
                if (!aimed) { failure = $"couldn't aim the {next.Def.Name} at {min}"; break; }
                for (int tries = 0; tries < 8 && game.CurrentTarget(out var pos, out _) && pos.y != min.y; tries++)
                {
                    yield return Scroll(pos.y < min.y ? 120f : -120f);
                    wheel++;
                }
                game.CurrentTarget(out var target, out bool valid);
                if (target != min || !valid) { failure = $"the {next.Def.Name}'s ghost is at {target} ({(valid ? "fits" : "won't fit")}), not {min}"; break; }
                yield return Click();
                yield return Wait(0.45f);
                if (next.GridPos != min || (next.State != ItemState.Packed && next.State != ItemState.Dropping)) { failure = $"clicking didn't drop the {next.Def.Name} at {min}"; break; }
                placed++;
            }
            yield return Wait(0.6f);
            yield return Shot("mouse-only-packed");
            if (failure == null)
            {
                yield return ClickUi("Close");
                for (float t = 0f; t < 9f && !game.IsShowingResults; t += Time.unscaledDeltaTime) yield return null;
                yield return Wait(1.2f);
            }
            Check(failure == null && placed == placements.Count && game.IsShowingResults && game.LastStars == 3,
                $"mouse only: Weekend Getaway packed {placed}/{placements.Count} with clicks and the wheel ({clicks} hint clicks, {tipsRolls} of them tip or roll; {wheel} wheel steps) and closed for {game.LastStars} stars"
                + (failure != null ? $" (stopped: {failure})" : ""));
            GameController.AutoForgetTrunk("weekend");
            GameSettings.Tips = tipsWere;
            Uncap();
        }

        /// <summary>
        /// Mouse only, round 9: REDO is a button (shown only while there's something to redo), the held
        /// thing stays over the trunk while you click a key hint, and the wheel picks a shelf.
        /// </summary>
        IEnumerator MouseRedoChecks(Dictionary<string, List<(string, List<Vector3Int>)>> solutions)
        {
            PerfProbe.Begin("playing");
            bool tipsWere = GameSettings.Tips;
            GameSettings.Tips = false;
            Uncap();
            int weekend = LevelIndex("weekend");
            game.AutoStartLevel(weekend);
            yield return Wait(2.5f);
            var trunk = game.CurrentVehicle.transform;
            bool hiddenAtStart = !game.Ui.RedoShowing;

            // 1. A steady hand: aim into the empty trunk, click the tip hint, and the ghost stays over the
            // same spot with the tipped shape; a click back on the trunk drops it there.
            var size = game.TrunkSize;
            PackItem item = null;
            foreach (var candidate in game.Items.Where(i => i.State == ItemState.Pile && i.Def.Shape.Voxels.Length > 1))
            {
                var tipped = candidate.Def.Shape.Rotated(Quaternion.AngleAxis(90f, game.Rig.SnappedRight())).Size;
                if (tipped != candidate.Def.Shape.Size && tipped.x <= size.x && tipped.y <= size.y && tipped.z <= size.z) { item = candidate; break; }
            }
            if (item == null) { Check(false, "mouse only: Weekend Getaway has something that changes shape when tipped"); yield break; }
            game.AutoHold(item);
            yield return Wait(0.2f);
            var aimCell = new Vector3Int(size.x / 2, 0, size.z / 2);
            var aimWorld = trunk.TransformPoint(new Vector3(aimCell.x + 0.5f, 0.02f, aimCell.z + 0.5f));
            yield return MoveMouse(aimWorld);
            yield return Wait(0.2f);
            bool aimed = game.CurrentTarget(out var before, out _);
            var shapeBefore = item.Shape.Size;
            yield return ClickUi(HintChip(Bindings.Action.Tip));
            yield return Wait(0.3f);
            bool overHud = game.Ui.PointerOverUi;
            bool still = game.CurrentTarget(out var after, out bool afterValid);
            var s = item.Shape.Size;
            bool covers = after.x <= aimCell.x && aimCell.x < after.x + s.x && after.z <= aimCell.z && aimCell.z < after.z + s.z;
            var itemPos = trunk.InverseTransformPoint(item.transform.position);
            bool inTrunk = itemPos.x > -1f && itemPos.x < size.x + 1f && itemPos.z > -1f && itemPos.z < size.z + 1f;
            yield return Shot("mouse-only-steady-hand");
            yield return MoveMouse(aimWorld);
            yield return Wait(0.2f);
            game.CurrentTarget(out var dropAt, out bool dropValid);
            yield return Click();
            yield return Wait(0.5f);
            bool dropped = item.State == ItemState.Packed || item.State == ItemState.Dropping;
            Check(aimed && overHud && still && s != shapeBefore && covers && inTrunk && dropValid && dropped && item.GridPos == dropAt,
                $"mouse only: holding the {item.Def.Name} over the trunk ({before}) and clicking the tip hint keeps its ghost there ({after}, {(afterValid ? "fits" : "won't fit")}, tipped {shapeBefore} -> {s}); a click back on the trunk drops it at {item.GridPos}");

            // 2. REDO: hidden until there's something to redo, then a click redoes exactly what Shift + Z does.
            yield return ClickUi("Undo");
            yield return Wait(0.4f);
            bool shownAfterUndo = game.Ui.RedoShowing;
            yield return Shot("mouse-only-redo");
            yield return Press(Key.Z, Key.LeftShift);
            yield return Wait(0.4f);
            var byKey = (item.State, item.GridPos, item.Orientation);
            yield return ClickUi("Undo");
            yield return Wait(0.4f);
            bool backOnBlanket = item.State == ItemState.Pile;
            yield return ClickUi("Redo");
            yield return Wait(0.5f);
            var byClick = (item.State, item.GridPos, item.Orientation);
            bool same = byKey.Item1 == byClick.Item1 && byKey.Item2 == byClick.Item2 && Quaternion.Angle(byKey.Item3, byClick.Item3) < 1f
                && (byClick.Item1 == ItemState.Packed || byClick.Item1 == ItemState.Dropping);
            Check(hiddenAtStart && shownAfterUndo && backOnBlanket && same && !game.Ui.RedoShowing,
                $"mouse only: REDO is hidden on a fresh trip (hidden {hiddenAtStart}), shows after UNDO ({shownAfterUndo}), and a click puts the {item.Def.Name} back exactly as Shift + Z does ({byClick.Item2}); then it hides again");

            // 3. The wheel picks a shelf (owed since round 8: Weekend Getaway never needed it). Build a covered
            // gap on First Snow as the see-through checks do, hold a small thing over it and wheel down and up.
            game.AutoStartLevel(LevelIndex("snow"));
            yield return Wait(2.5f);
            trunk = game.CurrentVehicle.transform;
            var smalls = game.Items.Where(i => i.Def.Shape.Voxels.Length == 1 && !i.Def.Fragile).ToList();
            PackItem plank = null;
            Quaternion plankTurn = Quaternion.identity;
            foreach (var candidate in game.Items.Where(i => i.Def.Shape.Voxels.Length >= 2 && !i.Def.Fragile))
            {
                var flat = candidate.Def.Shape.Orientations().FirstOrDefault(o => o.Shape.Size.y == 1 && o.Shape.Size.z == 1 && o.Shape.Size.x >= 2);
                if (flat.Shape == null) continue;
                plank = candidate;
                plankTurn = flat.Rotation;
                break;
            }
            bool built = false;
            var gap = Vector3Int.zero;
            if (smalls.Count >= 3 && plank != null)
                for (int z = 1; z < game.TrunkSize.z && !built; z++)
                for (int x = 0; x + 1 < game.TrunkSize.x && !built; x++)
                {
                    if (!game.AutoPlace(smalls[0], Quaternion.identity, new Vector3Int(x, 0, z))) continue;
                    if (game.AutoPlace(plank, plankTurn, new Vector3Int(x, 1, z)))
                    {
                        if (game.AutoPlace(smalls[1], Quaternion.identity, new Vector3Int(x + 1, 0, z - 1)))
                        {
                            built = true;
                            gap = new Vector3Int(x + 1, 0, z);
                            break;
                        }
                        game.AutoPutBackToPile(plank);
                    }
                    game.AutoPutBackToPile(smalls[0]);
                }
            if (!built) { Check(false, "mouse only: First Snow has the pieces for a covered gap"); yield break; }
            yield return Wait(0.8f);
            yield return MoveMouse(Top(smalls[2]));
            yield return Wait(0.1f);
            yield return Click();
            yield return Wait(0.3f);
            yield return MoveMouse(trunk.TransformPoint(new Vector3(gap.x + 0.5f, 2f, gap.z + 0.5f)));
            yield return Wait(0.3f);
            game.CurrentTarget(out var top, out _);
            var pos = top;
            int down = 0;
            for (int i = 0; i < 4 && pos.y > 0; i++)
            {
                yield return Scroll(-120f);
                down++;
                game.CurrentTarget(out pos, out _);
            }
            var tucked = pos;
            yield return Scroll(120f);
            game.CurrentTarget(out var up, out _);
            Check(game.Held == smalls[2] && top.y > 0 && tucked == gap && up.y > 0,
                $"mouse only: the wheel picks a shelf: over the {plank.Def.Name} at {top}, {down} wheel step(s) down tucks it into the gap ({tucked}), one up puts it back on top ({up})");
            yield return Press(Key.Escape);
            yield return Wait(0.3f);
            GameController.AutoForgetTrunk("weekend");
            GameController.AutoForgetTrunk("snow");
            GameSettings.Tips = tipsWere;
            Uncap();
        }

        static Vector3 Top(PackItem i) => i.transform.position + (Vector3)i.Shape.Center + Vector3.up * (i.Shape.Size.y * 0.5f - 0.1f);
    }
}
