using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PackTheTrunk
{
    /// <summary>
    /// Seeing into the trunk. While you hold something, packed things that hide any part of the
    /// placement ghost turn into faint silhouettes. Holding the X-ray key (Tab, or clicking the left
    /// stick), or pressing it once in Toggle mode, turns everything packed see-through, and aiming
    /// passes through it to the floor and walls, so a gap under or behind a stack can be targeted
    /// directly.
    /// </summary>
    public partial class GameController
    {
        readonly HashSet<PackItem> seeThrough = new HashSet<PackItem>();
        readonly HashSet<PackItem> wantSeeThrough = new HashSet<PackItem>();
        readonly List<PackItem> seeThroughScratch = new List<PackItem>();
        readonly RaycastHit[] rayHits = new RaycastHit[32];
        bool xray, xrayLatched, xrayClicked;

        /// <summary>A click on a key hint acts on what's in your hands; with empty hands it says so.</summary>
        bool CanClickHeld()
        {
            if (mode != Mode.Playing || paused) return false;
            if (held != null) return true;
            ui.Toast("Pick something up first.", 1.4f);
            return false;
        }

        /// <summary>
        /// Clicking the X-ray key hint: on for as long as something is in your hands (a click, not a
        /// held key, so the mouse alone can aim through a stack). It goes off with the next click, or
        /// when the item is dropped or put back, so it never leaves empty hands unable to pick packed
        /// things back out. If X-ray is already on (a toggled key), the click turns it off.
        /// </summary>
        void ToggleClickedXRay()
        {
            bool on = xrayLatched || xrayClicked;
            xrayLatched = false;
            xrayClicked = !on;
            ui.Toast(xrayClicked ? "X-ray on until you let go of it." : "X-ray off.", 1.6f);
        }

        bool XRayHeld =>
            Bindings.Held(Bindings.Action.XRay) ||
            (Gamepad.current != null && Gamepad.current.leftStickButton.isPressed);

        bool XRayPressed =>
            Bindings.Pressed(Bindings.Action.XRay) ||
            (Gamepad.current != null && Gamepad.current.leftStickButton.wasPressedThisFrame);

        /// <summary>Called each playing frame (before aiming): work out what should be see-through.</summary>
        void UpdateSeeThrough()
        {
            // Settings → Accessibility: X-ray is held (the default), or a press turns it on and the
            // next turns it off, so nobody has to hold a key (or click a stick) while aiming.
            if (GameSettings.XRayToggle)
            {
                if (XRayPressed)
                {
                    // The key turns off X-ray however it was turned on (a click on its key hint too).
                    xrayLatched = !(xrayLatched || xrayClicked);
                    xrayClicked = false;
                    string key = GamepadCursor.Active ? "L3" : Bindings.Label(Bindings.Action.XRay);
                    ui.Toast(xrayLatched ? $"X-ray on. {key} again turns it off." : "X-ray off.", 1.6f);
                }
            }
            else xrayLatched = false;
            if (held == null) xrayClicked = false;
            xray = xrayLatched || xrayClicked || XRayHeld;
            ui.SetXRayOn(xrayLatched || xrayClicked);
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
            xray = xrayLatched = xrayClicked = false;
            ui.SetXRayOn(false);
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
