using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Picking;
using ColorBlockJam.Gameplay.Input;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>
    /// An orthographic top view at 100 pixels per cell: screen (100x, 100y) is board point (x, y),
    /// and its footprint projector is the exact inverse. MissesPlane makes every ray miss the board.
    /// </summary>
    public sealed class FakeBoardProjector : IBoardProjector
    {
        public const float k_pixelsPerCell = 100f;

        public FakeBoardProjector()
        {
            Footprint = FakeFootprintProjector.CreateOrthographic();
        }

        public bool MissesPlane { get; set; }

        public IFootprintProjector Footprint { get; }

        public static Vector2 ToScreen(float boardX, float boardY) => new Vector2(boardX * k_pixelsPerCell, boardY * k_pixelsPerCell);

        public bool TryScreenToBoard(Vector2 screenPosition, out BoardPoint point)
        {
            point = new BoardPoint(screenPosition.x / k_pixelsPerCell, screenPosition.y / k_pixelsPerCell);
            return !MissesPlane;
        }
    }
}
