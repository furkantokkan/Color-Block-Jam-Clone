using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>One block record (BM) read from a CBJ file, before colour and shape resolution.</summary>
    internal sealed class CbjBlock
    {
        internal int Bct;
        internal Vector2Int[] Cells; // Absolute CBJ coordinates (BPMS); may contain duplicates.
    }

    /// <summary>One door record (DM) read from a CBJ file, before side/colour resolution.</summary>
    internal sealed class CbjDoor
    {
        internal int Bct;
        internal Vector2Int[] Cells; // Absolute CBJ coordinates (BPMS).
        internal bool IsHorizontal; // IH
    }

    /// <summary>
    /// The whole CBJ file after safe parsing: floor, walls, doors, blocks and the ring origin, all in the
    /// file's own absolute coordinates. Board conversion (CbjLevelConverter) resolves the project's
    /// (0, 0)-based board from this.
    /// </summary>
    internal sealed class CbjLevelSource
    {
        internal Vector2Int[] FloorCells; // CMS
        internal Vector2Int[] WallCells; // WMS
        internal CbjDoor[] Doors; // DMS
        internal CbjBlock[] Blocks; // BMS
        internal Vector2Int RingOrigin; // BSP
    }
}
