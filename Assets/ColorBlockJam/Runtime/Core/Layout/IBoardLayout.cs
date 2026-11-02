using UnityEngine;

namespace ColorBlockJam.Runtime.Layout
{
    /// <summary>
    /// Read-only geometry of one level's play area. Blocks, doors, input and the
    /// camera place themselves with it instead of re-deriving the board frame.
    /// Cell (x, y) sits at local (x, 0, y): +X is the column, +Z is the row.
    /// </summary>
    public interface IBoardLayout
    {
        int Width { get; }
        int Height { get; }

        /// <summary>Floor plus the half-cell wall ring, in board-local space.</summary>
        Bounds LocalBounds { get; }

        /// <summary>One slot per door-covered boundary edge segment, grouped by door.</summary>
        int DoorSlotCount { get; }

        /// <summary>False outside the board and on void cells.</summary>
        bool IsFloor(int x, int y);

        Vector3 CellToLocal(Vector2Int cell);

        /// <summary>Rounds to the nearest cell; false when that cell is not floor.</summary>
        bool TryLocalToCell(Vector3 localPosition, out Vector2Int cell);

        DoorSlot GetDoorSlotAt(int index);
    }
}
