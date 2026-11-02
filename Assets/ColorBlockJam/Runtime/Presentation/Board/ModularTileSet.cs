using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using UnityEngine;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// Shows one composed footprint under its owner from the board's shared BoardTilePool (AD-19, DEC-184): it
    /// composes, returns the tiles it showed and checks out one tile per composed piece. A waiting tile is reused
    /// without allocating; an empty pool grows by one tile. Owner stop point: Release returns the shown tiles, and
    /// Forget drops them when the owner is destroyed with them.
    /// </summary>
    internal sealed class ModularTileSet
    {
        private readonly BoardTilePool m_pool;
        private readonly Transform m_owner;
        private readonly BlockPieceComposer m_composer;
        private readonly List<BlockPiecePlacement> m_placements;
        private readonly BoardPieceView[] m_shown;
        private readonly BlockPieceKind[] m_shownKinds;
        private int m_shownCount;

        /// <summary>A set that shows footprints of up to cellCapacity cells under owner.</summary>
        public ModularTileSet(BoardTilePool pool, Transform owner, int cellCapacity)
        {
            if (cellCapacity < 1) throw new ArgumentOutOfRangeException(nameof(cellCapacity));
            m_pool = pool ?? throw new ArgumentNullException(nameof(pool));
            m_owner = owner != null ? owner : throw new ArgumentNullException(nameof(owner));
            m_composer = new BlockPieceComposer(cellCapacity);
            int placements = cellCapacity * BlockPieceComposer.MaxPlacementsPerCell;
            m_placements = new List<BlockPiecePlacement>(placements);
            m_shown = new BoardPieceView[placements];
            m_shownKinds = new BlockPieceKind[placements];
        }

        public int ShownCount => m_shownCount;

        public BoardPieceView GetShown(int index) => m_shown[index];

        /// <summary>
        /// Composes first, so an invalid footprint throws through the composer and keeps the shown one; then returns
        /// the shown tiles and checks out one unscaled tile per composed piece at its vertex position plus origin.
        /// </summary>
        public void Show(IReadOnlyList<Vector2Int> cells, Vector3 origin)
        {
            m_composer.Compose(cells, m_placements);
            Release();
            for (int index = 0; index < m_placements.Count; index++)
            {
                BlockPiecePlacement placement = m_placements[index];
                BoardPieceView tile = m_pool.Get(placement.Kind, m_owner);
                tile.transform.SetLocalPositionAndRotation(
                    new Vector3(placement.PositionTwice.x * 0.5f, 0f, placement.PositionTwice.y * 0.5f) + origin,
                    Quaternion.Euler(0f, -90f * placement.QuarterTurns, 0f));
                tile.transform.localScale = Vector3.one;
                m_shownKinds[m_shownCount] = placement.Kind;
                m_shown[m_shownCount++] = tile;
            }
        }

        /// <summary>Returns every shown tile to the shared pool.</summary>
        public void Release()
        {
            for (int index = 0; index < m_shownCount; index++)
            {
                m_pool.Return(m_shownKinds[index], m_shown[index]);
                m_shown[index] = null;
            }
            m_shownCount = 0;
        }

        /// <summary>Drops the shown tiles without returning them, for an owner destroyed together with them.</summary>
        public void Forget()
        {
            for (int index = 0; index < m_shownCount; index++)
            {
                m_pool.Forget(m_shownKinds[index]);
                m_shown[index] = null;
            }
            m_shownCount = 0;
        }
    }
}
