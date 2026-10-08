using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// Favours for the neighbours: a new pile for a car you've already packed. The pile is made by packing it, under
    /// the game's own rules (<see cref="TrunkGrid"/>), so a complete packing of it always exists; that packing is what
    /// Grandpa hints. Things come only from trips you've closed, so a favour never spoils the story. A favour is saved
    /// as it was made (<see cref="Serialize"/>), so leaving, quitting or an update to the trips never changes its pile.
    /// </summary>
    public static class Favours
    {
        /// <summary>Trunks smaller than this (the wagon, the Mini) are too small for a good pile.</summary>
        public const int MinFreeCells = 24;
        public const int MinItems = 6, MaxItems = 22;
        public const float MinFill = 0.82f, MaxFill = 0.95f;

        /// <summary>The trip whose closing opens the neighbours' page (the end of chapter II).</summary>
        public const string UnlockedBy = "everything";

        static readonly (string Name, string Where)[] Neighbours =
        {
            ("Mrs. Alvarez", "next door"),
            ("Mr. Pickering", "across the street"),
            ("The Okafors", "two doors down"),
            ("Coach Dana", "on the corner"),
            ("Mr. Haskins", "at the end of the street"),
            ("Priya", "across the street"),
            ("The Nguyens", "behind the hedge"),
            ("Bea from the bakery", "on the corner"),
            ("Pastor Lin", "up the hill"),
            ("Mr. Fitz", "with the blue door"),
        };

        static readonly (string Title, string Place, string Ask)[] Errands =
        {
            ("The Flea Market", "the Flea Market", "The flea market opens at eight and none of this is going to fit by itself."),
            ("The Jumble Sale", "the Church Hall", "It's all for the jumble sale. Some of it is older than me."),
            ("Off to Camp", "Camp Starfish", "Dropping the kids at camp. They packed for a month. It's a week."),
            ("The Craft Fair", "the Craft Fair", "I've got a table at the craft fair and a car that's too small for it."),
            ("Moving Day", "the New Apartment", "Moving day! The van fell through, so it's just my car and your magic."),
            ("The Beach Trip", "Pebble Bay", "We're off to the beach and the car is already complaining."),
            ("The Science Fair", "the School Gym", "The science fair is tomorrow. Please don't ask about the volcano."),
            ("The Big Picnic", "Willow Park", "The whole street's picnic is in my car. No pressure."),
            ("The Yard Sale", "the Yard Sale", "Yard sale on Sunday. Turns out I own a lot of yard."),
            ("The Road Trip", "Route 9", "Road trip to see my sister. She asked us to bring 'a few things'."),
            ("The Talent Show", "the Village Hall", "Props for the talent show. Don't ask what my act is."),
            ("Visiting Grandma", "Grandma's", "Visiting my grandmother, who asked for everything in the attic."),
        };

        static readonly string[] Thanks =
        {
            "Everyone says you can make anything fit. I'll owe you a pie.",
            "I hear you're the one who can make anything fit. Lemonade's on me.",
            "Your grandpa always said you had the knack. Help?",
            "I've tried three times. It keeps not closing.",
        };

        /// <summary>Made in this session, by number, so TRY AGAIN and a waiting trunk always find the same pile.</summary>
        static readonly Dictionary<int, LevelDef> made = new Dictionary<int, LevelDef>();

        /// <summary>
        /// Make favour <paramref name="number"/>: a car from <paramref name="cars"/> (not <paramref name="avoidCar"/> if
        /// there's a choice) and a pile from <paramref name="pool"/>, packed into it. Deterministic for the same inputs.
        /// </summary>
        public static LevelDef Make(int number, IReadOnlyList<LevelDef> cars, IReadOnlyList<ItemDef> pool, string avoidCar = null)
        {
            var rng = new System.Random(number * 7919 + 17);
            var choices = cars.Where(c => c.FreeCells >= MinFreeCells).ToList();
            if (choices.Count > 1 && avoidCar != null) choices.RemoveAll(c => c.Id == avoidCar);
            if (choices.Count == 0) return null;
            var car = choices[rng.Next(choices.Count)];
            var items = pool.GroupBy(d => d.Id).Select(g => g.First()).OrderBy(d => d.Id, System.StringComparer.Ordinal).ToList();

            List<(ItemDef Def, Vector3Int Min, VoxelShape Shape)> best = null;
            float bestScore = float.MaxValue;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float target = Mathf.Lerp(0.84f, 0.94f, (float)rng.NextDouble());
                var packed = Pack(car, items, rng, target);
                float fill = packed.Sum(p => p.Shape.Voxels.Length) / (float)car.FreeCells;
                bool good = fill >= MinFill && fill <= MaxFill && packed.Count >= MinItems && packed.Count <= MaxItems;
                if (good) { best = packed; break; }
                // Otherwise keep the attempt closest to the range, in case none lands in it.
                float score = Mathf.Max(0f, MinFill - fill) + Mathf.Max(0f, fill - MaxFill) + Mathf.Max(0, MinItems - packed.Count) * 0.05f;
                if (score < bestScore) { bestScore = score; best = packed; }
            }
            if (best == null || best.Count < 3) return null;

            // Big things first: the biggest (about 60% of the volume) are the essentials, the rest extras, at least two.
            var order = best.Select(p => (p, Key: p.Shape.Voxels.Length + (float)rng.NextDouble() * 0.5f)).OrderByDescending(x => x.Key).Select(x => x.p).ToList();
            int total = order.Sum(p => p.Shape.Voxels.Length), essentials = 0, volume = 0;
            float share = Mathf.Lerp(0.52f, 0.66f, (float)rng.NextDouble());
            while (essentials < order.Count && volume < share * total) volume += order[essentials++].Shape.Voxels.Length;
            essentials = Mathf.Clamp(essentials, 2, order.Count - 2);

            var level = Shell(number, car);
            level.Packing = best.Select(p => (p.Def.Id, p.Shape.Voxels.Select(v => v.Pos + p.Min).ToList())).ToList();
            for (int i = 0; i < order.Count; i++) (i < essentials ? level.Required : level.Bonus).Add(order[i].Def);
            made[number] = level;
            return level;
        }

        /// <summary>The favour without its pile: who's asking, what for, and the car (the words follow the number alone).</summary>
        static LevelDef Shell(int number, LevelDef car)
        {
            var words = new System.Random(number * 104729 + 3);
            var who = Neighbours[words.Next(Neighbours.Length)];
            var errand = Errands[words.Next(Errands.Length)];
            return new LevelDef
            {
                Index = car.Index,
                Id = "favour-" + number.ToString(CultureInfo.InvariantCulture),
                Favour = number,
                ModelId = car.ModelId ?? car.Id,
                Title = errand.Title,
                Vehicle = car.Vehicle,
                Blurb = $"{who.Name} {who.Where} needs a hand.",
                Style = car.Style,
                BodyColor = car.BodyColor,
                Size = car.Size,
                Blocked = new HashSet<Vector3Int>(car.Blocked),
                Trip = $"{who.Where} · {errand.Place}",
                Sender = who.Name,
                PackFor = who.Name,
                Music = car.Music,
                Messages = new[] { $"Hi! {who.Name} here, {who.Where}.", errand.Ask, Thanks[words.Next(Thanks.Length)] },
                Epilogues = new[]
                {
                    $"It closed! The rest went on the back seat, and {who.Name} didn't mind a bit.",
                    $"Only a couple of things left on the driveway. {who.Name} waved all the way down the street.",
                    $"Every last thing fit. {who.Name} is telling the whole street about you.",
                },
                Medium = "phone",
            };
        }

        /// <summary>
        /// Pack a pile into the trunk, bottom first: at each empty cell in the solver's scan order (up, then back, then
        /// across), try things biggest-first with some luck, each in every turn, anchored on that cell; the first that the
        /// rules allow goes in. A cell nothing fits stays empty. Stops at the target fill or the item limit.
        /// </summary>
        static List<(ItemDef Def, Vector3Int Min, VoxelShape Shape)> Pack(LevelDef car, List<ItemDef> items, System.Random rng, float target)
        {
            var grid = new TrunkGrid(car.Size, car.Blocked);
            var placed = new List<(ItemDef, Vector3Int, VoxelShape)>();
            var counts = new Dictionary<string, int>();
            int filled = 0, fragiles = 0, free = car.FreeCells;
            var size = car.Size;
            for (int y = 0; y < size.y; y++)
            for (int z = 0; z < size.z; z++)
            for (int x = 0; x < size.x; x++)
            {
                var cell = new Vector3Int(x, y, z);
                if (filled >= target * free || placed.Count >= MaxItems) return placed;
                if (!grid.IsFree(cell)) continue;
                int room = free - filled;
                // Weighted shuffle (bigger things come first more often), at most two of anything and three fragile things.
                var candidates = items
                    .Where(d => d.Volume <= room && (!counts.TryGetValue(d.Id, out var n) || n < 2) && (!d.Fragile || fragiles < 3))
                    .Select(d => (d, Key: -System.Math.Pow(rng.NextDouble(), 1.0 / System.Math.Pow(d.Volume, 1.6))))
                    .OrderBy(t => t.Key).Take(40).Select(t => t.d).ToList();
                bool done = false;
                foreach (var def in candidates)
                {
                    var turns = def.Shape.Orientations().Select(o => o.Shape).OrderBy(_ => rng.Next()).ToList();
                    foreach (var shape in turns)
                    {
                        // Anchor the shape's first cell in scan order on this cell.
                        var first = shape.Voxels.Select(v => v.Pos).OrderBy(p => p.y).ThenBy(p => p.z).ThenBy(p => p.x).First();
                        var min = cell - first;
                        if (min.x < 0 || min.y < 0 || min.z < 0) continue;
                        if (grid.Check(shape, min, def.Fragile, out _) != PlacementResult.Ok) continue;
                        grid.PlaceData(def.Fragile, shape, min);
                        placed.Add((def, min, shape));
                        counts[def.Id] = counts.TryGetValue(def.Id, out var c) ? c + 1 : 1;
                        filled += shape.Voxels.Length;
                        if (def.Fragile) fragiles++;
                        done = true;
                        break;
                    }
                    if (done) break;
                }
            }
            return placed;
        }

        // ------------------------------------------------------------------ saving

        /// <summary>"1|number|car|req ids|bonus ids|packing" with packing "id:x,y,z;x,y,z/..." (what the favour is).</summary>
        public static string Serialize(LevelDef favour)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("1|").Append(favour.Favour.ToString(inv)).Append('|').Append(favour.ModelId).Append('|');
            sb.Append(string.Join(",", favour.Required.Select(d => d.Id))).Append('|');
            sb.Append(string.Join(",", favour.Bonus.Select(d => d.Id))).Append('|');
            sb.Append(string.Join("/", favour.Packing.Select(p => p.Id + ":" + string.Join(";", p.Cells.Select(c => $"{c.x.ToString(inv)},{c.y.ToString(inv)},{c.z.ToString(inv)}")))));
            return sb.ToString();
        }

        /// <summary>Read a saved favour back (null if it doesn't match the cars and things of this version).</summary>
        public static LevelDef Deserialize(string data, IReadOnlyList<LevelDef> levels, System.Func<string, ItemDef> item)
        {
            try
            {
                var parts = data.Split('|');
                if (parts.Length != 6 || parts[0] != "1") return null;
                int number = int.Parse(parts[1], CultureInfo.InvariantCulture);
                if (made.TryGetValue(number, out var known) && Serialize(known) == data) return known;
                var car = levels.FirstOrDefault(l => l.Id == parts[2]);
                if (car == null) return null;
                var shell = Shell(number, car);
                shell.Required = parts[3].Split(',').Where(s => s.Length > 0).Select(item).ToList();
                shell.Bonus = parts[4].Split(',').Where(s => s.Length > 0).Select(item).ToList();
                if (shell.Required.Any(d => d == null) || shell.Bonus.Any(d => d == null)) return null;
                shell.Packing = parts[5].Split('/').Select(p =>
                {
                    var bits = p.Split(':');
                    var cells = bits[1].Split(';').Select(c =>
                    {
                        var n = c.Split(',').Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                        return new Vector3Int(n[0], n[1], n[2]);
                    }).ToList();
                    return (bits[0], cells);
                }).ToList();
                made[number] = shell;
                return shell;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Favours] could not read the saved favour: " + e.Message);
                return null;
            }
        }
    }
}
