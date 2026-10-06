using UnityEngine;

namespace PackTheTrunk
{
    public enum ItemState
    {
        Pile,
        Held,
        Dropping,
        Packed,
    }

    /// <summary>One physical object the player can pick up, rotate and pack.</summary>
    public class PackItem : MonoBehaviour
    {
        static int nextUid = 1;

        public int Uid { get; private set; }
        public ItemDef Def { get; private set; }
        public bool IsBonus { get; private set; }
        public ItemState State { get; set; }
        public Vector3Int GridPos { get; set; }

        /// <summary>Rotation from the authored orientation; always one of the 24 axis-aligned turns.</summary>
        public Quaternion Orientation { get; private set; } = Quaternion.identity;

        /// <summary>The occupied cells in the current orientation.</summary>
        public VoxelShape Shape { get; private set; }

        /// <summary>Where this item rests on the driveway when it isn't in the trunk.</summary>
        public Vector3 PileLocalPosition { get; set; }

        Transform visual;
        Transform model;
        MeshFilter voxelFilter;
        Transform hitbox;
        MeshCollider meshCollider;

        Vector3 targetPosition;
        bool moving;
        float moveSpeed = 14f;
        float squash;
        float hoverAmount;
        bool hovered;
        Quaternion spinFrom = Quaternion.identity;
        float spinT = 1f;

        // Drop animation: fall from the hand to the resting cell.
        Vector3 dropFrom;
        float dropT = -1f;
        System.Action onLanded;

        // Pile entrance: tumble out of the sky onto the blanket with one little bounce.
        bool falling;
        float fallDelay, fallVelocity;
        int bounces;
        System.Action onFallLanded;

        Quaternion spinRotation = Quaternion.identity, tilt = Quaternion.identity;
        Vector3 lastPosition;

        public static PackItem Create(ItemDef def, bool bonus, Transform parent)
        {
            var go = new GameObject(def.Name);
            go.transform.SetParent(parent, false);
            var item = go.AddComponent<PackItem>();
            item.Uid = nextUid++;
            item.Def = def;
            item.IsBonus = bonus;

            // Visual: spins and squashes around the shape centre. Holds the Blender model, or a
            // voxel mesh when no model exists for this item.
            item.visual = new GameObject("Visual").transform;
            item.visual.SetParent(go.transform, false);
            var model = ModelLibrary.Instantiate("Items/" + def.Id, item.visual);
            if (model != null)
            {
                // The FBX root carries its export position; the mesh is already centred on it.
                item.model = model.transform;
                item.model.localPosition = Vector3.zero;
                model.name = def.Name;
            }
            else
            {
                var voxels = new GameObject("Voxels", typeof(MeshFilter), typeof(MeshRenderer));
                voxels.transform.SetParent(item.visual, false);
                item.voxelFilter = voxels.GetComponent<MeshFilter>();
                voxels.GetComponent<MeshRenderer>().sharedMaterials = MaterialLibrary.ForItem(def);
            }

            // Hitbox: the exact occupied cells, so picking matches the grid.
            item.hitbox = new GameObject("Hitbox").transform;
            item.hitbox.SetParent(go.transform, false);
            item.meshCollider = item.hitbox.gameObject.AddComponent<MeshCollider>();

            item.SetOrientation(Quaternion.identity);
            return item;
        }

        /// <summary>Rotate by a quarter turn, animating the visual from the old pose.</summary>
        public void Rotate(Quaternion delta) => SetOrientation(delta * Orientation, delta);

        public void SetOrientation(Quaternion orientation) => SetOrientation(orientation, Quaternion.identity);

        void SetOrientation(Quaternion orientation, Quaternion animateFrom)
        {
            Orientation = Snap(orientation);
            Shape = Def.Shape.Rotated(Orientation);

            var hitMesh = OwnedAssets.Track(this, VoxelMeshBuilder.Build(Shape, 1, Shape.Center, 0f));
            OwnedAssets.Release(this, meshCollider.sharedMesh);
            meshCollider.sharedMesh = hitMesh;
            hitbox.localPosition = Shape.Center;

            if (model != null)
            {
                model.localRotation = Orientation;
            }
            else
            {
                OwnedAssets.Release(this, voxelFilter.sharedMesh);
                voxelFilter.sharedMesh = OwnedAssets.Track(this, VoxelMeshBuilder.Build(Shape, Def.Colors.Length, Shape.Center));
            }
            visual.localPosition = Shape.Center;

            if (animateFrom != Quaternion.identity)
            {
                spinFrom = Quaternion.Inverse(animateFrom);
                spinT = 0f;
                spinRotation = spinFrom;
                visual.localRotation = tilt * spinRotation;
            }
        }

        /// <summary>Round to the nearest axis-aligned rotation so repeated turns never drift.</summary>
        static Quaternion Snap(Quaternion q)
        {
            Vector3 Round(Vector3 v) => new Vector3(Mathf.Round(v.x), Mathf.Round(v.y), Mathf.Round(v.z));
            return Quaternion.LookRotation(Round(q * Vector3.forward), Round(q * Vector3.up));
        }

        public void SetColliderEnabled(bool enabled) => meshCollider.enabled = enabled;

        Renderer[] renderers;
        Material[][] savedMaterials;

        /// <summary>Drawn as a faint see-through silhouette (no outline, no shadow) so you can see past it.</summary>
        public bool SeeThrough { get; private set; }

        public void SetSeeThrough(bool on)
        {
            if (SeeThrough == on) return;
            SeeThrough = on;
            renderers ??= visual.GetComponentsInChildren<Renderer>(true);
            if (on)
            {
                // Tinted with the item's own colour, so you can still tell what's what.
                var c = Def.Colors.Length > 0 ? Def.Colors[0] : Color.white;
                var mat = MaterialLibrary.Ghost(new Color(Mathf.Lerp(c.r, 1f, 0.35f), Mathf.Lerp(c.g, 1f, 0.35f), Mathf.Lerp(c.b, 1f, 0.35f), 0.16f));
                savedMaterials = new Material[renderers.Length][];
                for (int i = 0; i < renderers.Length; i++)
                {
                    savedMaterials[i] = renderers[i].sharedMaterials;
                    var ghosted = new Material[savedMaterials[i].Length];
                    for (int m = 0; m < ghosted.Length; m++) ghosted[m] = mat;
                    renderers[i].sharedMaterials = ghosted;
                }
            }
            else if (savedMaterials != null)
            {
                for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = savedMaterials[i];
                savedMaterials = null;
            }
        }

        /// <summary>Still dropping onto the blanket at the start of a trip.</summary>
        public bool IsFalling => falling;

        public void SetHovered(bool value) => hovered = value;

        public void MoveTo(Vector3 localPosition, bool instant = false, float speed = 14f)
        {
            EndFall();
            targetPosition = localPosition;
            moveSpeed = speed;
            dropT = -1f;
            if (instant)
            {
                transform.localPosition = localPosition;
                moving = false;
            }
            else moving = true;
        }

        public void DropTo(Vector3 localPosition, System.Action landed)
        {
            EndFall();
            dropFrom = transform.localPosition;
            targetPosition = localPosition;
            dropT = 0f;
            moving = false;
            onLanded = landed;
        }

        public void Squash(float amount = 1f) => squash = Mathf.Max(squash, amount);

        /// <summary>Drop in from above after <paramref name="delay"/> seconds, bounce once, then rest.</summary>
        public void FallTo(Vector3 localPosition, float delay, float height, System.Action landed)
        {
            moving = false;
            dropT = -1f;
            targetPosition = localPosition;
            transform.localPosition = localPosition + Vector3.up * height;
            falling = true;
            fallDelay = delay;
            fallVelocity = 0f;
            bounces = 0;
            onFallLanded = landed;
            visual.gameObject.SetActive(delay <= 0f);
        }

        void EndFall()
        {
            if (!falling) return;
            falling = false;
            visual.gameObject.SetActive(true);
            onFallLanded = null;
        }

        void UpdateFall(float dt)
        {
            if (fallDelay > 0f)
            {
                fallDelay -= dt;
                if (fallDelay > 0f) return;
                visual.gameObject.SetActive(true);
            }
            fallVelocity -= 42f * dt;
            var p = transform.localPosition;
            p.y += fallVelocity * dt;
            if (p.y <= targetPosition.y)
            {
                p.y = targetPosition.y;
                if (bounces == 0 && fallVelocity < -5f)
                {
                    fallVelocity = -fallVelocity * 0.22f;
                    bounces++;
                    squash = 1f;
                    var landed = onFallLanded;
                    onFallLanded = null;
                    landed?.Invoke();
                }
                else
                {
                    falling = false;
                    squash = Mathf.Max(squash, 0.35f);
                }
            }
            transform.localPosition = p;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (falling) UpdateFall(dt);
            else if (dropT >= 0f)
            {
                dropT += dt / 0.16f;
                float t = Mathf.Clamp01(dropT);
                var horizontal = Vector3.Lerp(dropFrom, targetPosition, Mathf.SmoothStep(0, 1, Mathf.Min(1, t * 1.6f)));
                horizontal.y = Mathf.Lerp(dropFrom.y, targetPosition.y, t * t);
                transform.localPosition = horizontal;
                if (t >= 1f)
                {
                    dropT = -1f;
                    transform.localPosition = targetPosition;
                    squash = 1f;
                    var landed = onLanded;
                    onLanded = null;
                    landed?.Invoke();
                }
            }
            else if (moving)
            {
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, 1f - Mathf.Exp(-moveSpeed * dt));
                if ((transform.localPosition - targetPosition).sqrMagnitude < 0.00001f)
                {
                    transform.localPosition = targetPosition;
                    moving = false;
                }
            }

            if (spinT < 1f)
            {
                spinT = Mathf.Min(1f, spinT + dt / 0.16f);
                spinRotation = Quaternion.SlerpUnclamped(spinFrom, Quaternion.identity, Ease.OutBack(spinT, 1.6f));
            }
            else spinRotation = Quaternion.identity;

            // Carried things lean into the direction they're being moved, like a real hand-carry.
            var velocity = dt > 0f ? (transform.localPosition - lastPosition) / dt : Vector3.zero;
            lastPosition = transform.localPosition;
            var lean = State == ItemState.Held
                ? Quaternion.Euler(Mathf.Clamp(velocity.z * 1.6f, -16f, 16f), 0f, Mathf.Clamp(-velocity.x * 1.6f, -16f, 16f))
                : Quaternion.identity;
            tilt = Quaternion.Slerp(tilt, lean, 1f - Mathf.Exp(-9f * dt));
            visual.localRotation = tilt * spinRotation;

            float hoverTarget = hovered && (State == ItemState.Pile || State == ItemState.Packed) ? 1f : 0f;
            hoverAmount = Mathf.MoveTowards(hoverAmount, hoverTarget, dt * 8f);
            squash = Mathf.MoveTowards(squash, 0f, dt * 5f);

            float wobble = squash * Mathf.Sin(squash * 18f) * 0.12f;
            float bob = State == ItemState.Held ? Mathf.Sin(Time.time * 4f) * 0.06f : 0f;
            visual.localPosition = Shape.Center + Vector3.up * (hoverAmount * 0.12f + bob);
            visual.localScale = new Vector3(1f + wobble, 1f - wobble, 1f + wobble) * (1f + hoverAmount * 0.04f);
        }
    }
}
