using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// Grandpa's tips: one short note the first time each move matters (picking up, aiming, turning,
    /// shelves, fragile things, undo, the camera). Each shows once ever, one at a time, and goes away
    /// when you do the thing it describes (or after a while). Settings → Gameplay turns them off,
    /// and turning them back on shows them all again.
    /// </summary>
    public partial class GameController
    {
        enum Tip { Pickup, Aim, Turn, Shelf, Fragile, Undo, Orbit }

        static readonly Tip[] AllTips = (Tip[])System.Enum.GetValues(typeof(Tip));

        readonly List<Tip> tipQueue = new List<Tip>();
        Tip? activeTip;
        float tipShownAt, tipHiddenAt, invalidAimTime, tripPlayTime;
        int placementsThisSession;

        static string TipKey(Tip tip) => "ptt.tip." + tip.ToString().ToLowerInvariant();

        /// <summary>Forget which tips have been seen, so they all show again.</summary>
        public static void ResetTips()
        {
            foreach (var tip in AllTips) Prefs.DeleteKey(TipKey(tip));
            Prefs.Save();
        }

        // Recordings and benchmarks show the game as it was filmed; the self-test does get tips.
        static readonly bool Capturing = System.Environment.GetCommandLineArgs()
            .Any(a => a == "-pttShowcase" || a == "-pttBench");

        static bool TipsOn => GameSettings.Tips && !Capturing;

        void QueueTip(Tip tip)
        {
            if (!TipsOn || mode != Mode.Playing || Prefs.GetInt(TipKey(tip)) != 0) return;
            if (activeTip == tip || tipQueue.Contains(tip)) return;
            tipQueue.Add(tip);
        }

        string TipText(Tip tip)
        {
            bool blue = GameSettings.PlacementPalette == 1;
            if (GamepadCursor.Active)
                switch (tip)
                {
                    case Tip.Pickup: return "Point with the left stick and press A to pick something up.";
                    case Tip.Aim: return $"Point into the trunk. {(blue ? "Blue" : "Green")} shows where it lands; striped means it won't fit. A drops it.";
                    case Tip.Turn: return "Won't fit like that? X turns it, Y tips it over and RB rolls it sideways.";
                    case Tip.Shelf: return "There's room underneath too! D-pad up / down picks the shelf.";
                    case Tip.Undo: return "Changed your mind? VIEW undoes, and anything packed can be picked back out.";
                    case Tip.Orbit: return "Can't see the gap? The right stick walks you around the car.";
                }
            switch (tip)
            {
                case Tip.Pickup: return "Click something on the blanket to pick it up.";
                case Tip.Aim: return $"Point into the trunk. {(blue ? "Blue" : "Green")} shows where it lands; striped means it won't fit. Click to drop it.";
                case Tip.Turn: return $"Won't fit like that? {K(Bindings.Action.Turn)} turns it, {K(Bindings.Action.Tip)} tips it over and {K(Bindings.Action.Roll)} rolls it sideways.";
                case Tip.Shelf: return $"There's room underneath too! The mouse wheel or {K(Bindings.Action.ShelfUp)} / {K(Bindings.Action.ShelfDown)} picks the shelf.";
                case Tip.Fragile: return "Fragile! Nothing can go on top of it, so it rides up top.";
                case Tip.Undo: return $"Changed your mind? {K(Bindings.Action.Undo)} undoes, and anything packed can be clicked back out.";
                default: return $"Can't see the gap? Right-drag or {K(Bindings.Action.LookLeft)} / {K(Bindings.Action.LookRight)} walks you around the car.";
            }
        }

        static string K(Bindings.Action action) => Bindings.Label(action);

        /// <summary>Has the player just done what the tip on screen is asking for?</summary>
        bool TipDone(Tip tip)
        {
            float shown = Time.unscaledTime - tipShownAt;
            switch (tip)
            {
                case Tip.Pickup: return held != null;
                case Tip.Aim: return (held == null && shown > 0.5f) || shown > 6f;
                case Tip.Turn: return tipRotated || shown > 10f;
                case Tip.Shelf: return tipShelfPicked || shown > 10f;
                case Tip.Orbit: return (tipOrbited && shown > 1.5f) || shown > 9f;
                default: return shown > 7f;
            }
        }

        bool tipRotated, tipShelfPicked, tipOrbited;
        readonly Dictionary<string, int> tipCounts = new Dictionary<string, int>();

        /// <summary>Called every playing frame: fire triggers, retire the current tip, show the next.</summary>
        void UpdateTips()
        {
            if (!TipsOn) return;

            // Triggers that depend on what's happening right now.
            if (held != null && hasTarget && !targetValid) invalidAimTime += Time.deltaTime;
            else invalidAimTime = 0f;
            if (invalidAimTime > 1f) QueueTip(Tip.Turn);
            // A while into a trunk with some depth to it (not the wagon), point out the camera.
            tripPlayTime += Time.deltaTime;
            if (tripPlayTime > 15f && level.Size.y > 1) QueueTip(Tip.Orbit);
            if (held != null && hasTarget && restingHeights.Count >= 2) QueueTip(Tip.Shelf);
            if (rig.IsDragging || rig.IsPadOrbiting || Bindings.Held(Bindings.Action.LookLeft) || Bindings.Held(Bindings.Action.LookRight)) tipOrbited = true;

            if (activeTip is Tip current && TipDone(current))
            {
                activeTip = null;
                tipHiddenAt = Time.unscaledTime;
                ui.HideTip();
            }
            if (activeTip == null && tipQueue.Count > 0 && Time.unscaledTime - tipHiddenAt > 0.45f)
            {
                // Most urgent first (enum order): the camera tip waits until nothing else is pending.
                var next = tipQueue.Min();
                tipQueue.Remove(next);
                if (Prefs.GetInt(TipKey(next)) != 0) return;
                activeTip = next;
                tipShownAt = Time.unscaledTime;
                tipRotated = tipShelfPicked = tipOrbited = false;
                Prefs.SetInt(TipKey(next), 1);
                Prefs.Save();
                ui.ShowTip(TipText(next));
                var name = next.ToString().ToLowerInvariant();
                tipCounts[name] = tipCounts.TryGetValue(name, out var n) ? n + 1 : 1;
                Debug.Log("[Tips] show " + name);
            }
        }

        /// <summary>Leaving the trunk (closing, restarting, menus): drop whatever was showing or queued.</summary>
        void ClearTips()
        {
            tipQueue.Clear();
            activeTip = null;
            invalidAimTime = 0f;
            ui.HideTip();
        }

        void OnTripStartTips()
        {
            tripPlayTime = 0f;
            QueueTip(Tip.Pickup);
        }

        void OnPickedUpTips(PackItem item)
        {
            QueueTip(Tip.Aim);
            if (item.Def.Fragile) QueueTip(Tip.Fragile);
        }

        void OnPlacedTips()
        {
            if (++placementsThisSession >= 3) QueueTip(Tip.Undo);
        }
    }
}
