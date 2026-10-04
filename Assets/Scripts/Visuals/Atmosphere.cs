using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PackTheTrunk
{
    /// <summary>
    /// Late-summer-afternoon mood: warm low sun, gradient sky, soft fog, and a post stack
    /// (neutral tonemapping, gentle grading, bloom, vignette, background depth of field).
    /// </summary>
    public class Atmosphere : MonoBehaviour
    {
        public static readonly Color Horizon = new Color(0.98f, 0.86f, 0.72f);
        static readonly Color FallbackSky = new Color(0.58f, 0.79f, 0.95f);

        CameraRig rig;
        DepthOfField dof;
        Bloom bloom;
        bool dofEnabled = true;
        float blur, blurTarget;

        public static Atmosphere Apply(Camera cam, CameraRig rig)
        {
            var sun = FindAnyObjectByType<Light>();
            if (sun == null || sun.type != LightType.Directional)
                sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40f, -42f, 0f);
            sun.color = new Color(1f, 0.9f, 0.76f);
            sun.intensity = 1.45f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            RenderSettings.sun = sun;

            var sky = Resources.Load<Material>("Materials/PTT_Sky");
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = FallbackSky;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.7f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.86f, 0.78f, 0.7f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.4f, 0.36f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Color.Lerp(Horizon, new Color(0.7f, 0.78f, 0.9f), 0.35f);
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 170f;

            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;

            foreach (var existing in FindObjectsByType<Volume>(FindObjectsInactive.Exclude))
                existing.enabled = false;

            var go = new GameObject("Post FX");
            var atmosphere = go.AddComponent<Atmosphere>();
            atmosphere.rig = rig;
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = profile;

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(0.18f);
            grade.contrast.Override(10f);
            grade.saturation.Override(14f);

            var balance = profile.Add<WhiteBalance>(true);
            balance.temperature.Override(9f);
            balance.tint.Override(3f);

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.92f, 0.95f, 1.08f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1.0f, 0.95f, 0f));

            var bloom = atmosphere.bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(1f, 0.92f, 0.82f));

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.26f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.12f, 0.1f, 0.2f));

            atmosphere.dof = profile.Add<DepthOfField>(true);
            atmosphere.dof.mode.Override(DepthOfFieldMode.Gaussian);
            atmosphere.dof.gaussianMaxRadius.Override(1.1f);
            atmosphere.dof.highQualitySampling.Override(true);
            atmosphere.UpdateFocus();
            return atmosphere;
        }

        void LateUpdate()
        {
            blur = Mathf.MoveTowards(blur, blurTarget, UiTime.Delta * 2.5f);
            UpdateFocus();
        }

        public void ApplySettings(bool bloomOn, bool dofOn)
        {
            if (bloom != null) bloom.active = bloomOn;
            dofEnabled = dofOn;
            UpdateFocus();
        }

        /// <summary>Soft-focus the whole scene behind menus (0 = sharp, 1 = fully blurred).</summary>
        public void SetBlur(float amount) => blurTarget = Mathf.Clamp01(amount);

        public float BlurTarget => blurTarget;

        /// <summary>Keep the play area sharp and soften only what is well behind it.</summary>
        void UpdateFocus()
        {
            if (dof == null || rig == null) return;
            float d = rig.Distance;
            float k = blur * blur * (3f - 2f * blur);
            dof.active = dofEnabled || k > 0.001f;
            dof.gaussianStart.Override(Mathf.Lerp(d + 6f, 0f, k));
            dof.gaussianEnd.Override(Mathf.Lerp(d + 55f, d * 0.5f, k));
            dof.gaussianMaxRadius.Override(Mathf.Lerp(1.1f, 1.5f, k));
        }
    }
}
