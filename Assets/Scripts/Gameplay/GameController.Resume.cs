using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// Your trunk waits for you: every change to it is saved for that trip, so leaving for the map,
    /// quitting, or a crash never throws a half-packed trunk away. Starting the trip again puts every
    /// packed thing back where it was. Closing the trunk clears it.
    /// </summary>
    public partial class GameController
    {
        // "1|<hinted 0/1>|<item index>:<item id>:x:y:z:ex:ey:ez;..." for the packed items only.
        static string TrunkKey(string levelId) => "ptt.trunk." + levelId;

        bool trunkResumeChecked;

        /// <summary>Write the trunk as it stands (an item in your hand counts where it was picked up from).</summary>
        void SaveTrunk()
        {
            if (mode != Mode.Playing || level == null) return;
            var snapshot = held != null && pendingSnapshot != null ? pendingSnapshot : Capture();
            string key = TrunkKey(level.Id);
            if (!snapshot.Any(s => s.Packed))
            {
                // Nothing packed is the same as a fresh start (RESTART clears the hint mark too).
                if (Prefs.HasKey(key))
                {
                    Prefs.DeleteKey(key);
                    Prefs.Save();
                }
                return;
            }
            var sb = new StringBuilder("1|").Append(hintedThisTry ? '1' : '0').Append('|');
            var inv = CultureInfo.InvariantCulture;
            bool first = true;
            foreach (var s in snapshot)
            {
                if (!s.Packed) continue;
                var e = s.Orientation.eulerAngles;
                if (!first) sb.Append(';');
                first = false;
                sb.Append(items.IndexOf(s.Item).ToString(inv)).Append(':').Append(s.Item.Def.Id)
                  .Append(':').Append(s.Pos.x.ToString(inv)).Append(':').Append(s.Pos.y.ToString(inv)).Append(':').Append(s.Pos.z.ToString(inv))
                  .Append(':').Append(Quarter(e.x)).Append(':').Append(Quarter(e.y)).Append(':').Append(Quarter(e.z));
            }
            Prefs.SetString(key, sb.ToString());
            Prefs.Save();
        }

        static string Quarter(float angle) => (Mathf.RoundToInt(angle / 90f) * 90 % 360).ToString(CultureInfo.InvariantCulture);

        static void ForgetTrunk(string levelId) => Prefs.DeleteKey(TrunkKey(levelId));

        /// <summary>How many things are packed in a trip's saved trunk (0 if none).</summary>
        static int SavedTrunkCount(string levelId)
        {
            var data = Prefs.GetString(TrunkKey(levelId), "");
            var parts = data.Split('|');
            return parts.Length == 3 && parts[0] == "1" && parts[2].Length > 0 ? parts[2].Split(';').Length : 0;
        }

        /// <summary>
        /// Put a saved trunk back, once per trip start, and return how many things went back in. All or
        /// nothing: a trunk that doesn't fit this version of the level is dropped.
        /// </summary>
        int RestoreSavedTrunk()
        {
            if (trunkResumeChecked) return 0;
            trunkResumeChecked = true;
            string key = TrunkKey(level.Id);
            var data = Prefs.GetString(key, "");
            if (string.IsNullOrEmpty(data)) return 0;

            var parts = data.Split('|');
            var wanted = new List<(PackItem item, Vector3Int pos, Quaternion rot)>();
            bool ok = parts.Length == 3 && parts[0] == "1" && parts[2].Length > 0;
            var inv = CultureInfo.InvariantCulture;
            if (ok)
                foreach (var entry in parts[2].Split(';'))
                {
                    var f = entry.Split(':');
                    var n = new int[7];
                    if (f.Length != 8 || !int.TryParse(f[0], NumberStyles.Integer, inv, out n[0])
                        || !Enumerable.Range(2, 6).All(i => int.TryParse(f[i], NumberStyles.Integer, inv, out n[i - 1])))
                    {
                        ok = false;
                        break;
                    }
                    if (n[0] < 0 || n[0] >= items.Count || items[n[0]].Def.Id != f[1] || wanted.Any(w => w.item == items[n[0]]))
                    {
                        ok = false;
                        break;
                    }
                    wanted.Add((items[n[0]], new Vector3Int(n[1], n[2], n[3]), Quaternion.Euler(n[4], n[5], n[6])));
                }

            // Place in passes until nothing more goes in: whatever holds something up goes in first.
            var placed = new List<PackItem>();
            if (ok)
            {
                var left = new List<(PackItem item, Vector3Int pos, Quaternion rot)>(wanted);
                for (bool progress = true; progress && left.Count > 0;)
                {
                    progress = false;
                    for (int i = left.Count - 1; i >= 0; i--)
                    {
                        var (item, pos, rot) = left[i];
                        item.SetOrientation(rot);
                        if (grid.Check(item.Shape, pos, item.Def.Fragile, out _) != PlacementResult.Ok) continue;
                        grid.Place(item, pos);
                        item.GridPos = pos;
                        placed.Add(item);
                        left.RemoveAt(i);
                        progress = true;
                    }
                }
                ok = left.Count == 0;
            }

            if (!ok)
            {
                foreach (var item in placed) grid.Remove(item);
                foreach (var item in items) if (item.Orientation != Quaternion.identity) item.SetOrientation(Quaternion.identity);
                Prefs.DeleteKey(key);
                Prefs.Save();
                Debug.LogWarning($"[Resume] {level.Id}: the saved trunk doesn't fit this version of the trip, starting fresh");
                return 0;
            }

            foreach (var item in placed)
            {
                item.State = ItemState.Packed;
                item.SetColliderEnabled(true);
                item.MoveTo(item.GridPos, false, 9f);
            }
            hintedThisTry = parts[1] == "1";
            starsAnnounced = CurrentStars();
            sfx.PutBack(vehicle.transform.position);
            Debug.Log($"[Resume] {level.Id}: put back {placed.Count} packed items{(hintedThisTry ? " (hinted)" : "")}");
            return placed.Count;
        }
    }
}
