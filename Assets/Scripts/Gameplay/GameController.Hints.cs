using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PackTheTrunk
{
    /// <summary>
    /// "Ask Grandpa" (H or the HINT button): shows where one thing goes, from the solver's complete
    /// packing of this trip. If something already in the trunk is somewhere the solution doesn't
    /// have it, Grandpa suggests moving it first; if nothing can move, he suggests undoing. It only
    /// ever shows a spot that is valid right now, and never places anything itself.
    /// </summary>
    public partial class GameController
    {
        // The HINT button's orange, strong enough to read next to yellow and gold things.
        static readonly Color HintGhostColor = new Color(1f, 0.48f, 0.24f, 0.6f);

        /// <summary>What Grandpa suggested.</summary>
        public struct Hint
        {
            /// <summary>The item to pick up (null when the hint is just "undo").</summary>
            public PackItem Item;
            public Vector3Int Pos;
            public Quaternion Rotation;
            /// <summary>The item is already in the trunk and should be moved here.</summary>
            public bool Move;
            /// <summary>No spot is open: names the packed item that's in the way.</summary>
            public bool Blocked;
            public string Message;
        }

        Transform hintGhost;
        MeshFilter hintFilter;
        PackItem hintItem;
        Quaternion hintRotation;
        Vector3Int hintPos, hintFrom;
        VoxelShape hintShape;
        bool hintFromTrunk;
        float hintUntil, hintBounce;

        public bool HintsAvailable => level != null && Solutions.For(level).Count > 0;

        void EnsureHintGhost()
        {
            if (hintGhost != null) return;
            var go = new GameObject("Hint Ghost", typeof(MeshFilter), typeof(MeshRenderer));
            hintGhost = go.transform;
            hintFilter = go.GetComponent<MeshFilter>();
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.sharedMaterial = MaterialLibrary.Ghost(HintGhostColor);
            go.SetActive(false);
        }

        void AskGrandpa()
        {
            if (mode != Mode.Playing) return;
            ClearHint();
            if (!HintsAvailable)
            {
                sfx.Error();
                ui.Toast("Grandpa's thinking... no hint for this trip, sorry.", 2f);
                return;
            }
            var hint = FindHint();
            if ((hint.Item != null || hint.Blocked) && !hintedThisTry)
            {
                hintedThisTry = true;
                Debug.Log($"[Seal] {level.Id}: asked Grandpa, no seal this attempt");
            }
            sfx.Confirm();
            ui.Toast(hint.Message, 3.4f);
            if (hint.Item == null) return;

            EnsureHintGhost();
            var shape = hint.Item.Def.Shape.Rotated(hint.Rotation);
            hintShape = shape;
            if (hintFilter.sharedMesh != null) Destroy(hintFilter.sharedMesh);
            hintFilter.sharedMesh = VoxelMeshBuilder.Build(shape, 1, Vector3.zero, -0.04f);
            hintGhost.SetPositionAndRotation(vehicle.transform.TransformPoint(hint.Pos), vehicle.transform.rotation);
            hintGhost.gameObject.SetActive(true);
            hintItem = hint.Item;
            hintRotation = hint.Rotation;
            hintPos = hint.Pos;
            hintFromTrunk = hint.Item != held && IsPacked(hint.Item);
            hintFrom = hint.Item.GridPos;
            hintUntil = Time.unscaledTime + 10f;
            hintBounce = 0f;
            Fx.Twinkle(hint.Item.transform.position + hint.Item.Shape.Center);
            Debug.Log($"[Hints] {level.Id}: {hint.Item.Def.Id} -> {hint.Pos}{(hint.Move ? " (move)" : "")}");
        }

        /// <summary>Keep the hint alive (bouncing the item) until it's followed, replaced or times out.</summary>
        void UpdateHint()
        {
            if (hintItem == null) return;
            // Done once the item lands somewhere new (ideally the hinted spot).
            bool followed = hintItem != held && IsPacked(hintItem) && (!hintFromTrunk || hintItem.GridPos != hintFrom);
            if (followed || Time.unscaledTime > hintUntil || mode != Mode.Playing)
            {
                ClearHint();
                return;
            }
            if (hintItem != held && (hintBounce -= Time.unscaledDeltaTime) <= 0f)
            {
                hintItem.Squash(0.35f);
                hintBounce = 0.9f;
            }
        }

        void ClearHint()
        {
            hintItem = null;
            if (hintGhost != null) hintGhost.gameObject.SetActive(false);
        }

        /// <summary>Picking up the hinted item turns it the way the hint shows.</summary>
        void OnPickedUpHint(PackItem item)
        {
            if (item == hintItem && item.Orientation != hintRotation) item.SetOrientation(hintRotation);
        }

        Hint FindHint()
        {
            // Match what's in the trunk against each known solution; use the one it agrees with most.
            var packed = items.Where(i => i != held && IsPacked(i)).ToList();
            List<SolvedPlacement> best = null;
            List<SolvedPlacement> bestUnused = null;
            List<PackItem> bestMisplaced = null;
            foreach (var variant in Solutions.For(level))
            {
                var unused = new List<SolvedPlacement>(variant);
                var misplaced = new List<PackItem>();
                foreach (var item in packed)
                {
                    var cells = CellsOf(item.Shape, item.GridPos);
                    var match = unused.FirstOrDefault(p => p.Def.Id == item.Def.Id && p.Cells.SetEquals(cells));
                    if (match != null) unused.Remove(match);
                    else misplaced.Add(item);
                }
                if (best == null || misplaced.Count < bestMisplaced.Count)
                {
                    best = variant;
                    bestUnused = unused;
                    bestMisplaced = misplaced;
                }
            }

            // 1. Whatever's in hand, if its spot is open.
            if (held != null)
                foreach (var spot in bestUnused.Where(p => p.Def.Id == held.Def.Id))
                    if (FitsNow(spot, null))
                        return Suggest(held, spot, false);

            // 2. Something in the trunk that belongs elsewhere, if it can be lifted and its spot is open.
            foreach (var item in bestMisplaced)
            {
                if (grid.ItemOnTop(item) != null) continue;
                foreach (var spot in bestUnused.Where(p => p.Def.Id == item.Def.Id))
                    if (FitsNow(spot, item))
                        return Suggest(item, spot, true);
            }

            // 3. The next thing off the blanket: essentials first, then extras.
            foreach (var bonus in new[] { false, true })
                foreach (var spot in bestUnused.Where(p => held == null || p.Def.Id != held.Def.Id))
                {
                    var item = items.FirstOrDefault(i => i.IsBonus == bonus && i.Def.Id == spot.Def.Id && i.State == ItemState.Pile);
                    if (item != null && FitsNow(spot, null)) return Suggest(item, spot, false);
                }

            // 4. Nothing fits where it should: something packed is in the way.
            if (bestMisplaced.Count > 0)
                return new Hint { Blocked = true, Message = $"Grandpa's hint: the {bestMisplaced[0].Def.Name} is in the way. {(GamepadCursor.Active ? "VIEW" : Bindings.Label(Bindings.Action.Undo))} undoes a step, or RESTART starts fresh." };
            return new Hint { Message = "Grandpa's hint: you've got it from here. Close it up!" };
        }

        Hint Suggest(PackItem item, SolvedPlacement spot, bool move)
        {
            bool turned = spot.Rotation != Quaternion.identity && Quaternion.Angle(spot.Rotation, Quaternion.identity) > 1f;
            string where = turned ? "goes here, turned like the ghost" : "goes here";
            string message = move ? $"Grandpa's hint: move the {item.Def.Name} over here." : $"Grandpa's hint: the {item.Def.Name} {where}.";
            return new Hint { Item = item, Pos = spot.Min, Rotation = spot.Rotation, Move = move, Message = message };
        }

        /// <summary>Would this spot take its item right now (optionally ignoring an item that's about to move)?</summary>
        bool FitsNow(SolvedPlacement spot, PackItem moving)
        {
            if (moving != null) grid.Remove(moving);
            bool ok = grid.Check(spot.Shape, spot.Min, spot.Def.Fragile, out _) == PlacementResult.Ok;
            if (moving != null) grid.Place(moving, moving.GridPos);
            return ok;
        }

        static HashSet<Vector3Int> CellsOf(VoxelShape shape, Vector3Int pos)
        {
            var set = new HashSet<Vector3Int>();
            foreach (var v in shape.Voxels) set.Add(v.Pos + pos);
            return set;
        }
    }
}
