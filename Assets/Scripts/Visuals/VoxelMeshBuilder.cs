using System.Collections.Generic;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// Builds a flat-shaded mesh from a voxel shape. Only exterior faces are emitted and the
    /// whole silhouette is inset slightly, so each object reads as one chunky piece rather than
    /// a pile of cubes. One submesh per palette colour.
    /// </summary>
    public static class VoxelMeshBuilder
    {
        static readonly Vector3Int[] Normals =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        public static Mesh Build(VoxelShape shape, int colorCount, Vector3 pivot, float inset = 0.045f)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>[Mathf.Max(1, colorCount)];
            for (int i = 0; i < triangles.Length; i++) triangles[i] = new List<int>();

            foreach (var voxel in shape.Voxels)
            {
                var c = voxel.Pos;
                foreach (var n in Normals)
                {
                    if (shape.Contains(c + n)) continue;
                    Tangents(n, out var t1, out var t2);

                    int start = vertices.Count;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        int s1 = corner == 1 || corner == 2 ? 1 : -1;
                        int s2 = corner >= 2 ? 1 : -1;
                        var pos = (Vector3)c + Vector3.one * 0.5f + (Vector3)n * 0.5f + (Vector3)t1 * (0.5f * s1) + (Vector3)t2 * (0.5f * s2);
                        pos -= (Vector3)n * inset;
                        pos += (Vector3)t1 * EdgeOffset(shape, c, n, t1 * s1, inset) * s1;
                        pos += (Vector3)t2 * EdgeOffset(shape, c, n, t2 * s2, inset) * s2;
                        vertices.Add(pos - pivot);
                        normals.Add(n);
                        uvs.Add(new Vector2(s1 * 0.5f + 0.5f, s2 * 0.5f + 0.5f));
                    }

                    // Unity treats a triangle as front-facing when Cross(b - a, c - a) points at the viewer.
                    bool flip = Vector3.Dot(Vector3.Cross(t1, t2), n) < 0;
                    var tris = triangles[Mathf.Min(voxel.Color, triangles.Length - 1)];
                    if (!flip)
                    {
                        tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
                        tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
                    }
                    else
                    {
                        tris.Add(start); tris.Add(start + 2); tris.Add(start + 1);
                        tris.Add(start); tris.Add(start + 3); tris.Add(start + 2);
                    }
                }
            }

            var mesh = new Mesh { name = "Voxels" };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// How far a face corner moves along a tangent direction so neighbouring faces meet:
        /// shrink at convex edges, stay put on flat continuations, extend at concave edges.
        /// </summary>
        static float EdgeOffset(VoxelShape shape, Vector3Int cell, Vector3Int normal, Vector3Int dir, float inset)
        {
            if (shape.Contains(cell + dir + normal)) return inset;
            if (shape.Contains(cell + dir)) return 0f;
            return -inset;
        }

        static void Tangents(Vector3Int n, out Vector3Int t1, out Vector3Int t2)
        {
            if (n.x != 0) { t1 = new Vector3Int(0, 1, 0); t2 = new Vector3Int(0, 0, 1); }
            else if (n.y != 0) { t1 = new Vector3Int(0, 0, 1); t2 = new Vector3Int(1, 0, 0); }
            else { t1 = new Vector3Int(1, 0, 0); t2 = new Vector3Int(0, 1, 0); }
        }
    }
}
