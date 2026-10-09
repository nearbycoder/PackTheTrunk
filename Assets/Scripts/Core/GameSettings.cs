using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PackTheTrunk
{
    /// <summary>
    /// Every player-facing option, saved in PlayerPrefs and applied live: audio mix, display,
    /// graphics quality (URP asset, renderer features and post effects) and gameplay comfort.
    /// </summary>
    public static class GameSettings
    {
        public static event Action Changed;

        // ------------------------------------------------------------------ audio
        public static float Master { get => F("master", 0.9f); set => SetAudio("master", value); }
        public static float Music { get => F("music", 0.75f); set => SetAudio("music", value); }
        public static float Effects { get => F("effects", 0.85f); set => SetAudio("effects", value); }
        public static float Ambience { get => F("ambience", 0.6f); set => SetAudio("ambience", value); }
        public static bool MuteInBackground { get => B("bgmute", true); set { Prefs.SetInt(Prefix + "bgmute", value ? 1 : 0); CommitAudio(); } }

        // ------------------------------------------------------------------ display
        public static readonly string[] WindowModes = { "Borderless", "Fullscreen", "Windowed" };
        public static int WindowMode { get => I("window", 0); set => SetDisplay("window", value); }
        public static int ResolutionIndex { get => I("resolution", -1); set => SetDisplay("resolution", value); }
        public static bool VSync { get => B("vsync", true); set => Set("vsync", value); }
        public static readonly int[] FrameCaps = { 30, 60, 120, 144, 240, -1 };
        public static int FrameCap { get => I("framecap", 1); set => Set("framecap", value); }
        public static float FieldOfView { get => F("fov", 40f); set => Set("fov", value); }
        public static float UiScale { get => Prefs.HasKey(Prefix + "uiscale") ? F("uiscale", 1f) : AutoUiScale; set => Set("uiscale", value); }

        /// <summary>
        /// The interface size until the player picks one: 100%, or bigger on screens too small for the
        /// 1920x1080 design to stay readable (aiming for 0.8 screen pixels per design unit), but only as
        /// far as the menus keep their full layout (a canvas at least 1080 units tall and 1745 wide). A
        /// Steam Deck's 1280x800 gets 110%; 16:9 screens stay at 100%.
        /// </summary>
        public static float AutoUiScale
        {
            get
            {
                float fit = Mathf.Max(0.01f, Mathf.Min(Screen.width / 1920f, Screen.height / 1080f));
                float roomy = Mathf.Min(Screen.height / fit / 1080f, Screen.width / fit / 1745f);
                float want = Mathf.Ceil(0.8f / fit * 20f - 0.001f) / 20f;
                return Mathf.Clamp(want, 1f, Mathf.Min(1.2f, Mathf.Floor(roomy * 20f + 0.001f) / 20f));
            }
        }

        /// <summary>The player hasn't picked an interface size, so it follows the screen.</summary>
        public static bool UiScaleIsAuto => !Prefs.HasKey(Prefix + "uiscale");

        /// <summary>Forget the chosen interface size (back to the default).</summary>
        public static void ResetUiScale()
        {
            Prefs.DeleteKey(Prefix + "uiscale");
            Commit();
        }

        // ------------------------------------------------------------------ graphics
        public static readonly string[] Presets = { "Low", "Medium", "High", "Ultra", "Custom" };

        /// <summary>
        /// The fidelity step a new save starts on: High, or Medium in the browser (WebGL draws the same frame slower), or Low
        /// in a phone's or tablet's browser (fewer and smaller full-screen buffers, for a tab's tight memory limit).
        /// </summary>
        public static readonly int DefaultFidelity = Web.IsTouchFirst ? 0 : Web.IsBrowser ? 1 : 2;

        /// <summary>The fine-tune rows each fidelity step sets (a new save's rows are its default step's).</summary>
        static readonly (float scale, int aa, int shadows, bool ssao, bool outlines, bool dof, bool bloom)[] StepRows =
        {
            (0.75f, 1, 1, false, true, false, false),   // Low
            (1f, 2, 2, false, true, true, true),        // Medium
            (1f, 3, 3, true, true, true, true),         // High
            (1.5f, 3, 4, true, true, true, true),       // Ultra
        };

        static (float scale, int aa, int shadows, bool ssao, bool outlines, bool dof, bool bloom) DefaultRows => StepRows[DefaultFidelity];

        /// <summary>A fidelity step (0-3), or 4 once a fine-tune row has been changed.</summary>
        public static int Preset { get => I("preset", DefaultFidelity); set => ApplyPreset(value); }

        /// <summary>
        /// The Graphics fidelity step: 0 Low (the default on phones and tablets), 1 Medium (the browser's default), 2 High (the default), 3 Ultra. It sets the fine-tune
        /// rows below it, and the detail that has no row of its own (ambient occlusion quality, bloom
        /// filtering, blanket texture density, particles, surface detail) follows it even once a row has been
        /// changed (cached: particles read it).
        /// </summary>
        public static int Fidelity { get { Cache(); return fidelity; } set => ApplyPreset(Mathf.Clamp(value, 0, 3)); }

        public static readonly string[] FidelitySteps = { "Low", "Medium", "High", "Ultra" };

        /// <summary>A fine-tune row differs from the fidelity step.</summary>
        public static bool FidelityCustom => Preset == 4;

        /// <summary>How many particles an effect spawns, relative to High.</summary>
        public static float ParticleDensity { get { Cache(); return ParticleDensities[fidelity]; } }
        static readonly float[] ParticleDensities = { 0.5f, 0.75f, 1f, 1.6f };

        /// <summary>Picnic blanket texels per unit (Ultra doubles them).</summary>
        public static float BlanketTexelScale => Fidelity == 3 ? 2f : 1f;
        public static float RenderScale { get => F("renderscale", DefaultRows.scale); set => SetCustom("renderscale", value); }
        public static readonly string[] AntiAliasingModes = { "Off", "FXAA", "SMAA", "MSAA 4x + SMAA" };
        public static int AntiAliasing { get => I("aa", DefaultRows.aa); set => SetCustom("aa", value); }
        public static readonly string[] ShadowModes = { "Off", "Low", "Medium", "High", "Ultra" };
        public static int Shadows { get => I("shadows", DefaultRows.shadows); set => SetCustom("shadows", value); }
        public static bool AmbientOcclusion { get => B("ssao", DefaultRows.ssao); set => SetCustom("ssao", value); }
        public static bool Outlines { get => B("outlines", DefaultRows.outlines); set => SetCustom("outlines", value); }
        public static bool DepthOfField { get => B("dof", DefaultRows.dof); set => SetCustom("dof", value); }
        public static bool Bloom { get => B("bloom", DefaultRows.bloom); set => SetCustom("bloom", value); }

        // ------------------------------------------------------------------ gameplay
        public static float OrbitSpeed { get => F("orbit", 1f); set => Set("orbit", value); }
        public static bool InvertOrbit { get => B("invert", false); set => Set("invert", value); }
        public static bool ScreenShake { get => B("shake", true); set => Set("shake", value); }
        public static bool KeyHints { get => B("hints", true); set => Set("hints", value); }
        public static bool Tips { get => B("tips", true); set => Set("tips", value); }
        public static readonly string[] PlacementPalettes = { "Green / red", "Blue / orange" };
        public static int PlacementPalette { get => I("ghostpal", 0); set => Set("ghostpal", value); }
        public static readonly string[] TextSpeeds = { "Relaxed", "Normal", "Quick" };
        public static int TextSpeed { get => I("textspeed", 1); set => Set("textspeed", value); }
        public static float TextDelayScale => TextSpeed == 0 ? 1.45f : TextSpeed == 2 ? 0.55f : 1f;
        public static readonly string[] XRayModes = { "Hold", "Toggle" };
        /// <summary>X-ray turns on and off with a press instead of being held (read every packing frame, so cached).</summary>
        public static bool XRayToggle { get { Cache(); return xrayToggle; } set => Set("xraytoggle", value); }

        /// <summary>
        /// Panels fade instead of sliding and bouncing, idle bobbing and pulsing stop, the paper-wipe
        /// becomes a fade, and the camera neither sweeps in nor shakes (read every frame, so cached).
        /// </summary>
        public static bool ReduceMotion { get { Cache(); return reduceMotion; } set => Set("reducemotion", value); }

        // Settings read every frame: cached here and re-read whenever a setting changes, so the
        // per-frame code never touches the save or builds key strings.
        static bool cached, xrayToggle, reduceMotion;
        static int fidelity = DefaultFidelity;

        static void Cache()
        {
            if (cached) return;
            cached = true;
            xrayToggle = B("xraytoggle", false);
            reduceMotion = B("reducemotion", false);
            // Saves from before the slider only have a preset: its step, or the default step for Custom.
            int preset = I("preset", DefaultFidelity);
            fidelity = Mathf.Clamp(Prefs.HasKey(Prefix + "fidelity") ? I("fidelity", DefaultFidelity) : preset < 4 ? preset : DefaultFidelity, 0, 3);
        }

        // ------------------------------------------------------------------ storage

        const string Prefix = "ptt.set.";

        static float F(string key, float fallback) => Prefs.GetFloat(Prefix + key, fallback);
        static int I(string key, int fallback) => Prefs.GetInt(Prefix + key, fallback);
        static bool B(string key, bool fallback) => Prefs.GetInt(Prefix + key, fallback ? 1 : 0) != 0;

        static void Set(string key, float value) { Prefs.SetFloat(Prefix + key, value); Commit(); }
        static void Set(string key, int value) { Prefs.SetInt(Prefix + key, value); Commit(); }
        static void Set(string key, bool value) { Prefs.SetInt(Prefix + key, value ? 1 : 0); Commit(); }

        static void SetDisplay(string key, int value)
        {
            Prefs.SetInt(Prefix + key, value);
            Prefs.SetInt(Prefix + "displaySet", 1);
            ApplyDisplay();
            Commit();
        }

        static void SetCustom(string key, float value) { Prefs.SetFloat(Prefix + key, value); Prefs.SetInt(Prefix + "preset", 4); Commit(); }
        static void SetCustom(string key, int value) { Prefs.SetInt(Prefix + key, value); Prefs.SetInt(Prefix + "preset", 4); Commit(); }
        static void SetCustom(string key, bool value) { Prefs.SetInt(Prefix + key, value ? 1 : 0); Prefs.SetInt(Prefix + "preset", 4); Commit(); }

        static void SetAudio(string key, float value)
        {
            Prefs.SetFloat(Prefix + key, value);
            CommitAudio();
        }

        // Volume sliders fire every frame while dragged: only touch the mixer, and save later.
        static void CommitAudio()
        {
            unsaved = true;
            ApplyAudio();
            Changed?.Invoke();
        }

        static void Commit()
        {
            unsaved = true;
            cached = false;
            Apply();
            Changed?.Invoke();
        }

        static bool unsaved;

        /// <summary>Write pending changes to disk (when the settings screen closes, and on quit).</summary>
        public static void Save()
        {
            if (!unsaved) return;
            unsaved = false;
            Prefs.Save();
        }

        static void ApplyPreset(int preset)
        {
            if (preset >= 0 && preset < StepRows.Length)
            {
                var r = StepRows[preset];
                Prefs.SetFloat(Prefix + "renderscale", r.scale);
                Prefs.SetInt(Prefix + "aa", r.aa);
                Prefs.SetInt(Prefix + "shadows", r.shadows);
                Prefs.SetInt(Prefix + "ssao", r.ssao ? 1 : 0);
                Prefs.SetInt(Prefix + "outlines", r.outlines ? 1 : 0);
                Prefs.SetInt(Prefix + "dof", r.dof ? 1 : 0);
                Prefs.SetInt(Prefix + "bloom", r.bloom ? 1 : 0);
            }
            if (preset < 4) Prefs.SetInt(Prefix + "fidelity", preset);
            Prefs.SetInt(Prefix + "preset", preset);
            Commit();
        }

        public static void ResetToDefaults()
        {
            foreach (var key in new[]
            {
                "master", "music", "effects", "ambience", "bgmute", "vsync", "framecap", "fov", "uiscale", "preset", "fidelity", "renderscale",
                "aa", "shadows", "ssao", "outlines", "dof", "bloom", "orbit", "invert", "shake", "hints", "tips", "ghostpal", "textspeed",
                "xraytoggle", "reducemotion",
            })
                Prefs.DeleteKey(Prefix + key);
            Bindings.ResetAll();
            PadBindings.ResetAll();
            Commit();
        }

        // ------------------------------------------------------------------ resolutions

        static List<Resolution> resolutions;

        public static List<Resolution> Resolutions
        {
            get
            {
                if (resolutions != null) return resolutions;
                resolutions = Screen.resolutions
                    .GroupBy(r => (r.width, r.height))
                    .Select(g => g.OrderByDescending(r => r.refreshRateRatio.value).First())
                    .Where(r => r.width >= 1024)
                    .OrderBy(r => r.width).ThenBy(r => r.height)
                    .ToList();
                if (resolutions.Count == 0) resolutions.Add(new Resolution { width = Screen.width, height = Screen.height });
                return resolutions;
            }
        }

        public static string ResolutionLabel(int index)
        {
            var list = Resolutions;
            if (index < 0 || index >= list.Count) return $"{Screen.width} × {Screen.height}";
            return $"{list[index].width} × {list[index].height}";
        }

        public static int CurrentResolutionIndex()
        {
            int saved = ResolutionIndex;
            if (saved >= 0 && saved < Resolutions.Count) return saved;
            int best = Resolutions.FindIndex(r => r.width == Screen.width && r.height == Screen.height);
            return best >= 0 ? best : Resolutions.Count - 1;
        }

        // ------------------------------------------------------------------ applying

        public static Camera Camera;
        public static Atmosphere Atmosphere;
        static UniversalRenderPipelineAsset pipeline;
        static List<ScriptableRendererFeature> features;
        static bool backgrounded;

        /// <summary>Called once at boot, after the camera and post stack exist.</summary>
        public static void Init(Camera camera, Atmosphere atmosphere)
        {
            Camera = camera;
            Atmosphere = atmosphere;
            // Work on a copy so tweaking quality never touches the project asset.
            var source = UniversalRenderPipeline.asset;
            if (source != null)
            {
                pipeline = UnityEngine.Object.Instantiate(source);
                QualitySettings.renderPipeline = pipeline;
                var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
                var list = field?.GetValue(pipeline) as ScriptableRendererData[];
                features = list?.Where(d => d != null).SelectMany(d => d.rendererFeatures).Where(f => f != null).ToList() ?? new List<ScriptableRendererFeature>();
            }
            Application.quitting += Save;
            Application.focusChanged += focused =>
            {
                backgrounded = !focused;
                ApplyAudio();
            };
            if (Prefs.GetInt(Prefix + "displaySet", 0) == 1 && !Web.IsBrowser) ApplyDisplay();
            Apply();
        }

        public static void Apply()
        {
            ApplyAudio();
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            int cap = FrameCaps[Mathf.Clamp(FrameCap, 0, FrameCaps.Length - 1)];
            Application.targetFrameRate = VSync ? -1 : cap;

            if (Camera != null)
            {
                Camera.fieldOfView = FieldOfView;
                var data = Camera.GetUniversalAdditionalCameraData();
                data.antialiasing = AntiAliasing switch
                {
                    0 => AntialiasingMode.None,
                    1 => AntialiasingMode.FastApproximateAntialiasing,
                    _ => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                };
                data.antialiasingQuality = AntialiasingQuality.High;
                data.renderShadows = Shadows > 0;
            }

            int step = Fidelity;
            if (pipeline != null)
            {
                pipeline.renderScale = Mathf.Clamp(RenderScale, 0.5f, 2f);
                pipeline.msaaSampleCount = AntiAliasing == 3 ? 4 : 1;
                int s = Mathf.Clamp(Shadows, 0, 4);
                pipeline.shadowDistance = new[] { 0f, 35f, 50f, 70f, 70f }[s];
                pipeline.shadowCascadeCount = new[] { 1, 1, 2, 4, 4 }[s];
                SetPrivate(pipeline, "m_MainLightShadowmapResolution", new[] { 512, 1024, 2048, 4096, 8192 }[s]);
                SetPrivate(pipeline, "m_SoftShadowsSupported", s >= 2);
                // UniversalRenderPipeline's SoftShadowQuality: 2 Medium, 3 High (a uniform on desktop, not a variant).
                SetPrivate(pipeline, "m_SoftShadowQuality", s >= 3 ? 3 : 2);
            }
            foreach (var f in features ?? Enumerable.Empty<ScriptableRendererFeature>())
            {
                if (f.name == "PTT Ink Outline") f.SetActive(Outlines);
                else if (f.GetType().Name == "ScreenSpaceAmbientOcclusion")
                {
                    f.SetActive(AmbientOcclusion);
                    ConfigureOcclusion(f, step);
                }
            }
            // High forces anisotropic filtering on every texture as the game always has (at least 9x); Ultra forces 16x.
            QualitySettings.anisotropicFiltering = step >= 2 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
            Texture.SetGlobalAnisotropicFilteringLimits(step == 3 ? 16 : 9, 16);
            // Surface detail in the toon shader: 0 a single cheap grain octave, 1 today's grain and the street's detail, 2 finer still.
            Shader.SetGlobalFloat(DetailId, step == 0 ? 0f : step == 3 ? 2f : 1f);
            Atmosphere?.ApplySettings(Bloom, DepthOfField, step);
        }

        static readonly int DetailId = Shader.PropertyToID("_PttDetail");

        /// <summary>
        /// Ambient occlusion quality follows the fidelity step: half resolution with a Gaussian blur on Medium (off
        /// there unless its row is switched on; at half resolution it measured no cheaper than High's full pass),
        /// full resolution with the bilateral blur on High, and 12 samples instead of 8 on Ultra (that variant
        /// is kept in the build by Assets/Settings/PC_UltraVariants_RPAsset, see ProjectSetup.EnsureUltraVariants).
        /// </summary>
        static void ConfigureOcclusion(ScriptableRendererFeature ssao, int step)
        {
            var settings = ssao.GetType().GetField("m_Settings", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ssao);
            if (settings == null) return;
            SetInternal(settings, "Downsample", step <= 1);
            SetInternal(settings, "BlurQuality", step <= 1 ? 1 : 0);   // BlurQualityOptions: High (bilateral) 0, Medium (Gaussian) 1
            SetInternal(settings, "Samples", step == 3 ? 0 : 1);       // AOSampleOption: High (12) 0, Medium (8) 1
        }

        /// <summary>What the ambient occlusion is set to right now (for the self-test and the benchmark log).</summary>
        public static string OcclusionDescription()
        {
            var ssao = features?.FirstOrDefault(f => f.GetType().Name == "ScreenSpaceAmbientOcclusion");
            if (ssao == null) return "none";
            if (!ssao.isActive) return "off";
            var settings = ssao.GetType().GetField("m_Settings", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ssao);
            object Get(string name) => settings?.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(settings);
            return $"{Get("Samples")} samples, {((Get("Downsample") as bool?) == true ? "half" : "full")} res, {Get("BlurQuality")} blur";
        }

        /// <summary>One line with what the renderer is actually set to (logged by the benchmark and checked by the self-test).</summary>
        public static string RenderDescription()
        {
            if (pipeline == null) return "no pipeline";
            var res = pipeline.GetType().GetField("m_MainLightShadowmapResolution", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(pipeline);
            return $"scale {pipeline.renderScale:0.##}, MSAA {pipeline.msaaSampleCount}x, AA {Camera?.GetUniversalAdditionalCameraData().antialiasing}, " +
                   $"shadows {(Camera != null && !Camera.GetUniversalAdditionalCameraData().renderShadows ? "off" : $"{res} x{pipeline.shadowCascadeCount} to {pipeline.shadowDistance:0}m, soft {pipeline.supportsSoftShadows}")}, " +
                   $"AO {OcclusionDescription()}, aniso {QualitySettings.anisotropicFiltering}, particles {ParticleDensity:0.##}x, " +
                   $"detail {Shader.GetGlobalFloat(DetailId):0}, {Atmosphere?.Describe()}";
        }

        public static int ShadowMapResolution =>
            pipeline?.GetType().GetField("m_MainLightShadowmapResolution", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(pipeline) is object o ? Convert.ToInt32(o) : 0;

        public static float AppliedRenderScale => pipeline != null ? pipeline.renderScale : 0f;
        public static int AppliedMsaa => pipeline != null ? pipeline.msaaSampleCount : 0;

        static void ApplyAudio()
        {
            AudioListener.volume = backgrounded && MuteInBackground ? 0f : Perceptual(Master);
        }

        /// <summary>Sliders feel linear when the gain follows a curve close to loudness.</summary>
        public static float Perceptual(float slider) => slider <= 0.001f ? 0f : slider * slider;

        static void ApplyDisplay()
        {
            var mode = WindowMode switch
            {
                1 => FullScreenMode.ExclusiveFullScreen,
                2 => FullScreenMode.Windowed,
                _ => FullScreenMode.FullScreenWindow,
            };
            int index = CurrentResolutionIndex();
            var r = Resolutions[Mathf.Clamp(index, 0, Resolutions.Count - 1)];
            Screen.SetResolution(r.width, r.height, mode);
        }

        static void SetInternal(object target, string field, object value) => SetPrivate(target, field, value);

        static void SetPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return;
            try
            {
                f.SetValue(target, f.FieldType.IsEnum ? Enum.ToObject(f.FieldType, value) : value);
            }
            catch (Exception)
            {
                // Field layout changed in this URP version; keep the asset's own value.
            }
        }
    }
}
