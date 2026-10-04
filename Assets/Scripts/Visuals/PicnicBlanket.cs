using System.Collections.Generic;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// The driveway "pile" area: a soft, gently rumpled plaid picnic blanket with a stitched hem
    /// and tassels on the short ends. Mesh and fabric texture are generated to fit the pile size.
    /// </summary>
    public static class PicnicBlanket
    {
        struct Plaid
        {
            public Color Base, Stripe, Accent;
        }

        static readonly Plaid[] Plaids =
        {
            new Plaid { Base = new Color(0.86f, 0.22f, 0.24f), Stripe = new Color(0.98f, 0.95f, 0.9f), Accent = new Color(0.55f, 0.1f, 0.12f) },
            new Plaid { Base = new Color(0.2f, 0.42f, 0.72f), Stripe = new Color(0.95f, 0.93f, 0.86f), Accent = new Color(0.98f, 0.78f, 0.25f) },
            new Plaid { Base = new Color(0.26f, 0.55f, 0.36f), Stripe = new Color(0.96f, 0.92f, 0.8f), Accent = new Color(0.82f, 0.25f, 0.2f) },
            new Plaid { Base = new Color(0.95f, 0.72f, 0.25f), Stripe = new Color(1f, 0.97f, 0.9f), Accent = new Color(0.35f, 0.5f, 0.75f) },
            new Plaid { Base = new Color(0.55f, 0.35f, 0.6f), Stripe = new Color(0.97f, 0.93f, 0.95f), Accent = new Color(0.98f, 0.65f, 0.3f) },
        };

        const float CellsPerUnit = 5f;
        const float TexelsPerUnit = 48f;

        public static GameObject Build(Transform parent, Vector3 min, Vector3 max, int seed)
        {
            var plaid = Plaids[Mathf.Abs(seed) % Plaids.Length];
            float sx = max.x - min.x, sz = max.z - min.z;
            var root = new GameObject("Picnic Blanket");
            root.transform.SetParent(parent, false);

            var mesh = OwnedAssets.Track(root.transform, BuildCloth(sx, sz, seed));
            var cloth = new GameObject("Cloth", typeof(MeshFilter), typeof(MeshRenderer));
            cloth.transform.SetParent(root.transform, false);
            cloth.transform.localPosition = new Vector3(min.x, min.y, min.z);
            cloth.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mat = OwnedAssets.Track(root.transform, new Material(MaterialLibrary.Lit(Color.white, 0.05f)) { name = "Blanket" });
            mat.SetTexture("_BaseMap", CachedTexture(sx, sz, Mathf.Abs(seed) % Plaids.Length));
            mat.SetFloat("_Grain", 0.12f);
            mat.SetFloat("_RimStrength", 0.15f);
            cloth.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Tassels along the short (z) ends, merged into one mesh (one draw, no per-tassel objects).
            var tasselMat = MaterialLibrary.Lit(Color.Lerp(plaid.Stripe, plaid.Base, 0.25f), 0.05f);
            var parts = new List<CombineInstance>();
            var cube = CubeMesh;
            for (int end = 0; end < 2; end++)
            {
                float z = end == 0 ? min.z - 0.12f : max.z + 0.12f;
                for (float x = min.x + 0.18f; x < max.x - 0.1f; x += 0.22f)
                {
                    var pose = Matrix4x4.TRS(new Vector3(x + Mathf.Sin(x * 7f + end) * 0.02f, min.y + 0.008f, z),
                        Quaternion.Euler(0f, Mathf.Sin(x * 11f + end * 3f) * 12f, 0f), new Vector3(0.045f, 0.012f, 0.24f));
                    parts.Add(new CombineInstance { mesh = cube, transform = pose });
                }
            }
            var tassels = new GameObject("Tassels", typeof(MeshFilter), typeof(MeshRenderer));
            tassels.transform.SetParent(root.transform, false);
            var tasselMesh = OwnedAssets.Track(root.transform, new Mesh { name = "Tassels" });
            tasselMesh.CombineMeshes(parts.ToArray(), true, true);
            tassels.GetComponent<MeshFilter>().sharedMesh = tasselMesh;
            tassels.GetComponent<MeshRenderer>().sharedMaterial = tasselMat;
            return root;
        }

        static Mesh cubeMesh;

        static Mesh CubeMesh
        {
            get
            {
                if (cubeMesh != null) return cubeMesh;
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubeMesh = probe.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(probe);
                return cubeMesh;
            }
        }

        // The menu preview and the trip itself build the same blanket back to back: keep the last one.
        static Texture2D cachedTexture;
        static (int, int, int) cachedKey;

        static Texture2D CachedTexture(float sx, float sz, int plaid)
        {
            var key = (Mathf.RoundToInt(sx * 100f), Mathf.RoundToInt(sz * 100f), plaid);
            if (cachedTexture != null && cachedKey == key) return cachedTexture;
            // The old blanket's material is destroyed along with its level this same frame.
            if (cachedTexture != null) Object.Destroy(cachedTexture);
            cachedTexture = BuildTexture(sx, sz, Plaids[plaid]);
            cachedKey = key;
            return cachedTexture;
        }

        static Mesh BuildCloth(float sx, float sz, int seed)
        {
            int nx = Mathf.Max(2, Mathf.CeilToInt(sx * CellsPerUnit));
            int nz = Mathf.Max(2, Mathf.CeilToInt(sz * CellsPerUnit));
            var verts = new Vector3[(nx + 1) * (nz + 1)];
            var uvs = new Vector2[verts.Length];
            float phase = seed * 1.7f;
            for (int j = 0; j <= nz; j++)
            for (int i = 0; i <= nx; i++)
            {
                float u = i / (float)nx, v = j / (float)nz;
                float x = u * sx, z = v * sz;
                // Soft folds, slightly raised hem, edges settling onto the ground.
                float folds = Mathf.Sin(x * 1.9f + phase) * Mathf.Sin(z * 1.3f - phase * 0.7f) * 0.012f
                              + Mathf.Sin((x + z) * 4.1f + phase) * 0.004f;
                float edge = Mathf.Min(Mathf.Min(x, sx - x), Mathf.Min(z, sz - z));
                float hem = edge < 0.16f ? 0.006f * Mathf.Sin(edge / 0.16f * Mathf.PI) : 0f;
                float settle = Mathf.Clamp01(edge / 0.5f);
                float y = 0.008f + (folds + 0.012f) * settle + hem;
                verts[j * (nx + 1) + i] = new Vector3(x, y, z);
                uvs[j * (nx + 1) + i] = new Vector2(u, v);
            }

            var tris = new int[nx * nz * 6];
            int t = 0;
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }

            var mesh = new Mesh { name = "Blanket" };
            if (verts.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Texture2D BuildTexture(float sx, float sz, Plaid plaid)
        {
            int w = Mathf.Clamp(Mathf.RoundToInt(sx * TexelsPerUnit), 64, 2048);
            int h = Mathf.Clamp(Mathf.RoundToInt(sz * TexelsPerUnit), 64, 2048);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var px = new Color32[w * h];
            const float hem = 0.16f;

            // The tartan is separable: stripe and pinstripe weights depend on one axis each, so
            // work them out per column and per row once instead of per pixel.
            var wx = new float[w];
            var bandX = new float[w];
            var pinX = new float[w];
            var weaveX = new float[w];
            for (int x = 0; x < w; x++)
            {
                wx[x] = (x + 0.5f) / w * sx;
                bandX[x] = Band(wx[x], 0.62f, 0.2f);
                pinX[x] = Band(wx[x] + 0.31f, 0.62f, 0.035f);
                weaveX[x] = Mathf.Sin(x * 0.9f);
            }
            var wz = new float[h];
            var bandZ = new float[h];
            var pinZ = new float[h];
            var weaveZ = new float[h];
            for (int y = 0; y < h; y++)
            {
                wz[y] = (y + 0.5f) / h * sz;
                bandZ[y] = Band(wz[y], 0.62f, 0.2f);
                pinZ[y] = Band(wz[y] + 0.31f, 0.62f, 0.035f);
                weaveZ[y] = Mathf.Sin(y * 0.9f);
            }
            Color hemColor = Color.Lerp(plaid.Base, plaid.Accent, 0.55f);

            for (int y = 0; y < h; y++)
            {
                float zEdge = Mathf.Min(wz[y], sz - wz[y]);
                for (int x = 0; x < w; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(wx[x], sx - wx[x]), zEdge);
                    Color c;
                    if (edge < hem)
                    {
                        c = hemColor;
                        // Running stitch along the hem.
                        float along = Mathf.Abs(wx[x] - sx * 0.5f) > Mathf.Abs(wz[y] - sz * 0.5f) * sx / sz ? wz[y] : wx[x];
                        if (Mathf.Abs(edge - hem * 0.55f) < 0.012f && Mathf.Repeat(along * 9f, 1f) < 0.55f) c = plaid.Stripe;
                    }
                    else
                    {
                        // Tartan: two woven stripe sets plus a thin accent pinstripe.
                        float bx = bandX[x], bz = bandZ[y];
                        c = Color.Lerp(plaid.Base, plaid.Stripe, (bx + bz) * 0.425f);
                        if (bx > 0.5f && bz > 0.5f) c = Color.Lerp(c, plaid.Stripe, 0.35f);
                        c = Color.Lerp(c, plaid.Accent, Mathf.Max(pinX[x], pinZ[y]) * 0.8f);
                        // Weave texture.
                        c *= ((x + y) & 1) == 0 ? 0.97f + 0.03f * weaveX[x] * weaveZ[y] : 0.94f * (0.97f + 0.03f * weaveX[x] * weaveZ[y]);
                    }
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>Soft-edged repeating band of the given width, 1 inside, 0 outside.</summary>
        static float Band(float v, float period, float width)
        {
            float d = Mathf.Abs(Mathf.Repeat(v, period) - period * 0.5f);
            return Mathf.Clamp01((width * 0.5f - d) / 0.012f + 0.5f);
        }
    }
}
