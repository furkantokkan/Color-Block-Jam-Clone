using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace ColorBlockJam.Runtime.Board
{
    /// <summary>
    /// Attempt-owned footprint of one block root: a validated copy of the cells it shows, never the authored array.
    /// It is empty until the first SetCells, and Clear empties it again.
    /// </summary>
    public sealed class BlockPieceState
    {
        private readonly List<Vector2Int> m_cells;
        private readonly List<Vector2Int> m_candidate;
        private readonly ReadOnlyCollection<Vector2Int> m_readOnlyCells;
        private readonly BlockPieceComposer m_validator;
        private readonly List<BlockPiecePlacement> m_placements;

        public IReadOnlyList<Vector2Int> Cells => m_readOnlyCells;

        public BlockPieceState(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            m_cells = new List<Vector2Int>(capacity);
            m_candidate = new List<Vector2Int>(capacity);
            m_readOnlyCells = m_cells.AsReadOnly();
            m_validator = new BlockPieceComposer(capacity);
            m_placements = new List<BlockPiecePlacement>(capacity * 9);
        }

        public void Clear()
        {
            m_cells.Clear();
            m_candidate.Clear();
            m_placements.Clear();
        }

        public void SetCells(IReadOnlyList<Vector2Int> cells)
        {
            // Validate before either clearing the previous state or copying input.
            m_validator.Compose(cells, m_placements);
            m_candidate.Clear();
            for (int i = 0; i < cells.Count; i++) m_candidate.Add(cells[i]);
            ApplyCandidate();
        }

        private void ApplyCandidate()
        {
            m_cells.Clear();
            for (int i = 0; i < m_candidate.Count; i++) m_cells.Add(m_candidate[i]);
        }
    }
}
