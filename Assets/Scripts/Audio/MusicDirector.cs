using System;
using System.Collections.Generic;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// Chill lo-fi soundtrack (TAD's CC0 "lofi Compilation", see Resources/Music/CREDITS.md).
    /// Each trip has its own track. Tracks crossfade between screens and loop seamlessly by
    /// crossfading into themselves. A low-pass filter muffles the music while story texts are on
    /// screen (as if from the next room) and opens up when packing starts; one-shots duck it.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        public const string MenuTrack = "a_cup_of_tea";
        public const string EndingTrack = "countryside";

        static readonly Dictionary<string, string> Titles = new Dictionary<string, string>
        {
            { "a_cup_of_tea", "A Cup of Tea" }, { "countryside", "Countryside" }, { "florist", "Florist" },
            { "rainy_forest", "Rainy Forest" }, { "cat_caffe", "Cat Caffe" }, { "morning_rain", "Morning Rain" },
            { "oceanside", "Oceanside" }, { "cue", "Cue" }, { "bartender", "Bartender" },
        };

        // Full gain at the default 75% music setting matches the original 0.42 mix level.
        const float Volume = 0.75f;
        const float CrossfadeSeconds = 2.5f;
        const float LoopOverlap = 3f;
        const float OpenCutoff = 22000f;
        const float MuffledCutoff = 900f;

        public event Action<string, string> TrackStarted;
        public bool Muted { get; private set; }

        class Deck
        {
            public AudioSource Source;
            public AudioLowPassFilter Filter;
            public float Gain;
            public float Target;
        }

        readonly Deck[] decks = new Deck[2];
        int active;
        string current;
        float cutoff = OpenCutoff, cutoffTarget = OpenCutoff;
        float duck = 1f, duckTimer;
        float duckAmount = 1f;
        float masterFade = 1f;

        void Awake()
        {
            for (int i = 0; i < decks.Length; i++)
            {
                var go = new GameObject("Music Deck " + i);
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.priority = 0;
                source.volume = 0f;
                var filter = go.AddComponent<AudioLowPassFilter>();
                filter.cutoffFrequency = OpenCutoff;
                filter.lowpassResonanceQ = 1f;
                decks[i] = new Deck { Source = source, Filter = filter };
            }
        }

        public static string TitleFor(string track) => Titles.TryGetValue(track ?? "", out var t) ? t : track;

        /// <summary>Crossfade to a track (no-op if it's already playing).</summary>
        public void Play(string track)
        {
            if (string.IsNullOrEmpty(track) || track == current) return;
            var clip = Resources.Load<AudioClip>("Music/" + track);
            if (clip == null)
            {
                Debug.LogWarning("[Music] missing track " + track);
                return;
            }
            current = track;
            StartOnNewDeck(clip, 0f);
            TrackStarted?.Invoke(TitleFor(track), "TAD");
        }

        public void SetMuffled(bool muffled) => cutoffTarget = muffled ? MuffledCutoff : OpenCutoff;

        /// <summary>Dip the music under a one-shot sound.</summary>
        public void Duck(float level, float seconds)
        {
            duckAmount = Mathf.Min(duckTimer > 0f ? duckAmount : 1f, level);
            duckTimer = Mathf.Max(duckTimer, seconds);
        }

        public void ToggleMute() => Muted = !Muted;

        void StartOnNewDeck(AudioClip clip, float startTime)
        {
            var outgoing = decks[active];
            outgoing.Target = 0f;
            active = 1 - active;
            var deck = decks[active];
            deck.Source.clip = clip;
            deck.Source.time = Mathf.Clamp(startTime, 0f, clip.length - 0.1f);
            deck.Source.Play();
            deck.Gain = 0f;
            deck.Target = 1f;
        }

        void Update()
        {
            float dt = UiTime.Delta;
            var deck = decks[active];

            // Seamless loop: crossfade into the start of the same clip before it runs out.
            if (deck.Source.clip != null && deck.Source.isPlaying && deck.Source.time > deck.Source.clip.length - LoopOverlap)
                StartOnNewDeck(deck.Source.clip, 0f);

            if (duckTimer > 0f)
            {
                duckTimer -= dt;
                duck = Mathf.MoveTowards(duck, duckAmount, dt * 4f);
            }
            else duck = Mathf.MoveTowards(duck, 1f, dt * 1.2f);

            masterFade = Mathf.MoveTowards(masterFade, Muted ? 0f : 1f, dt * 2f);

            // Exponential sweep sounds natural for filter cutoffs.
            cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(cutoff), Mathf.Log(cutoffTarget), 1f - Mathf.Exp(-dt * 2.2f)));

            foreach (var d in decks)
            {
                d.Gain = Mathf.MoveTowards(d.Gain, d.Target, dt / CrossfadeSeconds);
                float muffleGain = Mathf.Lerp(0.75f, 1f, Mathf.InverseLerp(MuffledCutoff, OpenCutoff * 0.2f, cutoff));
                d.Source.volume = Volume * GameSettings.Perceptual(GameSettings.Music) * d.Gain * duck * masterFade * muffleGain;
                d.Filter.cutoffFrequency = cutoff;
                if (d.Gain <= 0f && d.Target <= 0f && d.Source.isPlaying) d.Source.Stop();
            }
        }
    }
}
