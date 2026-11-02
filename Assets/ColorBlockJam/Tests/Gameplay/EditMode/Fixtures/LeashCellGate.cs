using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>
    /// A cell gate that lets a block enter only cells beside the cells the asked board shows it on,
    /// so a rule asked about a stale board (the block at its committed origin instead of where a
    /// drag or a search shows it) answers differently.
    /// </summary>
    public sealed class LeashCellGate : ICellGate
    {
        public bool AllowsCell(IBoardReader board, int block, Cell cell)
        {
            BlockFootprint footprint = board.GetFootprint(block);
            Cell origin = board.GetOrigin(block);
            for (int i = 0; i < footprint.CellCount; i++)
            {
                Cell own = origin + footprint.GetCell(i);
                if (Math.Abs(own.X - cell.X) + Math.Abs(own.Y - cell.Y) <= 1)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
