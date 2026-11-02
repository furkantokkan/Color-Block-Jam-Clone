using System;
using System.Collections.Generic;
using Onity.Factory;
using Onity.Pooling;
using UnityEngine;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// Attempt-owned Onity factory of block roots: one pool of copies of the catalog's bare block root, each showing
    /// the cells its Create passes (DEC-193), and the one shared BoardTilePool they show tiles from, which WarmTiles
    /// fills (DEC-184). Prepare before Playing; dispose at teardown. Exhaustion rejects instead of growing
    /// (Performance Rule 3).
    /// </summary>
    public sealed class BlockPieceFactory : IFactory<BlockPieceSpawnData, BlockPiece>, IDisposable
    {
        private sealed class PieceSlot
        {
            public BlockPiece Piece;
            public bool InUse;
        }

        private readonly BoardArt m_art;
        private readonly Transform m_parent;
        private readonly Dictionary<BlockPiece, PieceSlot> m_slots = new Dictionary<BlockPiece, PieceSlot>();
        // Owner: this factory. Stops: Dispose releases every root's tiles, then disposes it.
        private readonly BoardTilePool m_tiles;
        // Owner: this factory, from Prepare. Stops: Dispose returns and releases every root, then disposes the pool.
        private PrefabComponentPool<BlockPiece> m_pool;
        private PooledFactory<BlockPiece> m_rootFactory;
        private PieceSlot[] m_prepared = Array.Empty<PieceSlot>();
        private bool m_disposed;

        /// <summary>Block roots the factory instantiated in Prepare.</summary>
        public int PreparedCount => m_prepared.Length;

        public int ActiveCount { get; private set; }

        /// <summary>The tiles every root of this factory shows (DEC-184).</summary>
        public BoardTilePool Tiles => m_tiles;

        /// <summary>The art's catalog supplies the block root and the shared tiles; its palette resolves each Create's colour.</summary>
        public BlockPieceFactory(BoardArt art, Transform parent)
        {
            m_art = art ?? throw new ArgumentNullException(nameof(art));
            m_parent = parent != null ? parent : throw new ArgumentNullException(nameof(parent));
            m_tiles = new BoardTilePool(art.Catalog, parent);
        }

        /// <summary>
        /// True when the catalog's block root can host blocks: a BlockPiece whose ModularBlockView sits on the root
        /// itself, with no authored child, since a root shows only the tiles its cells need (DEC-184, DEC-193).
        /// Otherwise false and a sentence naming what to fix. Read-only; call it outside Playing.
        /// </summary>
        public static bool TryGetRoot(BoardPieceCatalog catalog, out BlockPiece root, out string error)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            root = catalog.BlockRoot;
            if (root == null)
            {
                error = "The board piece catalog has no block root; assign the ModularBlock prefab with its BlockPiece.";
                return false;
            }

            if (root.View == null || root.View.gameObject != root.gameObject || root.transform.childCount != 0)
            {
                error = "The board piece catalog's block root must be a bare BlockPiece with its own ModularBlockView and no child.";
                root = null;
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Warms count roots, outside Playing, for footprints of up to cellCapacity cells. A waiting root is empty and
        /// holds no tiles; Create shows the block's cells from the shared pool that WarmTiles fills. Call it once per
        /// factory.
        /// </summary>
        public void Prepare(int count, int cellCapacity)
        {
            ThrowIfDisposed();
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            if (cellCapacity < 1) throw new ArgumentOutOfRangeException(nameof(cellCapacity));
            if (m_pool != null) throw new InvalidOperationException("The block root pool is already prepared.");
            if (!TryGetRoot(m_art.Catalog, out BlockPiece root, out string error)) throw new InvalidOperationException(error);

            var slots = new PieceSlot[count];
            var pool = new PrefabComponentPool<BlockPiece>(root, m_parent, count, count, "Block roots");
            var rootFactory = new PooledFactory<BlockPiece>(pool);
            int created = 0;
            try
            {
                // Rent all before returning, so count distinct roots are prepared; a prepared root checks out no tile.
                for (int i = 0; i < count; i++)
                {
                    var piece = rootFactory.Create();
                    slots[i] = new PieceSlot { Piece = piece };
                    created++;
                    piece.Prepare(m_tiles, cellCapacity);
                }
            }
            catch
            {
                for (int i = 0; i < created; i++)
                {
                    slots[i].Piece.Release();
                    pool.Release(slots[i].Piece);
                }
                pool.Dispose();
                throw;
            }
            for (int i = 0; i < count; i++)
            {
                pool.Release(slots[i].Piece);
                m_slots.Add(slots[i].Piece, slots[i]);
            }
            m_pool = pool;
            m_rootFactory = rootFactory;
            m_prepared = slots;
        }

        /// <summary>
        /// Outside Playing: makes at least tileCounts[kind] shared tiles of each kind exist (BlockTileBudget). A
        /// footprint that needs more still shows: the pool grows by the missing tiles and keeps them (DEC-184).
        /// </summary>
        public void WarmTiles(IReadOnlyList<int> tileCounts)
        {
            ThrowIfDisposed();
            m_tiles.Warm(tileCounts);
        }

        /// <summary>
        /// True when Create can check out one more root: the pool was prepared and has a free warmed root. A caller
        /// that spawns during Playing (a Rocket part, B-19) asks first instead of catching Create's exhaustion
        /// exception. Allocates nothing.
        /// </summary>
        public bool CanCreate()
        {
            return !m_disposed && m_pool != null && ActiveCount < m_prepared.Length;
        }

        /// <summary>Checks out a root showing data.Cells in data.Color at data's pose; the cells are required.</summary>
        public BlockPiece Create(BlockPieceSpawnData data)
        {
            ThrowIfDisposed();
            if (data.Cells == null || data.Cells.Count == 0)
                throw new ArgumentException("A block spawns from its own cells.", nameof(data));
            if (m_pool == null) throw new InvalidOperationException("Prepare the factory before creating a piece.");
            // Onity maxSize caps retained objects, not active growth. Enforce a hard bound here.
            if (ActiveCount == m_prepared.Length) throw new InvalidOperationException("The prepared piece pool is exhausted.");
            var piece = m_rootFactory.Create();
            var slot = m_slots[piece];
            try
            {
                piece.transform.SetParent(m_parent, false);
                piece.transform.SetLocalPositionAndRotation(data.LocalPosition, data.LocalRotation);
                piece.SetColor(m_art.GetAtlasColor(data.Color));
                piece.SetCells(data.Cells);
                slot.InUse = true;
                ActiveCount++;
                return piece;
            }
            catch
            {
                m_pool.Release(piece);
                throw;
            }
        }

        public void Return(BlockPiece piece)
        {
            ThrowIfDisposed();
            if (piece == null || !m_slots.TryGetValue(piece, out var slot) || !slot.InUse)
                throw new ArgumentException("Return only a checked-out piece from this factory.", nameof(piece));
            Return(slot);
        }

        private void Return(PieceSlot slot)
        {
            slot.Piece.transform.SetParent(m_parent, false);
            m_pool.Release(slot.Piece);
            slot.InUse = false;
            ActiveCount--;
        }

        public void Dispose()
        {
            if (m_disposed) return;
            for (int i = 0; i < m_prepared.Length; i++)
            {
                var slot = m_prepared[i];
                if (slot.InUse) Return(slot);
                // Release nested state now; Unity may defer destroying the outer root.
                slot.Piece.Release();
            }
            m_pool?.Dispose();
            m_pool = null;
            m_rootFactory = null;
            m_prepared = Array.Empty<PieceSlot>();
            m_slots.Clear();
            m_tiles.Dispose();
            m_disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (m_disposed) throw new ObjectDisposedException(nameof(BlockPieceFactory));
        }
    }
}
