using System.Collections.Generic;
using UnityEngine;

namespace PackTheTrunk
{
    public enum PlacementResult
    {
        Ok,
        TooBig,
        Blocked,
        Floating,
        OnFragile,
        CrushesFragile,
    }

    /// <summary>Occupancy grid for the trunk interior plus the packing rules.</summary>
    public class TrunkGrid
    {
        const int Empty = 0;
        const int Wall = -1;

        public readonly Vector3Int Size;
        readonly int[,,] cells;
        readonly Dictionary<int, PackItem> items = new Dictionary<int, PackItem>();

        public TrunkGrid(Vector3Int size, IEnumerable<Vector3Int> blocked)
        {
            Size = size;
            cells = new int[size.x, size.y, size.z];
            foreach (var b in blocked)
                if (InBounds(b)) cells[b.x, b.y, b.z] = Wall;
        }

        public bool InBounds(Vector3Int p) =>
            p.x >= 0 && p.y >= 0 && p.z >= 0 && p.x < Size.x && p.y < Size.y && p.z < Size.z;

        public bool IsWall(Vector3Int p) => InBounds(p) && cells[p.x, p.y, p.z] == Wall;

        public PackItem ItemAt(Vector3Int p)
        {
            if (!InBounds(p)) return null;
            int id = cells[p.x, p.y, p.z];
            return id > 0 && items.TryGetValue(id, out var item) ? item : null;
        }

        /// <summary>Returns the first item that would be crushed or the item being stood on, for messages.</summary>
        public PlacementResult Check(VoxelShape shape, Vector3Int offset, bool fragile, out PackItem culprit)
        {
            culprit = null;
            if (shape.Size.x > Size.x || shape.Size.y > Size.y || shape.Size.z > Size.z)
                return PlacementResult.TooBig;

            foreach (var v in shape.Voxels)
            {
                var p = v.Pos + offset;
                if (!InBounds(p) || cells[p.x, p.y, p.z] != Empty)
                    return PlacementResult.Blocked;
            }

            bool supported = false;
            foreach (var v in shape.Voxels)
            {
                if (shape.Contains(v.Pos + Vector3Int.down)) continue;
                var below = v.Pos + offset + Vector3Int.down;
                if (below.y < 0) { supported = true; continue; }
                int id = cells[below.x, below.y, below.z];
                if (id == Wall) { supported = true; continue; }
                if (id == Empty) continue;
                var under = items[id];
                if (under.Def.Fragile)
                {
                    culprit = under;
                    return PlacementResult.OnFragile;
                }
                supported = true;
            }
            if (!supported) return PlacementResult.Floating;

            if (fragile)
            {
                foreach (var v in shape.Voxels)
                {
                    if (shape.Contains(v.Pos + Vector3Int.up)) continue;
                    var above = ItemAt(v.Pos + offset + Vector3Int.up);
                    if (above != null)
                    {
                        culprit = above;
                        return PlacementResult.CrushesFragile;
                    }
                }
            }
            return PlacementResult.Ok;
        }

        /// <summary>Every resting height in a column where the shape fits, lowest first.</summary>
        public List<int> RestingHeights(VoxelShape shape, int x, int z, bool fragile, List<int> heights = null)
        {
            heights ??= new List<int>();
            heights.Clear();
            for (int y = 0; y + shape.Size.y <= Size.y; y++)
                if (Check(shape, new Vector3Int(x, y, z), fragile, out _) == PlacementResult.Ok)
                    heights.Add(y);
            return heights;
        }

        public void Place(PackItem item, Vector3Int offset) => Place(item, item.Shape, offset);

        /// <summary>Place an item as if it had this shape (to try a layout without turning the item itself).</summary>
        public void Place(PackItem item, VoxelShape shape, Vector3Int offset)
        {
            items[item.Uid] = item;
            foreach (var v in shape.Voxels)
            {
                var p = v.Pos + offset;
                cells[p.x, p.y, p.z] = item.Uid;
            }
        }

        public void Remove(PackItem item)
        {
            if (!items.Remove(item.Uid)) return;
            for (int x = 0; x < Size.x; x++)
            for (int y = 0; y < Size.y; y++)
            for (int z = 0; z < Size.z; z++)
                if (cells[x, y, z] == item.Uid) cells[x, y, z] = Empty;
        }

        /// <summary>The first other item resting directly on top of this one, if any.</summary>
        public PackItem ItemOnTop(PackItem item)
        {
            foreach (var v in item.Shape.Voxels)
            {
                var above = ItemAt(v.Pos + item.GridPos + Vector3Int.up);
                if (above != null && above != item) return above;
            }
            return null;
        }

        public int FreeCellCount()
        {
            int count = 0;
            foreach (var c in cells)
                if (c == Empty) count++;
            return count;
        }
    }
}
