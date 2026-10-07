using System.Collections.Generic;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>Read-only views and shortcuts used by <see cref="AutoPilot"/> for scripted test runs.</summary>
    public partial class GameController
    {
        public IReadOnlyList<PackItem> Items => items;
        public PackItem Held => held;
        public Camera Camera => cam;
        public Vehicle CurrentVehicle => vehicle;
        public bool IsPlaying => mode == Mode.Playing;
        public bool IsShowingResults => mode == Mode.Results;
        public bool IsInStory => mode == Mode.Story;
        public bool IsPaused => paused;
        public bool HasValidTarget => held != null && hasTarget && targetValid;

        /// <summary>Start a trip fresh (any trunk saved for it is forgotten first, so scripted runs are repeatable).</summary>
        public void AutoStartLevel(int index)
        {
            ForgetTrunk(GameDatabase.Levels[index].Id);
            StartLevel(index);
        }

        /// <summary>Forget a trip's saved trunk, so its next start is a fresh one.</summary>
        public static void AutoForgetTrunk(string levelId) => ForgetTrunk(levelId);

        /// <summary>The trunk saved for a trip, as stored ("" if none).</summary>
        public static string SavedTrunk(string levelId) => Prefs.GetString(TrunkKey(levelId), "");

        public static int SavedTrunkItems(string levelId) => SavedTrunkCount(levelId);

        /// <summary>The trip the title's CONTINUE starts.</summary>
        public int NextTrip => NextTripIndex();

        public void AutoBeginTrip(int index) => BeginTrip(index);

        public void AutoShowMenu() => ShowMenu();

        public void AutoShowTitle() => ShowTitle(true);

        public void AutoTransitionTrip(int index) => ui.Transition(() => BeginTrip(index));

        public void AutoShowEnding() => ShowEnding();

        public void AutoShowAlbum() => ShowAlbum(true);

        public CameraRig Rig => rig;
        public GameUI Ui => ui;

        public void AutoPause() => PauseGame();

        public void AutoResume() => ResumeGame();

        public void AutoShowMenuAlbum() => ShowAlbum(false);

        /// <summary>What Grandpa would suggest right now (without showing it).</summary>
        public Hint AutoFindHint() => FindHint();

        public void AutoAskGrandpa() => AskGrandpa();

        public PackItem HintItem => hintItem;

        /// <summary>Show Grandpa's ghost for an item at an exact spot (as if he'd suggested it).</summary>
        public void AutoShowHint(PackItem item, Vector3Int pos, Quaternion rotation)
        {
            ClearHint();
            ShowHint(new Hint { Item = item, Pos = pos, Rotation = rotation, Message = "" });
        }

        public void AutoClearHint() => ClearHint();

        /// <summary>How many packed items are drawn see-through right now.</summary>
        public int SeeThroughCount => seeThrough.Count;

        public bool XRayActive => xray;

        public int UndoDepth => undo.Count;

        /// <summary>Has a hint been shown since this attempt began (no seal if so)?</summary>
        public bool HintedThisTry => hintedThisTry;

        /// <summary>The stars on the last postcard.</summary>
        public int LastStars { get; private set; }

        public int FreeCells => grid.FreeCellCount();

        /// <summary>Undo one step, as Z would; false if there's nothing to undo.</summary>
        public bool AutoUndo()
        {
            if (undo.Count == 0) return false;
            Undo();
            return true;
        }

        /// <summary>Take an item out of the trunk and back to the blanket (as undoing its drop would).</summary>
        public void AutoPutBackToPile(PackItem item)
        {
            if (item.State != ItemState.Packed && item.State != ItemState.Dropping) return;
            grid.Remove(item);
            item.SetColliderEnabled(true);
            ReturnToPile(item);
            RefreshHud();
            SaveTrunk();
        }

        /// <summary>The tip on screen right now (lower-case name), or null.</summary>
        public string ActiveTip => activeTip?.ToString().ToLowerInvariant();

        /// <summary>How many times each tip has been shown since the last <see cref="AutoResetTips"/>.</summary>
        public IReadOnlyDictionary<string, int> TipCounts => tipCounts;

        public void AutoResetTips()
        {
            ResetTips();
            tipCounts.Clear();
            placementsThisSession = 0;
        }

        /// <summary>The live placement target while holding an item.</summary>
        public bool CurrentTarget(out Vector3Int pos, out bool valid)
        {
            pos = targetPos;
            valid = targetValid;
            return held != null && hasTarget;
        }

        /// <summary>Where the held item would land if the mouse were at <paramref name="screen"/>, without moving it.</summary>
        public bool PreviewTarget(Vector2 screen, out Vector3Int pos, out bool valid) => PreviewTarget(screen, out pos, out valid, out _);

        public bool PreviewTarget(Vector2 screen, out Vector3Int pos, out bool valid, out string problem)
        {
            pos = default;
            valid = false;
            problem = null;
            if (held == null) return false;
            var saved = (hasTarget, targetValid, targetPos, lastColumn, heightBias);
            Physics.SyncTransforms();
            UpdateTarget(cam.ScreenPointToRay(screen), false);
            bool ok = hasTarget;
            pos = targetPos;
            valid = targetValid;
            problem = targetValid || !ok ? null : ExplainProblem(held.Shape, targetPos.x, targetPos.z);
            (hasTarget, targetValid, targetPos, lastColumn, heightBias) = saved;
            return ok;
        }

        /// <summary>Pick an item up as if it had been clicked.</summary>
        public void AutoHold(PackItem item)
        {
            if (held != null) PutBack();
            TryPickUp(item);
        }

        public void AutoPutBack() => PutBack();

        /// <summary>Every height the held item could rest at in this column (more than one means a shelf or a gap).</summary>
        public List<int> HeldRestingHeights(int x, int z) =>
            held == null ? new List<int>() : grid.RestingHeights(held.Shape, x, z, held.Def.Fragile, new List<int>());

        public Vector3Int TrunkSize => grid.Size;

        /// <summary>Part of the car (wheel well, toolbox, sloped glass, the clown) rather than packing space.</summary>
        public bool IsWall(Vector3Int cell) => grid.IsWall(cell);

        public bool AutoClose()
        {
            if (!CanClose()) return false;
            StartCoroutine(CloseTrunk());
            return true;
        }

        /// <summary>Packs an item at an exact spot through the normal pick-up / place path.</summary>
        public bool AutoPlace(PackItem item, Quaternion orientation, Vector3Int pos)
        {
            if (held != null) PutBack();
            TryPickUp(item);
            if (held != item) return false;
            held.SetOrientation(orientation);
            if (grid.Check(held.Shape, pos, item.Def.Fragile, out _) != PlacementResult.Ok)
            {
                PutBack();
                return false;
            }
            targetPos = pos;
            targetValid = true;
            hasTarget = true;
            PlaceHeld();
            return true;
        }
    }
}
