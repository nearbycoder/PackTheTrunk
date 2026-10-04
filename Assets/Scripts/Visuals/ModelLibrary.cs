using System.Collections.Generic;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// Loads the Blender-built FBX models from Resources/Models and swaps their imported materials
    /// for shared URP materials. Material names encode the look: col_/metal_/glass_/glow_ + RRGGBB.
    /// </summary>
    public static class ModelLibrary
    {
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Material> resolved = new Dictionary<string, Material>();

        public static GameObject Load(string path)
        {
            if (!prefabs.TryGetValue(path, out var prefab))
            {
                prefab = Resources.Load<GameObject>("Models/" + path);
                prefabs[path] = prefab;
            }
            return prefab;
        }

        public static GameObject Instantiate(string path, Transform parent)
        {
            var prefab = Load(path);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null) mats[i] = Resolve(mats[i]);
                renderer.sharedMaterials = mats;
            }
            return go;
        }

        public static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }

        static Material Resolve(Material imported)
        {
            var name = imported.name.Replace(" (Instance)", "");
            if (resolved.TryGetValue(name, out var mat)) return mat;

            mat = imported;
            int split = name.IndexOf('_');
            if (split > 0 && name.Length >= split + 7 &&
                ColorUtility.TryParseHtmlString("#" + name.Substring(split + 1, 6), out var color))
            {
                switch (name.Substring(0, split))
                {
                    case "metal": mat = MaterialLibrary.Metal(color); break;
                    case "glass": mat = MaterialLibrary.Lit(color, 0.92f); break;
                    case "glow": mat = MaterialLibrary.Glowing(color, 1.6f); break;
                    default: mat = MaterialLibrary.Lit(color, 0.3f); break;
                }
            }
            resolved[name] = mat;
            return mat;
        }
    }
}
