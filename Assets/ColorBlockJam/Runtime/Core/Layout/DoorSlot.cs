using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Runtime.Layout
{
    /// <summary>
    /// One boundary edge segment covered by a door: the floor cell behind it,
    /// the side it opens to and where the door piece stands.
    /// </summary>
    public readonly struct DoorSlot
    {
        // Doors stand in the wall ring, a quarter cell beyond the floor edge.
        private const float k_ringOffset = 0.75f;

        public readonly int DoorIndex;
        public readonly int SegmentIndex;
        public readonly Vector2Int Cell;
        public readonly BoardSide Side;
        public readonly PaletteColor Color;

        public DoorSlot(int doorIndex, int segmentIndex, Vector2Int cell, BoardSide side, PaletteColor color)
        {
            DoorIndex = doorIndex;
            SegmentIndex = segmentIndex;
            Cell = cell;
            Side = side;
            Color = color;
        }

        public Vector3 LocalPosition
        {
            get
            {
                Vector2Int outward = GetOutward(Side);
                return new Vector3(Cell.x + outward.x * k_ringOffset, 0f, Cell.y + outward.y * k_ringOffset);
            }
        }

        /// <summary>Door pieces run along X at yaw 0; left and right doors turn 90 degrees.</summary>
        public int Yaw => Side == BoardSide.Left || Side == BoardSide.Right ? 90 : 0;

        public static Vector2Int GetOutward(BoardSide side)
        {
            switch (side)
            {
                case BoardSide.Top: return Vector2Int.up;
                case BoardSide.Bottom: return Vector2Int.down;
                case BoardSide.Left: return Vector2Int.left;
                default: return Vector2Int.right;
            }
        }
    }
}
