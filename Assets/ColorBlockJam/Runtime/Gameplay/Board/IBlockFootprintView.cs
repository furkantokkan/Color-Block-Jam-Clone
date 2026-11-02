using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Board
{
    /// <summary>
    /// Optional capability of a block effect view whose presentation follows a block's runtime footprint, such as
    /// Ice shells and counters (B-10, B-19). The board presenter tests for it with is, as it does for
    /// IBlockedDragFeedback, so IBlockEffectView and views without footprint-bound art stay unchanged. Cells are
    /// relative to the block root, one cell per BoardRoot unit, and are borrowed only for the call.
    /// </summary>
    public interface IBlockFootprintView
    {
        /// <summary>The presenter recomposed the attached block's root to cells after a Rocket removed one of its cells.</summary>
        void SetFootprint(int block, IReadOnlyList<Vector2Int> cells);

        /// <summary>
        /// Replaces Attach for the root of a new Rocket part: block is its runtime ID, source the authored block it
        /// came from and cells its own footprint. A view without this capability receives Attach instead.
        /// </summary>
        void AttachPart(int block, int source, Transform root, IReadOnlyList<Vector2Int> cells);
    }
}
