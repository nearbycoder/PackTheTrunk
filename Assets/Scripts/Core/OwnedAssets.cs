using System.Collections.Generic;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// Frees meshes, textures and materials made at runtime when their object is destroyed
    /// (Unity keeps them alive otherwise, so every trip would leak its blanket, colliders etc.).
    /// </summary>
    public class OwnedAssets : MonoBehaviour
    {
        readonly List<Object> assets = new List<Object>();

        public static T Track<T>(Component owner, T asset) where T : Object
        {
            if (asset == null) return asset;
            var owned = owner.GetComponent<OwnedAssets>();
            if (owned == null) owned = owner.gameObject.AddComponent<OwnedAssets>();
            owned.assets.Add(asset);
            return asset;
        }

        /// <summary>Destroy an asset that is being replaced and stop tracking it.</summary>
        public static void Release(Component owner, Object asset)
        {
            if (asset == null) return;
            var owned = owner.GetComponent<OwnedAssets>();
            if (owned != null) owned.assets.Remove(asset);
            Destroy(asset);
        }

        void OnDestroy()
        {
            foreach (var a in assets)
                if (a != null) Destroy(a);
            assets.Clear();
        }
    }
}

namespace PackTheTrunk
{
    /// <summary>Runs background coroutines that must survive their owner's StopAllCoroutines.</summary>
    public class CoroutineHost : UnityEngine.MonoBehaviour { }
}
