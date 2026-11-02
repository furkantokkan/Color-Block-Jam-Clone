using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Picking;
using ColorBlockJam.Gameplay.Input;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures
{
    /// <summary>A top view where a screen position equals its board point, in both directions.</summary>
    public sealed class IdentityBoardProjector : IBoardProjector, IFootprintProjector
    {
        public IFootprintProjector Footprint => this;

        public bool TryScreenToBoard(Vector2 screenPosition, out BoardPoint point)
        {
            point = new BoardPoint(screenPosition.x, screenPosition.y);
            return true;
        }

        public ScreenPoint Project(float boardX, float boardY) => new ScreenPoint(boardX, boardY);
    }
}
