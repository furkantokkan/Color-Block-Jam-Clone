using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using UnityEngine;

namespace ColorBlockJam.Environment
{
    /// <summary>
    /// One piece of the play area: which prefab, where on the environment Grid,
    /// how it turns and its tint. GridPosition is in half-cell quadrant grid units;
    /// the view maps it to local space through the BoardRoot Grid. Every placement
    /// is tinted, because pieces carry no colour of their own (DEC-123).
    /// </summary>
    public readonly struct EnvironmentPiecePlacement
    {
        public readonly BoardPieceView Piece;
        public readonly Vector3 GridPosition;
        public readonly int Yaw;
        public readonly Vector3 Scale;
        public readonly AtlasColor Tint;

        public EnvironmentPiecePlacement(BoardPieceView piece, Vector3 gridPosition, int yaw, Vector3 scale, AtlasColor tint)
        {
            Piece = piece;
            GridPosition = gridPosition;
            Yaw = yaw;
            Scale = scale;
            Tint = tint;
        }
    }
}
