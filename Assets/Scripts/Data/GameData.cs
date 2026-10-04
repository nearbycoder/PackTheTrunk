using System;
using System.Collections.Generic;
using UnityEngine;

namespace PackTheTrunk
{
    [Serializable]
    public class ItemJson
    {
        public string id;
        public string name;
        public string desc;
        public bool fragile;
        public string[] palette;
        public string[] layers;
    }

    [Serializable]
    public class LevelJson
    {
        public string id;
        public string title;
        public string vehicle;
        public string blurb;
        public string style;
        public string bodyColor;
        public int w;
        public int h;
        public int d;
        public string[] blocked;
        public string[] required;
        public string[] bonus;
        public string trip;
        public string sender;
        public string packFor;
        public string music;
        public string[] messages;
        public string[] epilogues;
        public string chapter;
        public int year;
        public string medium;
    }

    [Serializable]
    public class ChapterJson
    {
        public string id;
        public string title;
        public string years;
        public string intro;
    }

    [Serializable]
    public class GameDataJson
    {
        public ChapterJson[] chapters;
        public ItemJson[] items;
        public LevelJson[] levels;
    }

    /// <summary>A stretch of the family's story: several trips across a few years.</summary>
    public class ChapterDef
    {
        public int Index;
        public string Id;
        public string Title;
        public string Years;
        public string Intro;
        public List<LevelDef> Levels = new List<LevelDef>();

        public string Numeral => Roman(Index + 1);

        static string Roman(int n)
        {
            string[] r = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            return n < r.Length ? r[n] : n.ToString();
        }
    }

    public class ItemDef
    {
        public string Id;
        public string Name;
        public string Description;
        public bool Fragile;
        public Color[] Colors;
        public VoxelShape Shape;
        public int Volume => Shape.Voxels.Length;
    }

    public class LevelDef
    {
        public int Index;
        public string Id;
        public string Title;
        public string Vehicle;
        public string Blurb;
        public string Style;
        public Color BodyColor;
        public Vector3Int Size;
        public HashSet<Vector3Int> Blocked = new HashSet<Vector3Int>();
        public List<ItemDef> Required = new List<ItemDef>();
        public List<ItemDef> Bonus = new List<ItemDef>();
        public string Trip;
        public string Sender;
        /// <summary>Who the packing list is for (defaults to whoever asked).</summary>
        public string PackFor;
        public string Music;
        public string[] Messages = new string[0];
        /// <summary>One line per star rating (1, 2, 3 stars).</summary>
        public string[] Epilogues = new string[0];
        public ChapterDef Chapter;
        public int Year;
        /// <summary>"phone" for text messages, "note" for a handwritten note.</summary>
        public string Medium = "phone";

        public bool IsNote => Medium == "note";
        public bool IsFirstInChapter => Chapter != null && Chapter.Levels.Count > 0 && Chapter.Levels[0] == this;

        public int FreeCells => Size.x * Size.y * Size.z - Blocked.Count;
    }

    /// <summary>Loads items and levels from Resources/PackTheTrunkData.json.</summary>
    public static class GameDatabase
    {
        static List<LevelDef> levels;
        static List<ChapterDef> chapters;
        static Dictionary<string, ItemDef> items;

        public static IReadOnlyList<LevelDef> Levels
        {
            get { EnsureLoaded(); return levels; }
        }

        public static IReadOnlyList<ChapterDef> Chapters
        {
            get { EnsureLoaded(); return chapters; }
        }

        static void EnsureLoaded()
        {
            if (levels != null) return;

            var asset = Resources.Load<TextAsset>("PackTheTrunkData");
            if (asset == null) throw new InvalidOperationException("Missing Resources/PackTheTrunkData.json");
            var data = JsonUtility.FromJson<GameDataJson>(asset.text);

            items = new Dictionary<string, ItemDef>();
            foreach (var j in data.items)
                items[j.id] = ParseItem(j);

            chapters = new List<ChapterDef>();
            var byId = new Dictionary<string, ChapterDef>();
            if (data.chapters != null)
            {
                foreach (var c in data.chapters)
                {
                    var chapter = new ChapterDef { Index = chapters.Count, Id = c.id, Title = c.title, Years = c.years, Intro = c.intro };
                    chapters.Add(chapter);
                    byId[c.id] = chapter;
                }
            }

            levels = new List<LevelDef>();
            for (int i = 0; i < data.levels.Length; i++)
            {
                var level = ParseLevel(data.levels[i], i);
                if (string.IsNullOrEmpty(data.levels[i].chapter) || !byId.TryGetValue(data.levels[i].chapter, out var chapter))
                {
                    if (chapters.Count == 0)
                    {
                        chapters.Add(new ChapterDef { Index = 0, Id = "all", Title = "Road Trips", Years = "", Intro = "" });
                        byId["all"] = chapters[0];
                    }
                    chapter = chapters[chapters.Count - 1];
                }
                level.Chapter = chapter;
                chapter.Levels.Add(level);
                levels.Add(level);
            }
        }

        static ItemDef ParseItem(ItemJson j)
        {
            var keys = new List<char>();
            var colors = new List<Color>();
            foreach (var entry in j.palette)
            {
                keys.Add(entry[0]);
                colors.Add(ParseColor(entry.Substring(2)));
            }

            var voxels = new List<Voxel>();
            ForEachCell(j.layers, (pos, ch) =>
            {
                int index = keys.IndexOf(ch);
                voxels.Add(new Voxel(pos, (byte)Mathf.Max(0, index)));
            });

            return new ItemDef
            {
                Id = j.id,
                Name = j.name,
                Description = j.desc,
                Fragile = j.fragile,
                Colors = colors.ToArray(),
                Shape = new VoxelShape(voxels),
            };
        }

        static LevelDef ParseLevel(LevelJson j, int index)
        {
            var level = new LevelDef
            {
                Index = index,
                Id = j.id,
                Title = j.title,
                Vehicle = j.vehicle,
                Blurb = j.blurb,
                Style = j.style,
                BodyColor = ParseColor(j.bodyColor),
                Size = new Vector3Int(j.w, j.h, j.d),
                Trip = j.trip ?? "",
                Music = j.music ?? "",
                Sender = j.sender ?? "",
                PackFor = string.IsNullOrEmpty(j.packFor) ? j.sender ?? "" : j.packFor,
                Messages = j.messages ?? new string[0],
                Epilogues = j.epilogues ?? new string[0],
                Year = j.year,
                Medium = string.IsNullOrEmpty(j.medium) ? "phone" : j.medium,
            };
            if (j.blocked != null)
                ForEachCell(j.blocked, (pos, _) => level.Blocked.Add(pos));
            foreach (var id in j.required) level.Required.Add(Item(id));
            foreach (var id in j.bonus) level.Bonus.Add(Item(id));
            return level;
        }

        static ItemDef Item(string id)
        {
            if (!items.TryGetValue(id, out var def))
                throw new InvalidOperationException($"Unknown item id '{id}'");
            return def;
        }

        /// <summary>
        /// Layers run bottom to top. Inside a layer, rows are separated by '|' and listed
        /// far (high z) to near (z = 0) so the text reads like a top-down view from the bumper.
        /// </summary>
        static void ForEachCell(string[] layers, Action<Vector3Int, char> visit)
        {
            for (int y = 0; y < layers.Length; y++)
            {
                var rows = layers[y].Split('|');
                for (int r = 0; r < rows.Length; r++)
                {
                    int z = rows.Length - 1 - r;
                    for (int x = 0; x < rows[r].Length; x++)
                    {
                        char ch = rows[r][x];
                        if (ch == '.' || ch == ' ') continue;
                        visit(new Vector3Int(x, y, z), ch);
                    }
                }
            }
        }

        public static Color ParseColor(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }
    }
}
