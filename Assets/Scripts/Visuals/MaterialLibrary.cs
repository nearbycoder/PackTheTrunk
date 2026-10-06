using System.Collections.Generic;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// Hands out shared materials, one per colour, cloned from the stylized PTT/Toon template in
    /// Resources (falling back to URP Lit if the setup step hasn't been run).
    /// </summary>
    public static class MaterialLibrary
    {
        static Material litTemplate;
        static Material ghostTemplate;
        static readonly Dictionary<(Color32, float), Material> lit = new Dictionary<(Color32, float), Material>();
        static readonly Dictionary<(Color32, bool), Material> ghosts = new Dictionary<(Color32, bool), Material>();
        static readonly Dictionary<string, Material> special = new Dictionary<string, Material>();

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int Metallic = Shader.PropertyToID("_Metallic");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int Grain = Shader.PropertyToID("_Grain");
        static readonly int RimStrength = Shader.PropertyToID("_RimStrength");

        public static Material Lit(Color color, float smoothness = 0.3f)
        {
            var key = ((Color32)color, smoothness);
            if (lit.TryGetValue(key, out var mat)) return mat;

            if (litTemplate == null)
            {
                litTemplate = Resources.Load<Material>("Materials/PTT_Toon");
                if (litTemplate == null) litTemplate = Resources.Load<Material>("Materials/PTT_Lit");
                if (litTemplate == null) litTemplate = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }
            mat = new Material(litTemplate) { name = "Lit " + ColorUtility.ToHtmlStringRGB(color) };
            mat.SetColor(BaseColor, color);
            mat.SetFloat(Smoothness, smoothness);
            lit[key] = mat;
            return mat;
        }

        /// <summary>Matte surface with world-space grain (asphalt, grass, carpet, cardboard).</summary>
        public static Material Textured(Color color, float grain, float smoothness = 0.08f)
        {
            var key = $"grain{ColorUtility.ToHtmlStringRGB(color)}{grain}{smoothness}";
            if (special.TryGetValue(key, out var mat)) return mat;
            mat = new Material(Lit(color, smoothness)) { name = "Grain" };
            mat.SetFloat(Grain, grain);
            mat.SetFloat(RimStrength, 0.12f);
            return special[key] = mat;
        }

        public static Material Metal(Color color)
        {
            var key = "metal" + ColorUtility.ToHtmlStringRGB(color);
            if (special.TryGetValue(key, out var mat)) return mat;
            mat = new Material(Lit(color, 0.75f)) { name = "Metal" };
            mat.SetFloat(Metallic, 0.85f);
            return special[key] = mat;
        }

        public static Material Glowing(Color color, float intensity)
        {
            var key = "glow" + ColorUtility.ToHtmlStringRGB(color) + intensity;
            if (special.TryGetValue(key, out var mat)) return mat;
            mat = new Material(Lit(color)) { name = "Glow" };
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor(EmissionColor, color * intensity);
            return special[key] = mat;
        }

        public static Material Ghost(Color color, bool hatched = false)
        {
            var key = ((Color32)color, hatched);
            if (ghosts.TryGetValue(key, out var mat)) return mat;

            if (ghostTemplate == null)
            {
                ghostTemplate = Resources.Load<Material>("Materials/PTT_GhostFX");
                if (ghostTemplate == null) ghostTemplate = Resources.Load<Material>("Materials/PTT_Ghost");
                if (ghostTemplate == null)
                {
                    ghostTemplate = new Material(Lit(Color.white));
                    ghostTemplate.SetFloat("_Surface", 1f);
                    ghostTemplate.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    ghostTemplate.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    ghostTemplate.SetFloat("_ZWrite", 0f);
                    ghostTemplate.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    ghostTemplate.SetOverrideTag("RenderType", "Transparent");
                    ghostTemplate.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
            }
            mat = new Material(ghostTemplate) { name = "Ghost" };
            mat.SetColor(BaseColor, color);
            mat.SetFloat("_Hatch", hatched ? 1f : 0f);
            ghosts[key] = mat;
            return mat;
        }

        public static Material[] ForItem(ItemDef def)
        {
            var mats = new Material[def.Colors.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = Lit(def.Colors[i], 0.35f);
            return mats;
        }
    }
}
