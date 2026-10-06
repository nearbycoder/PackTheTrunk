using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PackTheTrunk
{
    /// <summary>
    /// Seeing into the trunk. While you hold something, packed things that hide any part of the
    /// placement ghost turn into faint silhouettes. Holding the X-ray key (Tab, or clicking the left
    /// stick) turns everything packed see-through, and aiming passes through it to the floor and
    /// walls, so a gap under or behind a stack can be targeted directly.
    /// </summary>
    public partial class GameController
    {
        readonly HashSet<PackItem> seeThrough = new HashSet<PackItem>();
        readonly HashSet<PackItem> wantSeeThrough = new HashSet<PackItem>();
        readonly List<PackItem> seeThroughScratch = new List<PackItem>();
        readonly RaycastHit[] rayHits = new RaycastHit[32];
        bool xray;

        bool XRayHeld =>
            Bindings.Held(Bindings.Action.XRay) ||
            (Gamepad.current != null && Gamepad.current.leftStickButton.isPressed);

        /// <summary>Called each playing frame (before aiming): work out what should be see-through.</summary>
        void UpdateSeeThrough()
        {
            xray = XRayHeld;
            wantSeeThrough.Clear();
            if (xray)
            {
                foreach (var item in items)
                    if (IsPacked(item)) wantSeeThrough.Add(item);
            }
            else
            {
                if (held != null && hasTarget) AddGhostOccluders(held.Shape, targetPos, held);
                // Grandpa's hint ghost can be tucked behind things too.
                if (hintItem != null && hintShape != null && hintGhost != null && hintGhost.gameObject.activeSelf)
                    AddGhostOccluders(hintShape, hintPos, hintItem);
            }

            seeThroughScratch.Clear();
            foreach (var item in seeThrough)
                if (!wantSeeThrough.Contains(item)) seeThroughScratch.Add(item);
            foreach (var item in seeThroughScratch)
            {
                if (item != null) item.SetSeeThrough(false);
                seeThrough.Remove(item);
            }
            foreach (var item in wantSeeThrough)
                if (seeThrough.Add(item)) item.SetSeeThrough(true);
        }

        /// <summary>Packed items (other than <paramref name="except"/>) between the camera and any cell of a ghost.</summary>
        void AddGhostOccluders(VoxelShape shape, Vector3Int pos, PackItem except)
        {
            var eye = cam.transform.position;
            foreach (var v in shape.Voxels)
            {
                var centre = vehicle.transform.TransformPoint((Vector3)(pos + v.Pos) + Vector3.one * 0.5f);
                var toCell = centre - eye;
                float distance = toCell.magnitude - 0.45f;
                if (distance <= 0f) continue;
                int n = Physics.RaycastNonAlloc(eye, toCell / toCell.magnitude, rayHits, distance);
                for (int i = 0; i < n; i++)
                {
                    var item = rayHits[i].collider.GetComponentInParent<PackItem>();
                    if (item != null && item != except && IsPacked(item)) wantSeeThrough.Add(item);
                }
            }
        }

        /// <summary>Back to normal (leaving the trunk, closing it, or a new trip).</summary>
        void ClearSeeThrough()
        {
            foreach (var item in seeThrough)
                if (item != null) item.SetSeeThrough(false);
            seeThrough.Clear();
            xray = false;
        }

        /// <summary>The aiming / hover raycast: in X-ray it passes straight through packed things.</summary>
        bool AimRaycast(Ray ray, out RaycastHit hit)
        {
            if (!xray) return Physics.Raycast(ray, out hit, 300f);
            hit = default;
            float best = float.MaxValue;
            int n = Physics.RaycastNonAlloc(ray, rayHits, 300f);
            for (int i = 0; i < n; i++)
            {
                var item = rayHits[i].collider.GetComponentInParent<PackItem>();
                if (item != null && IsPacked(item)) continue;
                if (rayHits[i].distance < best)
                {
                    best = rayHits[i].distance;
                    hit = rayHits[i];
                }
            }
            return best < float.MaxValue;
        }
    }
}
