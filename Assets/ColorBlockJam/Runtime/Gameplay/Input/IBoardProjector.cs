using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Picking;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Input
{
    /// <summary>
    /// Converts between screen pixels and the board plane at block-top height: screen to board for
    /// the drag, board to screen for picking (DEC-115).
    /// </summary>
    public interface IBoardProjector
    {
        IFootprintProjector Footprint { get; }

        /// <summary>False when the screen ray misses the board plane.</summary>
        bool TryScreenToBoard(Vector2 screenPosition, out BoardPoint point);
    }
}
