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
            ("Ms. Kowalski", "above the bakery"),
            ("The Brennans", "by the mailbox"),
            ("Old Mr. Tate", "at number 12"),
            ("Jules", "in the green house"),
            ("Dr. Osei", "by the park"),
            ("The Delgados", "with the trampoline"),
        };

        /// <summary>The neighbours take turns in this order, so nobody asks twice in a row.</summary>
        static readonly int[] Turns = { 3, 11, 0, 7, 14, 5, 9, 1, 12, 6, 15, 2, 8, 13, 4, 10 };

        /// <summary>
        /// What a neighbour needs a hand with. An errand with things listed suits a pile with at least three of them (a
        /// beach trip wants surfboards); one without fits any pile.
        /// </summary>
        static readonly (string Title, string Place, string Ask, string[] Things)[] Errands =
        {
            ("The Flea Market", "the Flea Market", "The flea market opens at eight and none of this is going to fit by itself.", null),
            ("The Jumble Sale", "the Church Hall", "It's all for the jumble sale. Some of it is older than me.", null),
            ("The Craft Fair", "the Craft Fair", "I've got a table at the craft fair and a car that's too small for it.", null),
            ("Moving Day", "the New Apartment", "Moving day! The van fell through, so it's just my car and your magic.", null),
            ("The Yard Sale", "the Yard Sale", "Yard sale on Sunday. Turns out I own a lot of yard.", null),
            ("The Road Trip", "Route 9", "Road trip to see my sister. She asked us to bring 'a few things'.", null),
            ("Visiting Grandma", "Grandma's", "Visiting my grandmother, who asked for everything in the attic.", null),
            ("The Science Fair", "the School Gym", "The science fair is tomorrow. Please don't ask about the volcano.", null),
            ("The Storage Unit", "the Storage Unit", "Clearing out the storage unit before they start charging me double.", null),
            ("The Garage Clear-Out", "the Charity Shop", "My garage is finally getting cleared out. All of it goes to the charity shop.", null),
            ("The Beach Trip", "Pebble Bay", "We're off to the beach and the car is already complaining.",
                new[] { "surfboard", "boogie_board", "umbrella", "shark", "kiddie_pool", "flamingo", "plant_stand", "tiki_torch", "cooler" }),
            ("Off to Camp", "Camp Starfish", "Dropping the kids at camp. They packed for a month. It's a week.",
                new[] { "tent", "sleeping_bag", "lantern", "firewood", "marshmallows", "camp_chair", "backpack", "thermos", "moose" }),
            ("Gone Fishing", "Lake Mirabel", "Fishing weekend at the lake. The fish have been warned.",
                new[] { "fishing_rod", "tackle_box", "kayak", "paddle", "cooler", "thermos" }),
            ("The Talent Show", "the Village Hall", "Props for the talent show. Don't ask what my act is.",
                new[] { "trombone", "tuba", "unicycle", "juggling_pins", "big_shoes", "rubber_chicken", "seltzer", "cream_pies", "costume_trunk", "disco_ball" }),
            ("The Big Picnic", "Willow Park", "The whole street's picnic is in my car. No pressure.",
                new[] { "picnic_basket", "lemonade", "watermelon", "baguette", "cake", "folding_chairs", "camp_chair", "grocery_bag" }),
            ("The Block Party", "the Cul-de-Sac", "Block party tonight, and I'm in charge of 'the fun stuff'.",
                new[] { "speaker", "glow_sticks", "disco_ball", "balloons_grad", "tiki_torch", "lava_lamp", "champagne" }),
            ("The Garden Show", "the Garden Show", "My plants are entered in the garden show. They're very nervous.",
                new[] { "ficus", "monstera", "flowers", "flower_arch", "plant_stand" }),
            ("The Ski Trip", "Mount Hollis", "Ski weekend! I've never skied. How hard can it be?",
                new[] { "skis", "snowman_kit", "thermos", "shovel" }),
            ("The Baby Shower", "My Sister's", "My sister's baby shower, and I'm bringing everything.",
                new[] { "stroller", "playpen", "baby_bath", "baby_mobile", "diapers", "car_seat", "white_noise", "crib_flatpack", "hospital_bag" }),
            ("Off to College", "the Dorms", "Taking my son to college. He says he packed light.",
                new[] { "mini_fridge", "futon", "beanbag", "desk_lamp", "hamper", "shower_caddy", "poster_tube", "comforter", "record_crate", "small_box" }),
            ("The Wedding", "the Old Barn", "My niece's wedding, and I'm on decorations.",
                new[] { "tiered_cake", "flower_arch", "champagne", "flowers", "folding_chairs", "camera_bag" }),
            ("The Food Drive", "the Food Bank", "The whole food drive is in my garage. It has to get to the food bank.",
                new[] { "grocery_bag", "eggs", "water", "toilet_paper", "cereal", "watermelon", "baguette", "marshmallows" }),
            ("The Birthday Party", "the Park", "The twins turn six on Saturday, and the party's at the park.",
                new[] { "toy_box", "toy_truck", "toy_blocks", "bike", "kiddie_pool", "cake", "balloons_grad", "lemonade" }),
            ("The Housewarming", "the New House", "My daughter bought a house. I'm bringing the furniture she doesn't know about yet.",
                new[] { "kitchen_table", "toaster", "lamp", "mirror", "kitchen_sink", "paint_cans", "moving_box", "cat_tree" }),
            ("League Night", "the Bowling Alley", "League final tonight. The trophy comes whether we win or not.",
                new[] { "bowling_ball", "trophy" }),
            ("The Duck Derby", "the Creek", "Duck Derby day! My duck has been training all year.",
                new[] { "duck", "trophy", "baby_bath", "comforter" }),
        };

        /// <summary>How many of an errand's things a pile needs for that errand (fewer, and nearly every pile suits one).</summary>
        const int SuitsAt = 3;

        static readonly string[] Thanks =
        {
            "Everyone says you can make anything fit. I'll owe you a pie.",
            "I hear you're the one who can make anything fit. Lemonade's on me.",
            "Your grandpa always said you had the knack. Help?",
            "I've tried three times. It keeps not closing.",
            "You're a lifesaver. I'll bring cookies.",
            "No rush! Well, a little rush.",
            "I'd ask my brother, but he packs like a raccoon.",
            "Everyone on the street says you're the one to ask.",
        };

        /// <summary>Made in this session, by number, so TRY AGAIN and a waiting trunk always find the same pile.</summary>
        static readonly Dictionary<int, LevelDef> made = new Dictionary<int, LevelDef>();

        /// <summary>
        /// Make favour <paramref name="number"/>: a car from <paramref name="cars"/> (not <paramref name="avoidCar"/> if
        /// there's a choice) and a pile from <paramref name="pool"/>, packed into it. <paramref name="variant"/> counts
        /// ASK SOMEONE ELSE: each one is another neighbour and pile for the same number. Deterministic for the same inputs.
        /// </summary>
        public static LevelDef Make(int number, IReadOnlyList<LevelDef> cars, IReadOnlyList<ItemDef> pool, string avoidCar = null, int variant = 0)
        {
            var rng = new System.Random(number * 7919 + 17 + variant * 104723);
            var choices = cars.Where(c => c.FreeCells >= MinFreeCells).ToList();
            if (choices.Count > 1 && avoidCar != null) choices.RemoveAll(c => c.Id == avoidCar);
            if (choices.Count == 0) return null;
            var car = choices[rng.Next(choices.Count)];
            // The neighbours' own things: never the family's (an heirloom, the cat, someone's named thing).
            var items = pool.Where(d => !d.Family).GroupBy(d => d.Id).Select(g => g.First()).OrderBy(d => d.Id, System.StringComparer.Ordinal).ToList();
            // Some piles are all sturdy, some have a few things that have to ride on top.
            int fragileCap = 1 + rng.Next(4);

            List<(ItemDef Def, Vector3Int Min, VoxelShape Shape)> best = null;
            float bestScore = float.MaxValue;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float target = Mathf.Lerp(0.84f, 0.94f, (float)rng.NextDouble());
                var packed = Pack(car, items, rng, target, fragileCap);
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
            level.FavourVariant = variant;
            level.Packing = best.Select(p => (p.Def.Id, p.Shape.Voxels.Select(v => v.Pos + p.Min).ToList())).ToList();
            for (int i = 0; i < order.Count; i++) (i < essentials ? level.Required : level.Bonus).Add(order[i].Def);
            Words(level);
            made[number] = level;
            return level;
        }

        /// <summary>The favour's car, before its pile and its words.</summary>
        static LevelDef Shell(int number, LevelDef car) => new LevelDef
        {
            Index = car.Index,
            Id = "favour-" + number.ToString(CultureInfo.InvariantCulture),
            Favour = number,
            ModelId = car.ModelId ?? car.Id,
            Vehicle = car.Vehicle,
            Style = car.Style,
            BodyColor = car.BodyColor,
            Size = car.Size,
            Blocked = new HashSet<Vector3Int>(car.Blocked),
            Music = car.Music,
            Medium = "phone",
        };

        /// <summary>
        /// Who's asking (the neighbours take turns; asking someone else moves seven along), what for (an errand that suits
        /// the pile, if one does) and the texts, one of which names something in the pile. Follows from the favour's number,
        /// variant and pile alone.
        /// </summary>
        static void Words(LevelDef level)
        {
            int number = level.Favour, variant = level.FavourVariant;
            var words = new System.Random(number * 104729 + 3 + variant * 7919);
            var who = Neighbours[Turns[(number - 1 + 7 * variant) % Turns.Length]];
            var pile = level.Required.Concat(level.Bonus).ToList();
            var errand = ErrandFor(pile, words);
            level.Title = errand.Title;
            level.Blurb = $"{who.Name} {who.Where} needs a hand.";
            level.Trip = $"{who.Where} · {errand.Place}";
            level.Sender = who.Name;
            level.PackFor = who.Name;
            level.Messages = new[] { $"Hi! {who.Name} here, {who.Where}.", errand.Ask, PileLine(pile, words), Thanks[words.Next(Thanks.Length)] };
            level.Epilogues = new[]
            {
                $"It closed! The rest went on the back seat, and {who.Name} didn't mind a bit.",
                $"Only a couple of things left on the driveway. {who.Name} waved all the way down the street.",
                $"Every last thing fit. {who.Name} is telling the whole street about you.",
            };
        }

        /// <summary>An errand with at least three of its things in the pile (the best few, by luck), or else one that fits anything.</summary>
        static (string Title, string Place, string Ask, string[] Things) ErrandFor(List<ItemDef> pile, System.Random words)
        {
            var ids = pile.Select(d => d.Id).ToList();
            var scored = Errands.Select(e => (e, Score: e.Things == null ? 0 : ids.Count(e.Things.Contains))).ToList();
            int best = scored.Max(x => x.Score);
            var pick = best >= SuitsAt ? scored.Where(x => x.Score >= Mathf.Max(SuitsAt, best - 1)).Select(x => x.e).ToList()
                                 : Errands.Where(e => e.Things == null).ToList();
            return pick[words.Next(pick.Count)];
        }

        /// <summary>A line about something in the pile: a fragile thing (and the rule), the biggest thing, or an odd one.</summary>
        static string PileLine(List<ItemDef> pile, System.Random words)
        {
            var fragile = pile.Where(d => d.Fragile).ToList();
            var biggest = pile.OrderByDescending(d => d.Volume).ThenBy(d => d.Id, System.StringComparer.Ordinal).First();
            var others = pile.Where(d => !d.Fragile && d != biggest).ToList();
            int kind = words.Next(4);
            if (fragile.Count > 0 && kind < 2)
                return $"Careful with {fragile[words.Next(fragile.Count)].WithThe}. It's fragile, so nothing goes on top of it.";
            if (others.Count > 0 && kind == 3)
                return words.Next(2) == 0 ? $"And yes, {others[words.Next(others.Count)].WithThe} is coming too." : $"Don't ask about {others[words.Next(others.Count)].WithThe}.";
            return words.Next(2) == 0 ? $"Big things first, I know. So {biggest.WithThe} goes in first." : $"{Capital(biggest.WithThe)} has to come. It always has to come.";
        }

        /// <summary>The things an errand suits (null for one that fits any pile, or an unknown title), for the self-test.</summary>
        public static string[] ErrandThings(string title) => Errands.FirstOrDefault(e => e.Title == title).Things;

        /// <summary>The errands that suit a pile (at least three of their things in it), for the self-test.</summary>
        public static IEnumerable<string> ErrandsSuiting(IEnumerable<string> pile)
        {
            var ids = pile.ToList();
            return Errands.Where(e => e.Things != null && ids.Count(e.Things.Contains) >= SuitsAt).Select(e => e.Title);
        }

        static string Capital(string s) => s.Length > 0 ? char.ToUpperInvariant(s[0]) + s.Substring(1) : s;

        /// <summary>
        /// Pack a pile into the trunk, bottom first: at each empty cell in the solver's scan order (up, then back, then
        /// across), try things biggest-first with some luck, each in every turn, anchored on that cell; the first that the
        /// rules allow goes in. A cell nothing fits stays empty. Stops at the target fill or the item limit.
        /// </summary>
        static List<(ItemDef Def, Vector3Int Min, VoxelShape Shape)> Pack(LevelDef car, List<ItemDef> items, System.Random rng, float target, int fragileCap)
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
                // Weighted shuffle (bigger things come first more often), at most two of anything and this favour's share of fragile things.
                var candidates = items
                    .Where(d => d.Volume <= room && (!counts.TryGetValue(d.Id, out var n) || n < 2) && (!d.Fragile || fragiles < fragileCap))
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

        /// <summary>
        /// "2|number|variant|car|req ids|bonus ids|packing" with packing "id:x,y,z;x,y,z/..." (what the favour is). Round 10
        /// saved "1|number|car|..." (no variant), which still reads.
        /// </summary>
        public static string Serialize(LevelDef favour)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("2|").Append(favour.Favour.ToString(inv)).Append('|').Append(favour.FavourVariant.ToString(inv)).Append('|').Append(favour.ModelId).Append('|');
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
                var parts = data.Split('|').ToList();
                int variant = 0;
                if (parts.Count == 7 && parts[0] == "2")
                {
                    variant = int.Parse(parts[2], CultureInfo.InvariantCulture);
                    parts.RemoveAt(2);
                }
                else if (parts.Count != 6 || parts[0] != "1") return null;
                int number = int.Parse(parts[1], CultureInfo.InvariantCulture);
                if (made.TryGetValue(number, out var known) && Serialize(known) == data) return known;
                var car = levels.FirstOrDefault(l => l.Id == parts[2]);
                if (car == null) return null;
                var shell = Shell(number, car);
                shell.FavourVariant = variant;
                shell.Required = parts[3].Split(',').Where(s => s.Length > 0).Select(item).ToList();
                shell.Bonus = parts[4].Split(',').Where(s => s.Length > 0).Select(item).ToList();
                if (shell.Required.Any(d => d == null) || shell.Bonus.Any(d => d == null)) return null;
                Words(shell);
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
