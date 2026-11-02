using System;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>What a canvas point lands on; ring corners are <see cref="Outside"/>.</summary>
    internal enum LevelCanvasHitKind
    {
        Outside = 0,
        Cell = 1,
        RingSegment = 2,
        Block = 3,
        Door = 4
    }

    /// <summary>
    /// Result of one Level Editor canvas hit test: the board cell or ring segment under a point and the block, door or
    /// board object that owns it. Two hits are equal when they name the same cell or segment, never by pixel position.
    /// </summary>
    internal readonly struct LevelCanvasHit : IEquatable<LevelCanvasHit>
    {
        public static readonly LevelCanvasHit Outside = new LevelCanvasHit(
            LevelCanvasHitKind.Outside, default, default, -1, -1, null);

        public readonly LevelCanvasHitKind Kind;
        /// <summary>Board cell for Cell and Block hits.</summary>
        public readonly Vector2Int Cell;
        /// <summary>Board side for RingSegment and Door hits.</summary>
        public readonly BoardSide Side;
        /// <summary>Segment index along <see cref="Side"/> for RingSegment and Door hits, otherwise -1.</summary>
        public readonly int Index;
        /// <summary>
        /// Block or door index in the level for Block and Door hits, the board object's index for a Cell hit on one,
        /// otherwise -1.
        /// </summary>
        public readonly int EntityIndex;
        public readonly string EntityId;

        private LevelCanvasHit(
            LevelCanvasHitKind kind, Vector2Int cell, BoardSide side, int index, int entityIndex, string entityId)
        {
            Kind = kind;
            Cell = cell;
            Side = side;
            Index = index;
            EntityIndex = entityIndex;
            EntityId = entityId;
        }

        public static LevelCanvasHit ForCell(Vector2Int cell)
        {
            return new LevelCanvasHit(LevelCanvasHitKind.Cell, cell, default, -1, -1, null);
        }

        /// <summary>A board cell a board object (level cell object) covers, with no block on it.</summary>
        public static LevelCanvasHit ForCell(Vector2Int cell, int obstacleIndex, string obstacleId)
        {
            return new LevelCanvasHit(LevelCanvasHitKind.Cell, cell, default, -1, obstacleIndex, obstacleId);
        }

        public static LevelCanvasHit ForBlock(Vector2Int cell, int blockIndex, string blockId)
        {
            return new LevelCanvasHit(LevelCanvasHitKind.Block, cell, default, -1, blockIndex, blockId);
        }

        public static LevelCanvasHit ForRingSegment(BoardSide side, int index)
        {
            return new LevelCanvasHit(LevelCanvasHitKind.RingSegment, default, side, index, -1, null);
        }

        public static LevelCanvasHit ForDoor(BoardSide side, int index, int doorIndex, string doorId)
        {
            return new LevelCanvasHit(LevelCanvasHitKind.Door, default, side, index, doorIndex, doorId);
        }

        public bool Equals(LevelCanvasHit other)
        {
            return Kind == other.Kind && Cell == other.Cell && Side == other.Side && Index == other.Index
                && EntityIndex == other.EntityIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is LevelCanvasHit other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Kind, Cell, (int)Side, Index, EntityIndex);
        }

        public override string ToString()
        {
            return $"{Kind} cell {Cell} side {Side} index {Index} entity {EntityIndex} {EntityId}";
        }
    }
}
