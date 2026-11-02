using ColorBlockJam.Domain.Board;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// The one encoding of a stored search move that the check and the solution listing share (DEC-178): the block
    /// index above a 3-bit kind, where kinds 0 to 3 slide in a LevelSolverTables direction and kinds 4 to 7 leave
    /// through Side (kind - ExitKindOffset). It also turns a stored move back into a LevelSolutionStep.
    /// </summary>
    internal static class LevelSolverMoveCode
    {
        /// <summary>The first exit kind; the slide directions come before it.</summary>
        public const int ExitKindOffset = LevelSolverTables.DirectionCount;

        private const int k_kindBits = 3;
        private const int k_kindMask = (1 << k_kindBits) - 1;

        public static int Encode(int block, int kind) => (block << k_kindBits) | kind;

        public static void Decode(int move, out int block, out int kind)
        {
            block = move >> k_kindBits;
            kind = move & k_kindMask;
        }

        public static bool IsExit(int kind) => kind >= ExitKindOffset;

        /// <summary>
        /// The step from the block's slot before the move to its slot after it, as origins; an exit keeps From as To
        /// and ignores toSlot, because an exited block has no slot.
        /// </summary>
        public static LevelSolutionStep CreateStep(LevelSolverTables tables, int block, int kind, int fromSlot, int toSlot)
        {
            Cell from = tables.GetOrigin(block, fromSlot);
            if (IsExit(kind))
            {
                return new LevelSolutionStep(block, from.X, from.Y, from.X, from.Y, true,
                    (BoardSide)(kind - ExitKindOffset));
            }

            Cell to = tables.GetOrigin(block, toSlot);
            return new LevelSolutionStep(block, from.X, from.Y, to.X, to.Y, false, BoardSide.Top);
        }
    }
}
