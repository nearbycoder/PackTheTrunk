using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>One item's spot in a complete packing: which item, where, and turned which way.</summary>
    public class SolvedPlacement
    {
        public ItemDef Def;
        public Vector3Int Min;
        public Quaternion Rotation;
        public VoxelShape Shape;
        public HashSet<Vector3Int> Cells;
    }

    /// <summary>
    /// The solver's 100% packing for every trip (Resources/PackTheTrunkSolutions.txt, written by
    /// <c>python3 Tools/solve_levels.py --dump Assets/Resources/PackTheTrunkSolutions.txt</c>), used for
    /// Grandpa's hints. Each trip also gets the mirror images of its solution when the trunk is
    /// symmetric, so a player who started on the other side still gets hints that match.
    /// </summary>
    public static class Solutions
    {
        static Dictionary<string, List<(string, List<Vector3Int>)>> raw;
        static readonly Dictionary<string, List<List<SolvedPlacement>>> cache = new Dictionary<string, List<List<SolvedPlacement>>>();

        /// <summary>Parse "level \t item \t x,y,z;x,y,z..." lines.</summary>
        public static Dictionary<string, List<(string, List<Vector3Int>)>> Parse(string text)
        {
            var result = new Dictionary<string, List<(string, List<Vector3Int>)>>();
            foreach (var line in text.Split('\n'))
            {
                var parts = line.Trim('\r').Split('\t');
                if (parts.Length != 3) continue;
                var cells = parts[2].Split(';').Select(c =>
                {
                    var n = c.Split(',').Select(int.Parse).ToArray();
                    return new Vector3Int(n[0], n[1], n[2]);
                }).ToList();
                if (!result.TryGetValue(parts[0], out var list)) result[parts[0]] = list = new List<(string, List<Vector3Int>)>();
                list.Add((parts[1], cells));
            }
            return result;
        }

        /// <summary>A favour's packing, as it was made, takes the place of a shipped solution for its id.</summary>
        public static void Register(LevelDef level)
        {
            if (level.Packing == null) return;
            LoadShipped();
            raw[level.Id] = level.Packing;
            cache.Remove(level.Id);
        }

        /// <summary>Every known complete packing of this trip (the solver's first), or none if the data doesn't match.</summary>
        public static IReadOnlyList<List<SolvedPlacement>> For(LevelDef level)
        {
            if (cache.TryGetValue(level.Id, out var known)) return known;
            var variants = new List<List<SolvedPlacement>>();
            cache[level.Id] = variants;

            LoadShipped();
            if (!raw.TryGetValue(level.Id, out var placements))
            {
                Debug.LogWarning($"[Hints] no solution shipped for {level.Id}");
                return variants;
            }

            var size = level.Size;
            var mirrors = new List<System.Func<Vector3Int, Vector3Int>>
            {
                c => c,
                c => new Vector3Int(size.x - 1 - c.x, c.y, c.z),
                c => new Vector3Int(c.x, c.y, size.z - 1 - c.z),
                c => new Vector3Int(size.x - 1 - c.x, c.y, size.z - 1 - c.z),
            };
            for (int m = 0; m < mirrors.Count; m++)
            {
                var map = mirrors[m];
                // A mirror only counts if the trunk itself (wheel wells, hatch glass...) looks the same.
                if (m > 0 && !level.Blocked.All(b => level.Blocked.Contains(map(b)))) continue;
                var variant = Build(level, placements.Select(p => (p.Item1, p.Item2.Select(map).ToList())).ToList(), m == 0);
                if (variant != null && !variants.Any(v => Same(v, variant))) variants.Add(variant);
            }
            if (variants.Count == 0) Debug.LogWarning($"[Hints] the shipped solution for {level.Id} doesn't match the level data; no hints for this trip");
            return variants;
        }

        static void LoadShipped()
        {
            if (raw != null) return;
            var asset = Resources.Load<TextAsset>("PackTheTrunkSolutions");
            raw = asset != null ? Parse(asset.text) : new Dictionary<string, List<(string, List<Vector3Int>)>>();
        }

        /// <summary>Check a solution against the level (items, bounds, walls, overlaps, shapes) and work out each rotation.</summary>
        static List<SolvedPlacement> Build(LevelDef level, List<(string Id, List<Vector3Int> Cells)> placements, bool report)
        {
            var wanted = level.Required.Concat(level.Bonus).GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.Count());
            var given = placements.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.Count());
            if (wanted.Count != given.Count || wanted.Any(kv => !given.TryGetValue(kv.Key, out var n) || n != kv.Value))
            {
                if (report) Debug.LogWarning($"[Hints] {level.Id}: the solution packs different items than the level");
                return null;
            }

            var used = new HashSet<Vector3Int>(level.Blocked);
            var result = new List<SolvedPlacement>();
            foreach (var (id, cells) in placements)
            {
                var def = level.Required.Concat(level.Bonus).First(d => d.Id == id);
                var min = cells.Aggregate(Vector3Int.Min);
                var local = new HashSet<Vector3Int>(cells.Select(c => c - min));
                var match = def.Shape.Orientations().FirstOrDefault(o => o.Shape.Voxels.Length == local.Count && o.Shape.Voxels.All(v => local.Contains(v.Pos)));
                if (match.Shape == null)
                {
                    if (report) Debug.LogWarning($"[Hints] {level.Id}: {id} isn't any turn of its shape");
                    return null;
                }
                foreach (var c in cells)
                {
                    bool inside = c.x >= 0 && c.y >= 0 && c.z >= 0 && c.x < level.Size.x && c.y < level.Size.y && c.z < level.Size.z;
                    if (!inside || !used.Add(c))
                    {
                        if (report) Debug.LogWarning($"[Hints] {level.Id}: {id} is outside the trunk or overlaps at {c}");
                        return null;
                    }
                }
                result.Add(new SolvedPlacement { Def = def, Min = min, Rotation = match.Rotation, Shape = match.Shape, Cells = new HashSet<Vector3Int>(cells) });
            }
            return result;
        }

        static bool Same(List<SolvedPlacement> a, List<SolvedPlacement> b) =>
            a.Count == b.Count && a.All(p => b.Any(q => q.Def.Id == p.Def.Id && q.Cells.SetEquals(p.Cells)));
    }
}
