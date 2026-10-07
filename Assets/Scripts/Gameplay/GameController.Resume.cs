using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// Your trunk waits for you: every change to it is saved for that trip, with its undo and redo
    /// history, so leaving for the map, quitting, or a crash never throws a half-packed trunk (or the
    /// way back through it) away. Starting the trip again puts every packed thing back where it was,
    /// and Z / Shift+Z carry on where they left off. Closing the trunk clears it.
    /// </summary>
    public partial class GameController
    {
        // v2: "2|<hinted 0/1>|<items signature>|<packed>|<undo steps>|<redo steps>"
        //   packed: "<item index>:<item id>:x:y:z:ex:ey:ez;..." for the packed items only
        //   steps:  oldest first, "/"-separated, each "<flags>!<item index>:x:y:z:ex:ey:ez;..." (what was
        //           packed at that step); flags: r = a RESTART, h = a RESTART that cleared the hint mark
        // v1 (round 4): "1|<hinted 0/1>|<packed>", no history.
        static string TrunkKey(string levelId) => "ptt.trunk." + levelId;

        // Undo and redo steps kept with the saved trunk (newest first); older ones are only in memory.
        const int HistoryKept = 50;

        bool trunkResumeChecked;

        /// <summary>Write the trunk as it stands (an item in your hand counts where it was picked up from).</summary>
        void SaveTrunk()
        {
            if (mode != Mode.Playing || level == null) return;
            var snapshot = held != null && pendingSnapshot != null ? pendingSnapshot : Capture();
            string key = TrunkKey(level.Id);
            // Nothing packed and no way back to a packing is a fresh start; so is an empty trunk after
            // asking Grandpa, as it always was (RESTART clears the hint mark too).
            if (!snapshot.Any(s => s.Packed) && (hintedThisTry || undo.Count + redo.Count == 0))
            {
                if (Prefs.HasKey(key))
                {
                    Prefs.DeleteKey(key);
                    Prefs.Save();
                }
                return;
            }
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("2|").Append(hintedThisTry ? '1' : '0').Append('|').Append(ItemsSignature()).Append('|');
            for (int i = 0, n = 0; i < snapshot.Count; i++)
            {
                if (!snapshot[i].Packed) continue;
                if (n++ > 0) sb.Append(';');
                sb.Append(i.ToString(inv)).Append(':').Append(snapshot[i].Item.Def.Id).Append(':');
                AppendPlacement(sb, snapshot[i]);
            }
            sb.Append('|');
            AppendSteps(sb, undo.Take(HistoryKept).Reverse().Select(s => (s, restartSteps.Contains(s), restartsAfterHint.Contains(s))));
            sb.Append('|');
            AppendSteps(sb, redo.Take(HistoryKept).Reverse().Select(r => (r.Step, r.Restart, false)));
            Prefs.SetString(key, sb.ToString());
            Prefs.Save();
        }

        static void AppendSteps(StringBuilder sb, IEnumerable<(List<SavedItem> Step, bool Restart, bool AfterHint)> steps)
        {
            var inv = CultureInfo.InvariantCulture;
            bool firstStep = true;
            foreach (var (step, restart, afterHint) in steps)
            {
                if (!firstStep) sb.Append('/');
                firstStep = false;
                sb.Append(restart ? (afterHint ? "rh" : "r") : "-").Append('!');
                for (int i = 0, n = 0; i < step.Count; i++)
                {
                    if (!step[i].Packed) continue;
                    if (n++ > 0) sb.Append(';');
                    sb.Append(i.ToString(inv)).Append(':');
                    AppendPlacement(sb, step[i]);
                }
            }
        }

        static void AppendPlacement(StringBuilder sb, SavedItem s)
        {
            var inv = CultureInfo.InvariantCulture;
            var e = s.Orientation.eulerAngles;
            sb.Append(s.Pos.x.ToString(inv)).Append(':').Append(s.Pos.y.ToString(inv)).Append(':').Append(s.Pos.z.ToString(inv))
              .Append(':').Append(Quarter(e.x)).Append(':').Append(Quarter(e.y)).Append(':').Append(Quarter(e.z));
        }

        static string Quarter(float angle) => (Mathf.RoundToInt(angle / 90f) * 90 % 360).ToString(CultureInfo.InvariantCulture);

        /// <summary>The trip's item list, so history saved by another version of the level is never applied to the wrong things.</summary>
        string ItemsSignature()
        {
            uint hash = 2166136261;
            foreach (var item in items)
                foreach (char c in item.Def.Id + ",")
                    hash = (hash ^ c) * 16777619;
            return items.Count.ToString(CultureInfo.InvariantCulture) + "-" + hash.ToString("x8", CultureInfo.InvariantCulture);
        }

        static void ForgetTrunk(string levelId) => Prefs.DeleteKey(TrunkKey(levelId));

        /// <summary>How many things are packed in a trip's saved trunk (0 if none).</summary>
        static int SavedTrunkCount(string levelId)
        {
            var parts = Prefs.GetString(TrunkKey(levelId), "").Split('|');
            string packed = parts.Length == 3 && parts[0] == "1" ? parts[2] : parts.Length == 6 && parts[0] == "2" ? parts[3] : "";
            return packed.Length > 0 ? packed.Split(';').Length : 0;
        }

        /// <summary>A saved trunk, read and checked against this trip: what's packed, the hint mark, and the history.</summary>
        class SavedTrunkData
        {
            public bool Hinted;
            public List<SavedItem> Packed;
            public readonly List<(List<SavedItem> Step, bool Restart, bool AfterHint)> Undo = new List<(List<SavedItem>, bool, bool)>();
            public readonly List<(List<SavedItem> Step, bool Restart)> Redo = new List<(List<SavedItem>, bool)>();
            public int PackedCount => Packed.Count(s => s.Packed);
        }

        /// <summary>
        /// Read the trip's saved trunk for the level that's built. Null if there isn't one; a saved trunk
        /// that doesn't fit this version of the trip (any step of it) is deleted with a log line.
        /// </summary>
        SavedTrunkData ReadSavedTrunk()
        {
            string key = TrunkKey(level.Id);
            var data = Prefs.GetString(key, "");
            if (string.IsNullOrEmpty(data)) return null;
            var result = ParseTrunk(data);
            if (result != null && LayoutFits(result.Packed) && result.Undo.All(u => LayoutFits(u.Step)) && result.Redo.All(r => LayoutFits(r.Step)))
                return result;
            Prefs.DeleteKey(key);
            Prefs.Save();
            Debug.LogWarning($"[Resume] {level.Id}: the saved trunk doesn't fit this version of the trip, starting fresh");
            return null;
        }

        SavedTrunkData ParseTrunk(string data)
        {
            var parts = data.Split('|');
            var result = new SavedTrunkData();
            if (parts.Length == 3 && parts[0] == "1")
            {
                result.Hinted = parts[1] == "1";
                result.Packed = ParseStep(parts[2], true);
                return result.Packed != null && result.PackedCount > 0 ? result : null;
            }
            if (parts.Length != 6 || parts[0] != "2" || parts[2] != ItemsSignature()) return null;
            result.Hinted = parts[1] == "1";
            result.Packed = ParseStep(parts[3], true);
            if (result.Packed == null) return null;
            foreach (var step in parts[4].Length > 0 ? parts[4].Split('/') : new string[0])
            {
                var f = step.Split('!');
                var layout = f.Length == 2 ? ParseStep(f[1], false) : null;
                if (layout == null) return null;
                result.Undo.Add((layout, f[0].StartsWith("r"), f[0] == "rh"));
            }
            foreach (var step in parts[5].Length > 0 ? parts[5].Split('/') : new string[0])
            {
                var f = step.Split('!');
                var layout = f.Length == 2 ? ParseStep(f[1], false) : null;
                if (layout == null) return null;
                result.Redo.Add((layout, f[0].StartsWith("r")));
            }
            return result;
        }

        /// <summary>One layout ("index[:id]:x:y:z:ex:ey:ez;..."), as a full snapshot of the trip's items (null if malformed).</summary>
        List<SavedItem> ParseStep(string text, bool withIds)
        {
            var snapshot = items.Select(i => new SavedItem { Item = i, Orientation = Quaternion.identity }).ToList();
            if (text.Length == 0) return snapshot;
            var inv = CultureInfo.InvariantCulture;
            int skip = withIds ? 1 : 0;
            foreach (var entry in text.Split(';'))
            {
                var f = entry.Split(':');
                var n = new int[7];
                if (f.Length != 7 + skip || !int.TryParse(f[0], NumberStyles.Integer, inv, out n[0])
                    || !Enumerable.Range(1, 6).All(i => int.TryParse(f[i + skip], NumberStyles.Integer, inv, out n[i])))
                    return null;
                if (n[0] < 0 || n[0] >= items.Count || snapshot[n[0]].Packed || (withIds && items[n[0]].Def.Id != f[1])) return null;
                snapshot[n[0]] = new SavedItem
                {
                    Item = items[n[0]],
                    Packed = true,
                    Pos = new Vector3Int(n[1], n[2], n[3]),
                    Orientation = Quaternion.Euler(n[4], n[5], n[6]),
                };
            }
            return snapshot;
        }

        /// <summary>
        /// Could this layout stand in the empty trunk under the game's rules? Placed in passes until nothing
        /// more goes in, so whatever holds something up goes in first.
        /// </summary>
        bool LayoutFits(List<SavedItem> layout)
        {
            var scratch = new TrunkGrid(level.Size, level.Blocked);
            var left = layout.Where(s => s.Packed).Select(s => (s, shape: s.Item.Def.Shape.Rotated(s.Orientation))).ToList();
            for (bool progress = true; progress && left.Count > 0;)
            {
                progress = false;
                for (int i = left.Count - 1; i >= 0; i--)
                {
                    var (s, shape) = left[i];
                    if (scratch.Check(shape, s.Pos, s.Item.Def.Fragile, out _) != PlacementResult.Ok) continue;
                    scratch.Place(s.Item, shape, s.Pos);
                    left.RemoveAt(i);
                    progress = true;
                }
            }
            return left.Count == 0;
        }

        /// <summary>Put the packed things of a layout into the (empty) trunk, gliding there or straight away.</summary>
        void PlaceLayout(List<SavedItem> layout, bool instant)
        {
            foreach (var s in layout)
            {
                if (!s.Packed) continue;
                var item = s.Item;
                item.SetOrientation(s.Orientation);
                grid.Place(item, s.Pos);
                item.GridPos = s.Pos;
                item.State = ItemState.Packed;
                item.SetColliderEnabled(true);
                item.MoveTo(s.Pos, instant, 9f);
            }
        }

        /// <summary>
        /// Put a saved trunk back, once per trip start, with its undo and redo history and the hint mark.
        /// Returns how many things went back in, or -1 if only the history came back (an emptied trunk).
        /// </summary>
        int RestoreSavedTrunk()
        {
            if (trunkResumeChecked) return 0;
            trunkResumeChecked = true;
            var saved = ReadSavedTrunk();
            if (saved == null) return 0;

            PlaceLayout(saved.Packed, false);
            foreach (var (step, restart, afterHint) in saved.Undo)
            {
                undo.Push(step);
                if (restart) restartSteps.Add(step);
                if (afterHint) restartsAfterHint.Add(step);
            }
            foreach (var r in saved.Redo) redo.Push(r);
            hintedThisTry = saved.Hinted;
            starsAnnounced = CurrentStars();
            int packed = saved.PackedCount;
            if (packed > 0) sfx.PutBack(vehicle.transform.position);
            Debug.Log($"[Resume] {level.Id}: put back {packed} packed items{(hintedThisTry ? " (hinted)" : "")}, {undo.Count} undo / {redo.Count} redo steps");
            return packed > 0 ? packed : undo.Count + redo.Count > 0 ? -1 : 0;
        }
    }
}
