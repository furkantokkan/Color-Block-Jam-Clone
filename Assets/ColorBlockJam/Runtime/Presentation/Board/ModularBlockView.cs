using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using UnityEngine;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// Shows one footprint in one colour from tiles of the board's shared BoardTilePool (AD-19, DEC-184), so the
    /// root holds only the tiles it shows. A frozen block also shows the shared Ice material on those same tiles,
    /// and a recomposed footprint keeps it. Its session calls Prepare before Playing and Clear on teardown.
    /// </summary>
    public sealed class ModularBlockView : MonoBehaviour
    {
        // The shown footprint. Owner: this view. Stops: Clear and Release return the shown tiles; OnDestroy drops
        // them with the root.
        private ModularTileSet m_tiles;
        // A pool this view created for itself (editor previews and tests); the board's factory pool otherwise.
        private BoardTilePool m_ownPool;
        private Material m_ice;
        private Color m_iceTint;
        private float m_iceOpacity;
        private float m_iceCracks;

        public int ActivePieceCount => m_tiles?.ShownCount ?? 0;
        public bool IsPrepared => m_tiles != null;
        public bool IsIced => m_ice != null;

        /// <summary>The tile shown at index, below ActivePieceCount.</summary>
        public BoardPieceView GetShownPiece(int index) => m_tiles.GetShown(index);

        /// <summary>Shows footprints of up to cellCapacity cells from the board's shared tiles, outside Playing.</summary>
        public void Prepare(BoardTilePool pool, int cellCapacity)
        {
            if (IsPrepared) throw new InvalidOperationException("Release the previous preparation first.");
            m_tiles = new ModularTileSet(pool, transform, cellCapacity);
        }

        /// <summary>
        /// Shows footprints of up to cellCapacity cells from a tile pool of its own under this root, for an Editor
        /// preview that has no board. The catalog is used only here; Release disposes the pool.
        /// </summary>
        public void Prepare(BoardPieceCatalog catalog, int cellCapacity)
        {
            if (IsPrepared) throw new InvalidOperationException("Release the previous preparation first.");
            if (cellCapacity < 1) throw new ArgumentOutOfRangeException(nameof(cellCapacity));
            m_ownPool = new BoardTilePool(catalog, transform, "Tiles");
            try
            {
                Prepare(m_ownPool, cellCapacity);
            }
            catch
            {
                m_ownPool.Dispose();
                m_ownPool = null;
                throw;
            }
        }

        /// <summary>
        /// Shows the footprint in an atlas colour (DEC-202); an invalid footprint throws and keeps the shown one.
        /// </summary>
        public void SetCells(IReadOnlyList<Vector2Int> cells, AtlasColor color)
        {
            if (!IsPrepared) throw new InvalidOperationException("Prepare the view before assigning cells.");
            m_tiles.Show(cells, Vector3.zero);
            SetColor(color);
            if (m_ice != null) ApplyIce();
        }

        public void SetColor(AtlasColor color)
        {
            for (int i = 0; i < ActivePieceCount; i++) m_tiles.GetShown(i).SetColor(color);
        }

        public void SetClipPlane(Vector4 plane, bool enabled)
        {
            for (int i = 0; i < ActivePieceCount; i++) m_tiles.GetShown(i).SetClipPlane(plane, enabled);
        }

        /// <summary>
        /// Shows the shared Ice material over the shown tiles with these values, and keeps it on the tiles of every
        /// later footprint until ClearIce.
        /// </summary>
        public void SetIce(Material material, Color tint, float opacity, float cracks)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            m_ice = material;
            m_iceTint = tint;
            m_iceOpacity = opacity;
            m_iceCracks = cracks;
            ApplyIce();
        }

        public void ClearIce()
        {
            if (m_ice == null) return;
            m_ice = null;
            for (int i = 0; i < ActivePieceCount; i++) m_tiles.GetShown(i).SetIce(null);
        }

        /// <summary>Returns the shown tiles; the preparation and the Ice state remain.</summary>
        public void Clear() => m_tiles?.Release();

        public void Release()
        {
            ClearIce();
            m_tiles?.Release();
            m_tiles = null;
            m_ownPool?.Dispose();
            m_ownPool = null;
        }

        private void ApplyIce()
        {
            for (int i = 0; i < ActivePieceCount; i++)
            {
                BoardPieceView tile = m_tiles.GetShown(i);
                tile.SetIce(m_ice);
                tile.SetIceValues(m_iceTint, m_iceOpacity, m_iceCracks);
            }
        }

        // The tiles are children of this root and are destroyed with it, so they never return to the shared pool.
        private void OnDestroy()
        {
            m_tiles?.Forget();
            m_tiles = null;
            m_ownPool?.Dispose();
            m_ownPool = null;
        }
    }
}
