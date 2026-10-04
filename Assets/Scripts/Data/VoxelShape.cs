using System.Collections.Generic;
using UnityEngine;

namespace PackTheTrunk
{
    public struct Voxel
    {
        public Vector3Int Pos;
        public byte Color;

        public Voxel(Vector3Int pos, byte color)
        {
            Pos = pos;
            Color = color;
        }
    }

    /// <summary>An immutable set of unit cubes, normalized so the minimum corner sits at the origin.</summary>
    public class VoxelShape
    {
        public readonly Voxel[] Voxels;
        public readonly Vector3Int Size;
        readonly HashSet<Vector3Int> lookup;

        public VoxelShape(IEnumerable<Voxel> voxels)
        {
            var list = new List<Voxel>(voxels);
            var min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            foreach (var v in list) min = Vector3Int.Min(min, v.Pos);

            var max = Vector3Int.zero;
            lookup = new HashSet<Vector3Int>();
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i].Pos - min;
                list[i] = new Voxel(p, list[i].Color);
                max = Vector3Int.Max(max, p);
                lookup.Add(p);
            }

            Voxels = list.ToArray();
            Size = max + Vector3Int.one;
        }

        public bool Contains(Vector3Int p) => lookup.Contains(p);

        public Vector3 Center => (Vector3)Size * 0.5f;

        /// <summary>Rotate by a quarter-turn quaternion; cell positions are rounded back to the lattice.</summary>
        public VoxelShape Rotated(Quaternion rotation)
        {
            var result = new List<Voxel>(Voxels.Length);
            foreach (var v in Voxels)
            {
                // Rotate the cell centre, then snap back to the cell's min corner.
                var r = rotation * ((Vector3)v.Pos + Vector3.one * 0.5f);
                var p = new Vector3Int(Mathf.RoundToInt(r.x - 0.5f), Mathf.RoundToInt(r.y - 0.5f), Mathf.RoundToInt(r.z - 0.5f));
                result.Add(new Voxel(p, v.Color));
            }
            return new VoxelShape(result);
        }
    }
}
