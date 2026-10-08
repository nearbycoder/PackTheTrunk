using System.Collections.Generic;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>Tiny pooled particle system for juice: dust puffs, sparkles, rings and confetti.</summary>
    public class Fx : MonoBehaviour
    {
        enum Shape { Puff = 0, Sparkle = 1, Confetti = 2, Ring = 3 }

        class Particle
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public Vector3 Velocity;
            public float Life, Age, Size, Grow, Gravity, Drag, Spin, Angle;
            public Color Color;
            public bool Billboard;
        }

        static Fx instance;
        static readonly int TintId = Shader.PropertyToID("_Tint");

        readonly List<Particle> live = new List<Particle>();
        readonly Stack<Particle> pool = new Stack<Particle>();
        readonly Material[] materials = new Material[4];
        Mesh quad;
        MaterialPropertyBlock block;
        Camera cam;

        static Fx Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("Fx").AddComponent<Fx>();
                return instance;
            }
        }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            quad = new Mesh { name = "FxQuad" };
            quad.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) });
            quad.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            quad.RecalculateBounds();

            var template = Resources.Load<Material>("Materials/PTT_Fx");
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = template != null ? new Material(template) : new Material(Shader.Find("Sprites/Default"));
                materials[i].SetFloat("_Shape", i);
                materials[i].enableInstancing = true;
            }
        }

        /// <summary>How many of <paramref name="count"/> particles the Graphics fidelity step spawns (at least one).</summary>
        static int Scaled(int count) => Mathf.Max(1, Mathf.RoundToInt(count * GameSettings.ParticleDensity));

        /// <summary>Soft dust ring and a few sparkles where something lands.</summary>
        public static void Land(Vector3 basePosition, Vector3 footprint)
        {
            var fx = Instance;
            float radius = Mathf.Max(footprint.x, footprint.z) * 0.5f;
            int puffs = Scaled(14);
            for (int i = 0; i < puffs; i++)
            {
                float a = i / (float)puffs * Mathf.PI * 2f + Random.value * 0.3f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                fx.Spawn(Shape.Puff, basePosition + dir * radius * 0.8f + Vector3.up * 0.08f, dir * Random.Range(1.2f, 2.2f) + Vector3.up * 0.3f,
                    Random.Range(0.35f, 0.6f), Random.Range(0.25f, 0.45f), 1.8f, new Color(0.95f, 0.9f, 0.82f, 0.55f), 0f, 4f);
            }
            fx.Spawn(Shape.Ring, basePosition + Vector3.up * 0.05f, Vector3.zero, 0.45f, radius * 1.2f, radius * 3.5f, new Color(1f, 0.95f, 0.75f, 0.7f), 0f, 0f, billboard: false);
            for (int i = 0, n = Scaled(6); i < n; i++)
                fx.Spawn(Shape.Sparkle, basePosition + new Vector3(Random.Range(-radius, radius), Random.Range(0.3f, 1f), Random.Range(-radius, radius)),
                    Vector3.up * Random.Range(0.6f, 1.4f), Random.Range(0.4f, 0.7f), Random.Range(0.18f, 0.3f), -0.2f, new Color(1f, 0.95f, 0.6f, 1f), -0.5f, 1f);
        }

        /// <summary>Little twinkle when picking something up.</summary>
        public static void Twinkle(Vector3 position)
        {
            var fx = Instance;
            for (int i = 0, n = Scaled(5); i < n; i++)
                fx.Spawn(Shape.Sparkle, position + Random.insideUnitSphere * 0.5f, Random.insideUnitSphere * 0.6f + Vector3.up,
                    Random.Range(0.35f, 0.55f), Random.Range(0.15f, 0.25f), -0.1f, new Color(1f, 1f, 0.8f, 1f), 0f, 1.5f);
        }

        /// <summary>Celebration burst.</summary>
        public static void Confetti(Vector3 position, int count = 90)
        {
            var fx = Instance;
            Color[] colors = { new Color(1f, 0.36f, 0.4f), new Color(1f, 0.8f, 0.26f), new Color(0.33f, 0.78f, 0.5f), new Color(0.3f, 0.6f, 1f), new Color(0.75f, 0.45f, 1f) };
            count = Scaled(count);
            for (int i = 0; i < count; i++)
            {
                var v = Random.insideUnitSphere * 5f;
                v.y = Mathf.Abs(v.y) * 1.4f + 5f;
                fx.Spawn(Shape.Confetti, position, v, Random.Range(1.6f, 2.6f), Random.Range(0.12f, 0.22f), 0f, colors[i % colors.Length], 7f, 1.4f,
                    spin: Random.Range(-720f, 720f));
            }
        }

        void Spawn(Shape shape, Vector3 position, Vector3 velocity, float life, float size, float grow, Color color, float gravity, float drag,
                   bool billboard = true, float spin = 0f)
        {
            var p = pool.Count > 0 ? pool.Pop() : Create();
            p.Transform.gameObject.SetActive(true);
            p.Renderer.sharedMaterial = materials[(int)shape];
            p.Transform.position = position;
            p.Velocity = velocity;
            p.Life = life;
            p.Age = 0f;
            p.Size = size;
            p.Grow = grow;
            p.Gravity = gravity;
            p.Drag = drag;
            p.Color = color;
            p.Billboard = billboard;
            p.Spin = spin;
            p.Angle = Random.Range(0f, 360f);
            live.Add(p);
        }

        Particle Create()
        {
            var go = new GameObject("Particle", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = quad;
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return new Particle { Transform = go.transform, Renderer = r };
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            float dt = Time.deltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Transform.gameObject.SetActive(false);
                    live.RemoveAt(i);
                    pool.Push(p);
                    continue;
                }
                p.Velocity += Vector3.down * p.Gravity * dt;
                p.Velocity *= Mathf.Exp(-p.Drag * dt);
                p.Transform.position += p.Velocity * dt;
                p.Angle += p.Spin * dt;
                float t = p.Age / p.Life;
                float size = p.Size + p.Grow * t;
                p.Transform.localScale = Vector3.one * Mathf.Max(0.001f, size);
                if (p.Billboard && cam != null)
                    p.Transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, p.Angle);
                else if (!p.Billboard)
                    p.Transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var c = p.Color;
                c.a *= 1f - t * t;
                block.SetColor(TintId, c);
                p.Renderer.SetPropertyBlock(block);
            }
        }
    }
}
