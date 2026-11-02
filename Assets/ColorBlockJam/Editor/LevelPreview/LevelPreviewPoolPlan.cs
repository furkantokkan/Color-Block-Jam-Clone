using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using UnityEngine;

namespace ColorBlockJam.Editor.LevelPreview
{
    /// <summary>
    /// Plans LevelPreviewHost's one pool of block roots (DEC-193): the prepared and required sizes (root count and cell
    /// capacity), grown only to cover the largest level shown so far, and the shared tile budget of the level about to
    /// build. Roots hold no tiles; the factory's shared tile pool grows when a level needs more (DEC-184).
    /// </summary>
    internal sealed class LevelPreviewPoolPlan
    {
        /// <summary>Prepared or required root pool size: root count and cell capacity.</summary>
        internal sealed class Size
        {
            public int Roots;
            public int Cells;

            /// <summary>Grows to cover need; returns true when it already did.</summary>
            public bool Raise(Size need)
            {
                bool covered = Roots >= need.Roots && Cells >= need.Cells;
                Roots = Mathf.Max(Roots, need.Roots);
                Cells = Mathf.Max(Cells, need.Cells);
                return covered;
            }
        }

        private readonly Size m_prepared = new Size();
        private readonly Size m_required = new Size();
        private readonly int[] m_tiles = new int[BlockPieceCapacity.KindCount];
        // The preview never recomposes a block, so no Rocket spare is warmed; AddTileBudget needs somewhere to put it.
        private readonly int[] m_unusedSpare = new int[BlockPieceCapacity.KindCount];

        /// <summary>The prepared size to warm the factory's root pool from; no roots until a level with blocks shows.</summary>
        public Size Prepared => m_prepared;

        /// <summary>The tiles per kind the level started by StartLevel shows.</summary>
        public IReadOnlyList<int> Tiles => m_tiles;

        /// <summary>Forgets the prepared size; the host calls this once its factory and pool are gone.</summary>
        public void Clear()
        {
            m_prepared.Roots = 0;
            m_prepared.Cells = 0;
        }

        /// <summary>Starts a fresh required tally for the level about to build; call once before the AddBlock calls.</summary>
        public void StartLevel()
        {
            m_required.Roots = 0;
            m_required.Cells = 0;
            System.Array.Clear(m_tiles, 0, m_tiles.Length);
        }

        /// <summary>Adds one mapped block's need to the required tally started by StartLevel: one root showing its cells.</summary>
        public void AddBlock(LevelPreviewBlock block)
        {
            m_required.Roots++;
            m_required.Cells = Mathf.Max(m_required.Cells, block.Cells.Count);
            BlockPieceCapacity.AddTileBudget(block.Cells, m_tiles, m_unusedSpare);
        }

        /// <summary>Grows the prepared size to cover the required tally; returns true when it already covered it.</summary>
        public bool GrowToCover()
        {
            return m_prepared.Raise(m_required);
        }
    }
}
