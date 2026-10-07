using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// The save: progress, settings and seen tips in PlayerPrefs, album photos on disk. Self-tests,
    /// benchmarks and recordings get a sandbox instead (prefs in memory, photos in their own
    /// folder), so they always start from a fresh save and never touch the player's. With
    /// <c>-pttPrefsFile &lt;path&gt;</c> the sandbox is kept in that file instead of memory, so a test
    /// can quit (or kill) the player and start it again on the same throwaway save.
    /// </summary>
    public static class Prefs
    {
        static readonly Dictionary<string, object> sandbox = LoadSandbox();

        static bool Sandboxed => GameController.Automated;

        static string SandboxFile
        {
            get
            {
                var args = System.Environment.GetCommandLineArgs();
                int i = System.Array.IndexOf(args, "-pttPrefsFile");
                return Sandboxed && i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            }
        }

        // One "type<TAB>key<TAB>value" line per entry.
        static Dictionary<string, object> LoadSandbox()
        {
            var d = new Dictionary<string, object>();
            var file = SandboxFile;
            if (file == null || !File.Exists(file)) return d;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var line in File.ReadAllLines(file))
            {
                var f = line.Split('\t');
                if (f.Length != 3) continue;
                if (f[0] == "i" && int.TryParse(f[2], System.Globalization.NumberStyles.Integer, inv, out var i)) d[f[1]] = i;
                else if (f[0] == "f" && float.TryParse(f[2], System.Globalization.NumberStyles.Float, inv, out var x)) d[f[1]] = x;
                else if (f[0] == "s") d[f[1]] = f[2];
            }
            return d;
        }

        /// <summary>Where trunk photos are saved (a throwaway cache folder for sandboxed runs).</summary>
        public static string AlbumDir => Sandboxed
            ? Path.Combine(Application.temporaryCachePath, "album-capture")
            : Path.Combine(Application.persistentDataPath, "album");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ClearSandboxAlbum()
        {
            if (!Sandboxed) return;
            try
            {
                if (Directory.Exists(AlbumDir)) Directory.Delete(AlbumDir, true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Prefs] could not clear the capture album: " + e.Message);
            }
        }

        public static int GetInt(string key, int fallback = 0) =>
            Sandboxed ? (sandbox.TryGetValue(key, out var v) && v is int i ? i : fallback) : PlayerPrefs.GetInt(key, fallback);

        public static float GetFloat(string key, float fallback = 0f) =>
            Sandboxed ? (sandbox.TryGetValue(key, out var v) && v is float f ? f : fallback) : PlayerPrefs.GetFloat(key, fallback);

        public static string GetString(string key, string fallback = "") =>
            Sandboxed ? (sandbox.TryGetValue(key, out var v) && v is string s ? s : fallback) : PlayerPrefs.GetString(key, fallback);

        public static bool HasKey(string key) => Sandboxed ? sandbox.ContainsKey(key) : PlayerPrefs.HasKey(key);

        public static void SetString(string key, string value)
        {
            if (Sandboxed) sandbox[key] = value;
            else PlayerPrefs.SetString(key, value);
        }

        public static void SetInt(string key, int value)
        {
            if (Sandboxed) sandbox[key] = value;
            else PlayerPrefs.SetInt(key, value);
        }

        public static void SetFloat(string key, float value)
        {
            if (Sandboxed) sandbox[key] = value;
            else PlayerPrefs.SetFloat(key, value);
        }

        public static void DeleteKey(string key)
        {
            if (Sandboxed) sandbox.Remove(key);
            else PlayerPrefs.DeleteKey(key);
        }

        /// <summary>Write pending changes to disk.</summary>
        public static void Save()
        {
            if (!Sandboxed)
            {
                PlayerPrefs.Save();
                return;
            }
            var file = SandboxFile;
            if (file == null) return;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var lines = new List<string>();
            foreach (var kv in sandbox)
                lines.Add(kv.Value switch
                {
                    int i => "i\t" + kv.Key + "\t" + i.ToString(inv),
                    float x => "f\t" + kv.Key + "\t" + x.ToString("R", inv),
                    _ => "s\t" + kv.Key + "\t" + kv.Value,
                });
            // Write then rename, so a kill mid-write never leaves half a file.
            File.WriteAllLines(file + ".tmp", lines);
            if (File.Exists(file)) File.Replace(file + ".tmp", file, null);
            else File.Move(file + ".tmp", file);
        }
    }
}
