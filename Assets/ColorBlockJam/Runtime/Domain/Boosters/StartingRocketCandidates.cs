using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;

namespace ColorBlockJam.Domain.Boosters
{
    /// <summary>
    /// The cells the opening Rocket may remove and its seeded choice among them (B-11, B-16). The
    /// candidates are every cell BoosterTargets accepts for Rocket, including cells that split a block,
    /// listed blocks in ascending ID and each block's cells in slice order, so the same board and seed
    /// always choose the same cell. It runs on Home; TrySample creates one local System.Random.
    /// </summary>
    public static class StartingRocketCandidates
    {
        /// <summary>
        /// Writes the candidate board cells into buffer and returns how many there are; 0 when every
        /// on-board block has one cell. A buffer of LevelModel.TotalCellCount always has room.
        /// </summary>
        public static int Collect(IBoardReader board, Cell[] buffer)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            int count = 0;
            for (int block = 0; block < board.BlockCount; block++)
            {
                if (board.GetStatus(block) != BlockStatus.OnBoard)
                {
                    continue;
                }

                Cell origin = board.GetOrigin(block);
                BlockFootprint footprint = board.GetFootprint(block);
                for (int i = 0; i < footprint.CellCount; i++)
                {
                    Cell cell = origin + footprint.GetCell(i);
                    if (!BoosterTargets.CanRemoveCell(board, cell, out int owner, out CommandOutcome _) || owner != block)
                    {
                        continue;
                    }

                    if (count == buffer.Length)
                    {
                        throw new ArgumentException("The buffer needs room for every candidate; size it to the level's cell count.",
                            nameof(buffer));
                    }

                    buffer[count] = cell;
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Chooses one of the first count candidates, each equally likely, from a System.Random seeded
        /// with seed; false when there is no candidate.
        /// </summary>
        public static bool TrySample(Cell[] candidates, int count, int seed, out Cell cell)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (count < 0 || count > candidates.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (count == 0)
            {
                cell = default;
                return false;
            }

            cell = candidates[new Random(seed).Next(count)];
            return true;
        }
    }
}
