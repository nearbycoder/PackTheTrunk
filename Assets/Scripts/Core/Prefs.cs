using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// The save: progress, settings and seen tips in PlayerPrefs, album photos on disk. Self-tests,
    /// benchmarks and recordings get a sandbox instead (prefs in memory, photos in their own
    /// folder), so they always start from a fresh save and never touch the player's.
    /// </summary>
    public static class Prefs
    {
        static readonly Dictionary<string, object> sandbox = new Dictionary<string, object>();

        static bool Sandboxed => GameController.Automated;

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
            if (!Sandboxed) PlayerPrefs.Save();
        }
    }
}
