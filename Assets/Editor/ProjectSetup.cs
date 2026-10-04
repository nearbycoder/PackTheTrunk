using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PackTheTrunk.EditorTools
{
    /// <summary>
    /// One-time project wiring for the stylized look: material assets for the custom shaders
    /// (kept in Resources so they ship in builds), the ink-outline full-screen pass and SSAO tuning.
    /// Run from the menu or with -executeMethod PackTheTrunk.EditorTools.ProjectSetup.Apply.
    /// </summary>
    public static class ProjectSetup
    {
        const string MaterialDir = "Assets/Resources/Materials";
        const string OutlineName = "PTT Ink Outline";

        [MenuItem("Pack The Trunk/Apply Rendering Setup")]
        public static void Apply()
        {
            var toon = EnsureMaterial("PTT_Toon", "PTT/Toon");
            EnsureMaterial("PTT_GhostFX", "PTT/Ghost");
            var sky = EnsureMaterial("PTT_Sky", "PTT/Sky");
            EnsureMaterial("PTT_Fx", "PTT/FxSprite").enableInstancing = true;
            var outline = EnsureMaterial("PTT_Outline", "PTT/Outline");
            if (outline != null)
            {
                // The play camera sits ~25-35 units away, so the fade has to start well beyond that.
                outline.SetFloat("_Thickness", 1.7f);
                outline.SetFloat("_DepthSensitivity", 18f);
                outline.SetFloat("_NormalSensitivity", 3.2f);
                outline.SetFloat("_Strength", 0.92f);
                outline.SetFloat("_FadeStart", 70f);
                outline.SetFloat("_FadeEnd", 160f);
            }
            if (toon == null || sky == null || outline == null)
            {
                Debug.LogError("[ProjectSetup] shaders failed to load");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            foreach (var path in new[] { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" })
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null) continue;
                ConfigureRenderer(data, outline);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] rendering setup applied");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static Material EnsureMaterial(string name, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[ProjectSetup] missing shader {shaderName}");
                return null;
            }
            var path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void ConfigureRenderer(UniversalRendererData data, Material outline)
        {
            var feature = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f.name == OutlineName);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = OutlineName;
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                var so = new SerializedObject(data);
                var features = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedProperties();
            }
            feature.passMaterial = outline;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            feature.fetchColorBuffer = true;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);

            foreach (var ssao in data.rendererFeatures.Where(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion"))
            {
                var so = new SerializedObject(ssao);
                void Set(string prop, float value)
                {
                    var p = so.FindProperty("m_Settings." + prop);
                    if (p == null) return;
                    if (p.propertyType == SerializedPropertyType.Float) p.floatValue = value;
                    else if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value > 0;
                    else p.intValue = (int)value;
                }
                Set("Intensity", 1.1f);
                Set("Radius", 0.45f);
                Set("DirectLightingStrength", 0.35f);
                Set("Source", 1);
                Set("Falloff", 60f);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(ssao);
            }
            EditorUtility.SetDirty(data);
        }
    }
}
