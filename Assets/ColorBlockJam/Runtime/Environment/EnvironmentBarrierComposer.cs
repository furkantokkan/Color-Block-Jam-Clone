using ColorBlockJam.Runtime.Art;
using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Environment
{
    /// <summary>
    /// Composes the wall pieces of each Barrier board object (catalog OBJ-05) at their closed height:
    /// for one barrier at a time it treats the barrier's cells as the only walled region of the mask
    /// and matches the environment rule set on those cells' quadrants only, the same way an interior
    /// wall block is composed, wherever the barrier stands. The floor stays under the cells. It runs
    /// once per level load, on Home.
    /// </summary>
    public sealed class EnvironmentBarrierComposer
    {
        private readonly EnvironmentDatabase m_database;
        private readonly EnvironmentRuleMatcher m_matcher;
        private readonly List<Vector2Int> m_cells = new List<Vector2Int>();

        public EnvironmentBarrierComposer(EnvironmentDatabase database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (!database.Validate(out string error)) throw new ArgumentException(error, nameof(database));

            m_database = database;
            m_matcher = new EnvironmentRuleMatcher(database.RuleSet);
        }

        /// <summary>
        /// Replaces placements with every barrier's pieces and barriers with one range per barrier, in
        /// authored cell-object order. A null level or a level without barriers gives empty lists.
        /// Reading the typed barrier parameters converts them on first use, so call it outside Playing.
        /// </summary>
        public void Compose(
            IBoardLayout layout,
            LevelData level,
            List<EnvironmentPiecePlacement> placements,
            List<EnvironmentBarrier> barriers)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (placements == null) throw new ArgumentNullException(nameof(placements));
            if (barriers == null) throw new ArgumentNullException(nameof(barriers));

            placements.Clear();
            barriers.Clear();
            if (level == null || level.CellObjectCount == 0)
            {
                return;
            }

            // The matcher reads the rule set asset live; see EnvironmentComposer.Compose.
            if (!m_database.Validate(out string error))
            {
                throw new InvalidOperationException("The environment database is invalid: " + error);
            }

            for (int index = 0; index < level.CellObjectCount; index++)
            {
                LevelCellObjectData cellObject = level.GetCellObjectAt(index);
                if (cellObject == null || !cellObject.TryGetEffect(out BarrierObject barrier))
                {
                    continue;
                }

                CheckId(cellObject.Id, barriers);
                CheckCells(layout, cellObject);
                int firstPiece = placements.Count;
                AppendBarrierPieces(layout, cellObject, placements);

                barriers.Add(new EnvironmentBarrier(
                    cellObject.Id, firstPiece, placements.Count - firstPiece, barrier.StartsOpen));
            }
        }

        // The view finds a barrier by its ID, so an empty or repeated ID is refused before it shows anything.
        private static void CheckId(string id, List<EnvironmentBarrier> barriers)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("A barrier needs a cell-object ID.");
            }

            for (int index = 0; index < barriers.Count; index++)
            {
                if (string.Equals(barriers[index].Id, id, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Barrier ID {id} is used twice.");
                }
            }
        }

        private static void CheckCells(IBoardLayout layout, LevelCellObjectData barrier)
        {
            for (int index = 0; index < barrier.CellCount; index++)
            {
                Vector2Int cell = barrier.GetCellAt(index);
                if (!layout.IsFloor(cell.x, cell.y))
                    throw new ArgumentException(
                        $"Barrier {barrier.Id} has cell ({cell.x}, {cell.y}) off the board floor.", nameof(layout));
            }
        }

        // Appends one independently moving barrier: rule-set edges on its outline, BlockFill caps inside.
        // The outline follows only this barrier's cells, including where they touch a void or the board edge.
        // Compose already validated the database and checked every cell is on the floor.
        private void AppendBarrierPieces(IBoardLayout layout, LevelCellObjectData cellObject,
            List<EnvironmentPiecePlacement> output)
        {
            m_cells.Clear();
            for (int cell = 0; cell < cellObject.CellCount; cell++) m_cells.Add(cellObject.GetCellAt(cell));

            AtlasColor tint = m_database.GetBarrierColor();
            m_matcher.Reset(EnvironmentComposer.GetQuadrantBounds(layout));
            for (int y = -1; y <= layout.Height; y++)
            {
                for (int x = -1; x <= layout.Width; x++)
                {
                    if (m_cells.Contains(new Vector2Int(x, y))) continue;
                    for (int quadrant = 0; quadrant < 4; quadrant++)
                    {
                        Vector2Int position = EnvironmentComposer.GetQuadrant(x, y, quadrant);
                        m_matcher.SetFloor(position.x, position.y);
                    }
                }
            }

            foreach (Vector2Int cell in m_cells)
            {
                for (int quadrant = 0; quadrant < 4; quadrant++)
                {
                    Vector2Int position = EnvironmentComposer.GetQuadrant(cell.x, cell.y, quadrant);
                    bool edge = m_matcher.TryMatch(position.x, position.y, out int rule, out int yaw);
                    output.Add(new EnvironmentPiecePlacement(
                        edge ? m_matcher.GetOutput(rule) : m_database.BlockFillPiece,
                        EnvironmentComposer.GetQuadrantCenter(position), yaw, m_database.WallPieceScale, tint));
                }
            }
        }
    }
}
