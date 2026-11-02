using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using Onity.Pooling;
using UnityEngine;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// A pooled block root that keeps the footprint and colour it shows and forwards them to its ModularBlockView.
    /// Every block is one of these, showing the level's own cells (DEC-193); no prefab carries a shape. A prepared
    /// root is empty, with no cells and no tiles, until its checkout sets the block's cells, so idle roots cost no
    /// tiles. The session explicitly prepares and changes it; its owner passes the shared tile pool, the cells and
    /// the palette colour, so the prefab holds no ScriptableObject reference.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ModularBlockView))]
    public sealed class BlockPiece : MonoBehaviour, IPoolHooks
    {
        [SerializeField] private ModularBlockView m_view;
        private BlockPieceState m_state;
        private AtlasColor m_color;
        // The most cells the prepared state and view hold; TrySetCells refuses a larger footprint.
        private int m_cellCapacity;

        public IReadOnlyList<Vector2Int> Cells => GetState().Cells;
        public bool IsPrepared => m_state != null;
        public ModularBlockView View => m_view;

        /// <summary>
        /// Prepares the root, outside Playing, to show any footprint of up to cellCapacity cells from tiles of the
        /// board's shared pool (DEC-184). It starts empty and checks out no tile until the first SetCells or TrySetCells;
        /// each checkout passes the block's own cells (DEC-193).
        /// </summary>
        public void Prepare(BoardTilePool tiles, int cellCapacity)
        {
            if (IsPrepared) throw new InvalidOperationException("Release the previous piece preparation first.");
            if (m_view == null)
                throw new InvalidOperationException("The piece requires its view before preparation.");
            var state = new BlockPieceState(cellCapacity);
            m_view.Prepare(tiles, cellCapacity);
            m_cellCapacity = cellCapacity;
            m_state = state;
        }

        public void SetCells(IReadOnlyList<Vector2Int> cells)
        {
            BlockPieceState state = GetState();
            m_view.SetCells(cells, m_color);
            state.SetCells(cells);
        }

        /// <summary>
        /// Shows cells and makes them the piece's footprint, or returns false and keeps the shown footprint when
        /// the piece is not prepared, the footprint is empty, larger than the prepared cell capacity or not
        /// 4-connected. The Game presenter recomposes a Rocket part's root through it during Playing (B-19): it
        /// never throws, unlike SetCells. A success reuses waiting shared tiles and allocates nothing unless the
        /// shared pool has to grow (DEC-184); a duplicate or disconnected footprint, which the domain never
        /// produces, is refused through the composer's exception. The domain has already split the block and
        /// passes each part's own cells (DEC-146), so this keeps one connected footprint without a removal rule.
        /// </summary>
        public bool TrySetCells(IReadOnlyList<Vector2Int> cells)
        {
            BlockPieceState state = m_state;
            if (state == null || cells == null || cells.Count == 0 || cells.Count > m_cellCapacity)
            {
                return false;
            }

            try
            {
                // The view composes before it clears, so a refused footprint leaves the shown one unchanged.
                m_view.SetCells(cells, m_color);
            }
            catch (ArgumentException)
            {
                // A duplicate or disconnected footprint; the domain never produces one.
                return false;
            }

            // The view accepted the same cells, so the state's own validation passes.
            state.SetCells(cells);
            return true;
        }

        /// <summary>Sets the block's atlas colour; its tiles sample that cell of the shared atlas (DEC-202).</summary>
        public void SetColor(AtlasColor color)
        {
            GetState();
            m_view.SetColor(color);
            m_color = color;
        }

        /// <summary>Hides the visual tiles; prepared gameplay state remains available.</summary>
        public void Clear() => m_view.Clear();

        public void OnPoolGet() => ResetForPool();
        public void OnPoolRelease() => ResetForPool();

        private void ResetForPool()
        {
            // A root reused for another block starts unfrozen; its tiles go back to the shared pool.
            m_view.ClearIce();
            m_view.Clear();
            m_state?.Clear();
            m_color = default;
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
        }

        public void Release()
        {
            m_view.Release();
            m_state = null;
            m_cellCapacity = 0;
        }

        private BlockPieceState GetState() => m_state ?? throw new InvalidOperationException("Prepare the piece before changing its state.");

        // Deactivation changes nothing: the pool hooks return the tiles before a root is hidden, and a root's hierarchy
        // cannot change while it is being deactivated.
    }
}
