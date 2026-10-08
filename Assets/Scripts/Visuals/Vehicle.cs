using System.Collections.Generic;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>Marks colliders that make up the trunk interior so the cursor can target them.</summary>
    public class TrunkSurface : MonoBehaviour { }

    /// <summary>A procedurally assembled cartoon car wrapped around the trunk grid.</summary>
    public class Vehicle : MonoBehaviour
    {
        public const float GroundY = -1.15f;
        public const float Wall = 0.35f;

        public Transform LidPivot { get; private set; }
        public Quaternion LidOpen { get; private set; }
        public Quaternion LidClosed { get; private set; }
        /// <summary>Rear panel of the lid; folds flat against the lid while the trunk is open.</summary>
        public Transform FlapPivot { get; private set; }
        public Quaternion FlapOpen { get; private set; } = Quaternion.identity;
        public Quaternion FlapClosed { get; private set; } = Quaternion.identity;
        public float Length { get; private set; }

        readonly List<Transform> wheels = new List<Transform>();
        float wheelRadius = 0.72f;

        static readonly Color Carpet = new Color(0.22f, 0.22f, 0.25f);
        static readonly Color CarpetLine = new Color(0.30f, 0.30f, 0.34f);
        static readonly Color Trim = new Color(0.36f, 0.33f, 0.38f);
        static readonly Color Glass = new Color(0.55f, 0.75f, 0.9f);
        static readonly Color Rubber = new Color(0.1f, 0.1f, 0.11f);
        static readonly Color Chrome = new Color(0.8f, 0.82f, 0.85f);

        struct Style
        {
            public float Cabin, Roof, Hood;
            public bool Pickup;
            public bool Dots;
        }

        static Style StyleFor(string name, int h)
        {
            switch (name)
            {
                case "hatch": return new Style { Cabin = 3.0f, Roof = h + 0.9f, Hood = 2.2f };
                case "suv": return new Style { Cabin = 4.0f, Roof = h + 1.0f, Hood = 2.6f };
                case "wagon": return new Style { Cabin = 4.4f, Roof = h + 0.8f, Hood = 3.0f };
                case "mini": return new Style { Cabin = 2.4f, Roof = h + 1.2f, Hood = 1.6f };
                case "pickup": return new Style { Cabin = 3.0f, Roof = h + 1.6f, Hood = 3.0f, Pickup = true };
                case "clown": return new Style { Cabin = 2.2f, Roof = h + 2.4f, Hood = 1.4f, Dots = true };
                case "van": return new Style { Cabin = 5.0f, Roof = h + 0.5f, Hood = 1.8f };
                case "boxtruck": return new Style { Cabin = 3.2f, Roof = h + 0.6f, Hood = 2.0f };
                case "convertible": return new Style { Cabin = 3.2f, Roof = h + 0.2f, Hood = 3.2f };
                case "toywagon": return new Style { Cabin = 0.6f, Roof = h, Hood = 0.6f, Pickup = true };
                default: return new Style { Cabin = 3.6f, Roof = h + 1.3f, Hood = 3.2f };
            }
        }

        public static Vehicle Build(LevelDef level, Transform parent)
        {
            var go = new GameObject("Vehicle");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<Vehicle>();
            v.Construct(level);
            return v;
        }

        void Construct(LevelDef level)
        {
            var size = level.Size;
            float w = size.x, h = size.y, d = size.z, t = Wall;
            var style = StyleFor(level.Style, size.y);
            var body = MaterialLibrary.Lit(level.BodyColor, 0.65f);
            var carpet = MaterialLibrary.Textured(Carpet, 0.35f);
            var trim = MaterialLibrary.Lit(Trim, 0.1f);
            var glass = MaterialLibrary.Lit(Glass, 0.9f);
            var rubber = MaterialLibrary.Lit(Rubber, 0.2f);
            var chrome = MaterialLibrary.Metal(Chrome);

            float floorBottom = -0.3f;
            float bodyBottom = GroundY + 0.45f;
            float cabinStart = d + t;
            float hoodStart = cabinStart + style.Cabin;
            float front = hoodStart + style.Hood;
            Length = front + 0.8f;

            // A Blender-built body (Resources/Models/Vehicles/<level>.fbx) replaces the box car.
            var model = ModelLibrary.Instantiate("Vehicles/" + (level.ModelId ?? level.Id), transform);
            if (model != null) model.name = $"{level.Vehicle} Body";

            // --- Trunk interior ---------------------------------------------------------------
            // With a model the walls are drawn by the body mesh; the boxes stay as invisible colliders.
            Surface(Box("Floor", new Vector3(0, floorBottom, 0), new Vector3(w, 0, d), carpet));
            Hide(model, Surface(Box("Wall L", new Vector3(-t, floorBottom, -t), new Vector3(0, h, d + t), body)));
            Hide(model, Surface(Box("Wall R", new Vector3(w, floorBottom, -t), new Vector3(w + t, h, d + t), body)));
            Hide(model, Surface(Box("Seat Back", new Vector3(0, floorBottom, d), new Vector3(w, h, d + t), body)));
            // Carpet liners sit just inside the walls so they never z-fight with the bodywork.
            Box("Liner L", new Vector3(0.004f, 0.002f, 0.004f), new Vector3(0.03f, h - 0.01f, d - 0.004f), carpet);
            Box("Liner R", new Vector3(w - 0.03f, 0.002f, 0.004f), new Vector3(w - 0.004f, h - 0.01f, d - 0.004f), carpet);
            Box("Liner Back", new Vector3(0.004f, 0.002f, d - 0.03f), new Vector3(w - 0.004f, h - 0.01f, d - 0.004f), carpet);
            if (!style.Pickup)
                Hide(model, Surface(Box("Sill", new Vector3(0, floorBottom, -t), new Vector3(w, 0.1f, 0), body)));

            // Grid lines on the floor so cells are readable.
            var lineMat = MaterialLibrary.Lit(CarpetLine, 0.05f);
            for (int x = 1; x < size.x; x++)
                Box("Grid X", new Vector3(x - 0.015f, 0, 0), new Vector3(x + 0.015f, 0.006f, d), lineMat);
            for (int z = 1; z < size.z; z++)
                Box("Grid Z", new Vector3(0, 0, z - 0.015f), new Vector3(w, 0.006f, z + 0.015f), lineMat);

            BuildBlocked(level, trim);

            if (model != null)
            {
                UseModel(model.transform, style.Pickup);
                return;
            }

            Box("Rim L", new Vector3(-t - 0.02f, h, -t - 0.02f), new Vector3(0.02f, h + 0.08f, d + t), trim);
            Box("Rim R", new Vector3(w - 0.02f, h, -t - 0.02f), new Vector3(w + t + 0.02f, h + 0.08f, d + t), trim);

            // --- Body -----------------------------------------------------------------------------
            Box("Lower Body", new Vector3(-t, bodyBottom, -0.6f), new Vector3(w + t, floorBottom, front), body);
            Box("Rear Panel", new Vector3(-t, floorBottom, -0.6f), new Vector3(w + t, 0.1f, -t), body);
            Box("Bumper", new Vector3(-t - 0.05f, bodyBottom - 0.05f, -0.85f), new Vector3(w + t + 0.05f, bodyBottom + 0.4f, -0.5f), trim);
            Box("Front Bumper", new Vector3(-t - 0.05f, bodyBottom - 0.05f, front - 0.1f), new Vector3(w + t + 0.05f, bodyBottom + 0.4f, front + 0.25f), trim);
            Box("Plate", new Vector3(w * 0.5f - 0.5f, bodyBottom + 0.5f, -0.63f), new Vector3(w * 0.5f + 0.5f, bodyBottom + 0.85f, -0.6f), MaterialLibrary.Lit(new Color(0.95f, 0.93f, 0.8f)));
            var tail = MaterialLibrary.Glowing(new Color(0.9f, 0.1f, 0.1f), 1.5f);
            Box("Tail L", new Vector3(-t, -0.55f, -0.64f), new Vector3(-t + 0.6f, -0.2f, -0.6f), tail);
            Box("Tail R", new Vector3(w + t - 0.6f, -0.55f, -0.64f), new Vector3(w + t, -0.2f, -0.6f), tail);
            var head = MaterialLibrary.Glowing(new Color(1f, 0.95f, 0.75f), 1.2f);
            Box("Head L", new Vector3(-t + 0.1f, -0.2f, front), new Vector3(-t + 0.8f, 0.25f, front + 0.04f), head);
            Box("Head R", new Vector3(w + t - 0.8f, -0.2f, front), new Vector3(w + t - 0.1f, 0.25f, front + 0.04f), head);

            // Cabin
            float roof = style.Roof;
            if (style.Pickup)
            {
                Box("Cab", new Vector3(-t, 0, cabinStart), new Vector3(w + t, roof, hoodStart), body);
                Box("Cab Rear Window", new Vector3(0.3f, h + 0.4f, cabinStart - 0.03f), new Vector3(w - 0.3f, roof - 0.35f, cabinStart), glass);
            }
            else
            {
                Box("Cabin", new Vector3(-t, h, cabinStart), new Vector3(w + t, roof, hoodStart - 0.6f), body);
                Box("Cabin Low", new Vector3(-t, 0, cabinStart), new Vector3(w + t, h, hoodStart), body);
                Box("Windshield", new Vector3(-t + 0.15f, h + 0.15f, hoodStart - 0.62f), new Vector3(w + t - 0.15f, roof - 0.2f, hoodStart - 0.58f), glass);
                Box("Rear Window", new Vector3(0.15f, h + 0.25f, cabinStart - 0.03f), new Vector3(w - 0.15f, roof - 0.2f, cabinStart), glass);
            }
            Box("Side Window L", new Vector3(-t - 0.03f, h + 0.25f, cabinStart + 0.3f), new Vector3(-t, roof - 0.25f, hoodStart - 0.9f), glass);
            Box("Side Window R", new Vector3(w + t, h + 0.25f, cabinStart + 0.3f), new Vector3(w + t + 0.03f, roof - 0.25f, hoodStart - 0.9f), glass);
            Box("Hood", new Vector3(-t, 0, hoodStart), new Vector3(w + t, Mathf.Min(h * 0.55f, 1.2f), front), body);

            if (style.Dots)
            {
                var rng = new System.Random(7);
                Color[] dotColors = { new Color(1f, 0.85f, 0.1f), new Color(0.2f, 0.6f, 1f), new Color(0.95f, 0.2f, 0.5f), new Color(0.3f, 0.85f, 0.4f) };
                for (int i = 0; i < 14; i++)
                {
                    bool left = i % 2 == 0;
                    float z = (float)rng.NextDouble() * (front - 1f);
                    float y = Mathf.Lerp(bodyBottom + 0.3f, roof - 0.4f, (float)rng.NextDouble());
                    if (y > h && (z < cabinStart || z > hoodStart - 0.6f)) y = Mathf.Lerp(bodyBottom + 0.3f, h - 0.3f, (float)rng.NextDouble());
                    var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    DestroyImmediate(dot.GetComponent<Collider>());
                    dot.transform.SetParent(transform, false);
                    dot.transform.localPosition = new Vector3(left ? -t - 0.02f : w + t + 0.02f, y, z);
                    dot.transform.localScale = new Vector3(0.12f, 0.45f, 0.45f);
                    dot.GetComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Lit(dotColors[rng.Next(dotColors.Length)], 0.6f);
                }
            }

            // Wheels: rear axle under the trunk, front under the hood.
            float rearAxle = Mathf.Max(1.2f, d * 0.5f);
            float frontAxle = hoodStart + style.Hood * 0.45f;
            foreach (float z in new[] { rearAxle, frontAxle })
            {
                Wheel(new Vector3(-t - 0.12f, GroundY + wheelRadius, z), rubber, chrome);
                Wheel(new Vector3(w + t + 0.12f, GroundY + wheelRadius, z), rubber, chrome);
            }

            // Lid (or tailgate for the pickup)
            var pivot = new GameObject(style.Pickup ? "Tailgate" : "Lid").transform;
            pivot.SetParent(transform, false);
            LidPivot = pivot;
            if (style.Pickup)
            {
                pivot.localPosition = new Vector3(w * 0.5f, floorBottom, -t);
                Box("Tailgate Panel", new Vector3(-w * 0.5f, 0, 0), new Vector3(w * 0.5f, h - floorBottom, t), body, pivot);
                LidClosed = Quaternion.identity;
                LidOpen = Quaternion.Euler(-90f, 0f, 0f);
            }
            else
            {
                pivot.localPosition = new Vector3(w * 0.5f, h + 0.08f, d + t);
                Box("Lid Panel", new Vector3(-w * 0.5f - t, 0, -d - 2 * t), new Vector3(w * 0.5f + t, 0.16f, 0), body, pivot);
                var flap = new GameObject("Lid Flap").transform;
                flap.SetParent(pivot, false);
                // Hinged at the lid's far edge; folding -90° tucks it flat under the lid.
                flap.localPosition = new Vector3(0f, 0f, -d - t);
                Box("Lid Rear", new Vector3(-w * 0.5f - t, 0.1f - (h + 0.08f), -t), new Vector3(w * 0.5f + t, 0.16f, 0f), body, flap);
                FlapPivot = flap;
                FlapOpen = Quaternion.Euler(-90f, 0f, 0f);
                flap.localRotation = FlapOpen;
                if (level.Style != "sedan")
                    Box("Lid Glass", new Vector3(-w * 0.5f + 0.2f, 0.16f, -d * 0.55f), new Vector3(w * 0.5f - 0.2f, 0.18f, -0.25f), glass, pivot);
                LidClosed = Quaternion.identity;
                LidOpen = Quaternion.Euler(112f, 0f, 0f);
            }
            pivot.localRotation = LidOpen;
        }

        void BuildBlocked(LevelDef level, Material trim)
        {
            if (level.Blocked.Count == 0) return;

            var voxels = new List<Voxel>();
            var min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            foreach (var b in level.Blocked)
            {
                voxels.Add(new Voxel(b, 0));
                min = Vector3Int.Min(min, b);
            }

            var shape = new VoxelShape(voxels);
            var go = new GameObject("Obstructions");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = OwnedAssets.Track(this, VoxelMeshBuilder.Build(shape, 1, -(Vector3)min, 0.0f));
            go.AddComponent<MeshRenderer>().sharedMaterial = trim;
            go.AddComponent<TrunkSurface>();
            foreach (var b in level.Blocked)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = (Vector3)b + Vector3.one * 0.5f;
                box.size = Vector3.one;
            }
        }

        GameObject Surface(GameObject go)
        {
            go.AddComponent<BoxCollider>();
            go.AddComponent<TrunkSurface>();
            return go;
        }

        static void Hide(GameObject model, GameObject go)
        {
            if (model != null) go.GetComponent<MeshRenderer>().enabled = false;
        }

        /// <summary>Wire up the animated parts of an imported body.</summary>
        void UseModel(Transform model, bool pickup)
        {
            if (ModelLibrary.Find(model, "Obstructions") != null)
            {
                var voxels = transform.Find("Obstructions");
                if (voxels != null) voxels.GetComponent<MeshRenderer>().enabled = false;
            }

            foreach (var t in model.GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("Wheel_")) wheels.Add(t);

            LidPivot = ModelLibrary.Find(model, pickup ? "Tailgate" : "Lid");
            LidClosed = LidPivot.localRotation;
            LidOpen = LidClosed * (pickup ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.Euler(112f, 0f, 0f));

            FlapPivot = pickup ? null : ModelLibrary.Find(model, "LidFlap");
            if (FlapPivot != null)
            {
                // Exported as a sibling of the lid; it must swing with it.
                FlapPivot.SetParent(LidPivot, true);
                FlapClosed = FlapPivot.localRotation;
                FlapOpen = FlapClosed * Quaternion.Euler(-90f, 0f, 0f);
                FlapPivot.localRotation = FlapOpen;
            }
            LidPivot.localRotation = LidOpen;
        }

        GameObject Box(string name, Vector3 min, Vector3 max, Material mat, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = (min + max) * 0.5f;
            go.transform.localScale = max - min;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        void Wheel(Vector3 position, Material rubber, Material chrome)
        {
            var pivot = new GameObject("Wheel").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = position;

            var tire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(tire.GetComponent<Collider>());
            tire.transform.SetParent(pivot, false);
            tire.transform.localRotation = Quaternion.Euler(0, 0, 90);
            tire.transform.localScale = new Vector3(wheelRadius * 2f, 0.22f, wheelRadius * 2f);
            tire.GetComponent<MeshRenderer>().sharedMaterial = rubber;

            var hub = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(hub.GetComponent<Collider>());
            hub.transform.SetParent(pivot, false);
            hub.transform.localScale = new Vector3(0.5f, wheelRadius * 0.9f, 0.22f);
            hub.GetComponent<MeshRenderer>().sharedMaterial = chrome;
            wheels.Add(pivot);
        }

        public void SpinWheels(float distance)
        {
            float degrees = distance / wheelRadius * Mathf.Rad2Deg;
            foreach (var w in wheels) w.Rotate(Vector3.right, degrees, Space.Self);
        }
    }
}
