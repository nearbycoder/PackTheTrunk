using System.Collections.Generic;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// Sound effects and the outdoor ambience bed. Recorded foley (Kenney, CC0) for handling,
    /// landing and UI; offline-rendered stingers for the warm electric-piano moments, the horn and
    /// the engine (see Resources/Audio/CREDITS.md). Voices are pooled, panned to where things happen
    /// on screen and lightly randomised so repeats never sound mechanical.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        public static Sfx Instance { get; private set; }

        const int Voices = 16;
        const float AmbienceBase = 0.55f;

        readonly AudioSource[] voices = new AudioSource[Voices];
        readonly bool[] voiceImportant = new bool[Voices];
        readonly float[] voiceStarted = new float[Voices];
        readonly Dictionary<string, AudioClip[]> banks = new Dictionary<string, AudioClip[]>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        readonly Dictionary<string, int> lastIndex = new Dictionary<string, int>();

        AudioSource birds, wind;
        AudioLowPassFilter birdsFilter, windFilter;
        float ambienceLevel = 1f, ambienceTarget = 1f, ambienceCutoff = 22000f, ambienceCutoffTarget = 22000f;

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("Sfx Voice " + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.priority = 64;
                voices[i] = s;
            }
            // Load every clip up front so the first pickup or slam never stalls on disk.
            foreach (var clip in Resources.LoadAll<AudioClip>("Audio/Sfx"))
            {
                string bank = clip.name;
                int split = bank.LastIndexOf('_');
                if (split > 0 && int.TryParse(bank.Substring(split + 1), out _)) bank = bank.Substring(0, split);
                if (!banks.TryGetValue(bank, out var list)) banks[bank] = list = new AudioClip[0];
                System.Array.Resize(ref list, list.Length + 1);
                list[list.Length - 1] = clip;
                banks[bank] = list;
            }
            birds = Loop("Birds", "Audio/Ambience/birds", out birdsFilter);
            wind = Loop("Wind", "Audio/Ambience/wind", out windFilter);
        }

        AudioSource Loop(string name, string path, out AudioLowPassFilter filter)
        {
            var go = new GameObject("Ambience " + name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = Resources.Load<AudioClip>(path);
            s.loop = true;
            s.playOnAwake = false;
            s.volume = 0f;
            s.priority = 32;
            filter = go.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = 22000f;
            if (s.clip != null)
            {
                s.time = Random.Range(0f, s.clip.length * 0.8f);
                s.Play();
            }
            return s;
        }

        /// <summary>How present the outdoors is: 1 while packing, lower and muffled behind menus.</summary>
        public void SetAmbience(float level, bool muffled)
        {
            ambienceTarget = level;
            ambienceCutoffTarget = muffled ? 1400f : 22000f;
        }

        void Update()
        {
            float dt = UiTime.Delta;
            ambienceLevel = Mathf.MoveTowards(ambienceLevel, ambienceTarget, dt * 0.6f);
            ambienceCutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(ambienceCutoff), Mathf.Log(ambienceCutoffTarget), 1f - Mathf.Exp(-dt * 2f)));
            float v = GameSettings.Perceptual(GameSettings.Ambience) * AmbienceBase * ambienceLevel;
            // Wind breathes slowly under the birds.
            float gust = 0.75f + 0.25f * Mathf.PerlinNoise(UiTime.Now * 0.07f, 0.3f);
            if (birds != null) birds.volume = v;
            if (wind != null) wind.volume = v * 0.7f * gust;
            if (birdsFilter != null) birdsFilter.cutoffFrequency = ambienceCutoff;
            if (windFilter != null) windFilter.cutoffFrequency = ambienceCutoff;
        }

        // ------------------------------------------------------------------ playback

        AudioClip[] Bank(string name)
        {
            if (banks.TryGetValue(name, out var clips)) return clips;
            var list = new List<AudioClip>();
            var single = Resources.Load<AudioClip>("Audio/Sfx/" + name);
            if (single != null) list.Add(single);
            for (int i = 0; i < 6; i++)
            {
                var c = Resources.Load<AudioClip>($"Audio/Sfx/{name}_{i}");
                if (c != null) list.Add(c);
            }
            clips = list.ToArray();
            banks[name] = clips;
            return clips;
        }

        /// <summary>Play a random variation (never the same one twice in a row).</summary>
        public void Play(string bank, float volume = 1f, float pitch = 1f, float pan = 0f, float pitchJitter = 0.04f, float minGap = 0.03f)
        {
            var clips = Bank(bank);
            if (clips.Length == 0) return;
            float now = UiTime.Now;
            if (lastPlayed.TryGetValue(bank, out var last) && now - last < minGap) return;
            lastPlayed[bank] = now;

            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && lastIndex.TryGetValue(bank, out var prev) && prev == index) index = (index + 1) % clips.Length;
            lastIndex[bank] = index;

            var voice = voices[FreeVoice(Important.Contains(bank))];
            voice.Stop();
            voice.clip = clips[index];
            voice.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
            voice.panStereo = Mathf.Clamp(pan, -1f, 1f);
            voice.volume = volume * GameSettings.Perceptual(GameSettings.Effects);
            voice.Play();
        }

        static readonly HashSet<string> Important = new HashSet<string>
        {
            "trip_complete", "chapter", "confirm", "slam", "engine", "honk", "stamp", "star_1", "star_2", "star_3", "popper",
        };

        /// <summary>
        /// An idle voice if there is one; otherwise steal the oldest ordinary sound, so a burst of
        /// thumps can never cut off a stinger like the trip-complete chime.
        /// </summary>
        int FreeVoice(bool important)
        {
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < Voices; i++)
            {
                if (!voices[i].isPlaying)
                {
                    best = i;
                    break;
                }
                float score = voiceStarted[i] + (voiceImportant[i] ? 1000f : 0f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            voiceImportant[best] = important;
            voiceStarted[best] = UiTime.Now;
            return best;
        }

        /// <summary>Stereo position of a world point on screen (-1 left .. 1 right).</summary>
        public static float PanFor(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return 0f;
            var v = cam.WorldToViewportPoint(world);
            return Mathf.Clamp((v.x - 0.5f) * 1.1f, -0.7f, 0.7f);
        }

        // ------------------------------------------------------------------ UI

        public void Hover() => Play("ui_hover", 0.22f, 1.05f, 0f, 0.06f, 0.05f);
        public void Click() => Play("ui_click", 0.5f, 1f, 0f, 0.03f);
        public void Back() => Play("ui_back", 0.5f);
        public void Toggle(bool on) => Play("ui_toggle", 0.45f, on ? 1.12f : 0.9f, 0f, 0.01f);
        public void Tick() => Play("ui_tick", 0.35f, 1f, 0f, 0.08f, 0.04f);
        public void OpenPanel() => Play("ui_open", 0.4f);
        public void ClosePanel() => Play("ui_close", 0.4f);
        public void Page() => Play("page", 0.7f, 1f, 0f, 0.06f);
        public void WhooshIn() => Play("whoosh_in", 0.45f, 1f, 0f, 0.03f);
        public void WhooshOut() => Play("whoosh_out", 0.4f, 1f, 0f, 0.03f);
        public void Confirm() => Play("confirm", 0.55f, 1f, 0f, 0f);
        public void Chapter() => Play("chapter", 0.55f, 1f, 0f, 0f);

        // ------------------------------------------------------------------ story

        public void Message() => Play("message", 0.5f, 1f, 0f, 0.04f);
        public void Pencil() => Play("pencil", 0.4f, 1f, 0f, 0.08f);
        public void Star(int index) => Play("star_" + Mathf.Clamp(index + 1, 1, 3), 0.55f, 1f, 0f, 0f, 0f);
        public void TripComplete() => Play("trip_complete", 0.6f, 1f, 0f, 0f);
        public void Stamp() => Play("stamp", 0.6f, 0.9f);
        public void Check() => Play("check", 0.45f, 1.1f, 0.35f, 0.1f);

        // ------------------------------------------------------------------ packing

        public void Pickup(PackItem item)
        {
            float pan = PanFor(item.transform.position + item.Shape.Center);
            bool bag = Has(item.Def.Id, "suitcase", "carry_on", "duffel", "backpack", "bag", "case", "hamper");
            Play(bag ? "leather" : "cloth", 0.5f, Mathf.Lerp(1.15f, 0.9f, Mathf.InverseLerp(1, 12, item.Def.Volume)), pan, 0.06f);
            Play("ui_tick", 0.18f, 1.6f, pan, 0.05f, 0f);
        }

        public void PutBack(Vector3 at) => Play("cloth", 0.35f, 1.15f, PanFor(at), 0.08f);

        public void Rotate(Vector3 at) => Play("swish", 0.35f, 1f, PanFor(at), 0.12f, 0.02f);

        public void Land(PackItem item, Vector3 at)
        {
            float pan = PanFor(at);
            float size = Mathf.InverseLerp(1, 14, item.Def.Volume);
            float pitch = Mathf.Lerp(1.15f, 0.82f, size);
            string bank = item.Def.Fragile ? "land_glass" : Material(item.Def.Id);
            Play(bank, bank == "land_glass" ? 0.4f : 0.6f, pitch, pan, 0.05f, 0f);
            if (item.Def.Volume >= 5 || bank != "land_soft") Play(item.Def.Volume >= 5 ? "land_heavy" : "land_soft", Mathf.Lerp(0.3f, 0.7f, size), pitch, pan, 0.05f, 0f);
        }

        /// <summary>Soft thump as the pile drops onto the blanket at the start of a trip.</summary>
        public void PileThump(Vector3 at, float size) => Play("land_soft", Mathf.Lerp(0.18f, 0.35f, size), Mathf.Lerp(1.1f, 0.8f, size), PanFor(at), 0.08f, 0.02f);

        public void Error() => Play("nope", 0.5f, 1f, 0f, 0.02f);
        public void Slam() { Play("slam", 0.85f, 0.95f); Play("latch", 0.45f, 0.9f); }
        public void LidOpen() { Play("latch", 0.4f, 1.1f); Play("creak", 0.35f, 1.15f); }
        public void Honk() => Play("honk", 0.45f, 1f, 0f, 0.01f);
        public void Engine() => Play("engine", 0.65f, 1f, 0f, 0.02f);
        public void Popper(Vector3 at) => Play("popper", 0.5f, 1f, PanFor(at), 0.05f);

        static bool Has(string id, params string[] keys)
        {
            foreach (var k in keys) if (id.Contains(k)) return true;
            return false;
        }

        static string Material(string id)
        {
            if (Has(id, "box", "crate", "firewood", "armchair", "clock", "cat_tree", "table", "crib", "rocking", "blocks", "plant_stand",
                    "futon", "paddle", "guitar", "trunk", "folding_chairs", "sled", "skis", "portrait", "kayak", "surfboard", "cutout", "toy_truck"))
                return "land_wood";
            if (Has(id, "cooler", "lantern", "bike", "sink", "grill", "tuba", "trombone", "fridge", "toaster", "paint", "stroller", "shovel",
                    "thermos", "tackle", "bowling", "camp_chair", "turntable", "unicycle", "desk_lamp", "speaker", "car_seat", "playpen"))
                return "land_metal";
            return "land_soft";
        }
    }
}
