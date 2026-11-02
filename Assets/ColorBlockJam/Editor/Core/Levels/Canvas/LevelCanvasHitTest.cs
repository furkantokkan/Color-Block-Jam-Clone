using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Translates a local canvas point into a <see cref="LevelCanvasHit"/>: a block, else a board cell with the board
    /// object on it, first, then a door
    /// bar (as deep as the ring, checked first so a gated segment reports its door), then a free ring segment; ring
    /// corners and everything else are Outside.
    /// </summary>
    internal static class LevelCanvasHitTest
    {
        public static LevelCanvasHit Test(LevelCanvasModel model, in LevelCanvasGeometry geometry, Vector2 point)
        {
            if (model == null || !geometry.IsValid)
            {
                return LevelCanvasHit.Outside;
            }

            if (geometry.TryGetCell(point, out Vector2Int cell))
            {
                int blockIndex = model.GetBlockIndexAt(cell);
                if (blockIndex >= 0)
                {
                    return LevelCanvasHit.ForBlock(cell, blockIndex, model.GetBlock(blockIndex).Id);
                }

                // A block drawn over an open barrier wins above; otherwise the cell reports its board object.
                int obstacleIndex = model.GetObstacleIndexAt(cell);
                return obstacleIndex >= 0
                    ? LevelCanvasHit.ForCell(cell, obstacleIndex, model.GetObstacle(obstacleIndex).Id)
                    : LevelCanvasHit.ForCell(cell);
            }

            if (geometry.TryGetEdgeSegment(point, LevelCanvasGeometry.k_doorDepth, out BoardSide side, out int segment))
            {
                int doorIndex = model.GetDoorIndexAt(side, segment);
                if (doorIndex >= 0)
                {
                    return LevelCanvasHit.ForDoor(side, segment, doorIndex, model.GetDoor(doorIndex).Id);
                }
            }

            if (geometry.TryGetEdgeSegment(point, LevelCanvasGeometry.k_ringDepth, out side, out segment))
            {
                return LevelCanvasHit.ForRingSegment(side, segment);
            }

            return LevelCanvasHit.Outside;
        }
    }
}
