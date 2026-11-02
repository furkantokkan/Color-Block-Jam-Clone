using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using Onity.Pooling;
using UnityEngine;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// The board's one set of original-kit tiles (DEC-184): one Onity pool per piece kind under one "Board Tiles"
    /// object, shared by every block root, so a root holds only the tiles it shows. Warm fills it on Home with every
    /// block's own tiles plus a small spare; when a Rocket part needs more, Get instantiates one and it stays in the
    /// pool. Owner: the BlockPieceFactory, or a standalone ModularBlockView. Stop point: Dispose destroys the waiting
    /// tiles and the pool object; a checked-out tile belongs to its root until returned or destroyed with it.
    /// </summary>
    public sealed class BoardTilePool : IDisposable
    {
        // Retained tiles are never destroyed while the pool lives: a tile that grew the pool stays in it.
        private const int k_maxRetained = int.MaxValue;
        private static readonly string[] s_tileNames = { "Center", "Edge", "OuterCorner", "InnerCorner" };

        private readonly BoardPieceCatalog m_catalog;
        private readonly Transform m_parent;
        private readonly string m_name;
        // Created with the first tile, so a pool that never shows one leaves no object behind.
        private Transform m_root;
        private readonly PrefabComponentPool<BoardPieceView>[] m_pools =
            new PrefabComponentPool<BoardPieceView>[BlockPieceCapacity.KindCount];
        private readonly int[] m_created = new int[BlockPieceCapacity.KindCount];
        private readonly int[] m_waiting = new int[BlockPieceCapacity.KindCount];
        private readonly List<BoardPieceView> m_warm = new List<BoardPieceView>();
        private bool m_disposed;

        /// <summary>A pool whose waiting tiles sit under a child of parent named name, created with the first tile.</summary>
        public BoardTilePool(BoardPieceCatalog catalog, Transform parent, string name = "Board Tiles")
        {
            m_catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            m_parent = parent != null ? parent : throw new ArgumentNullException(nameof(parent));
            m_name = name;
        }

        /// <summary>The object the waiting tiles are parented to; null until the first tile exists.</summary>
        public Transform Root => m_root;

        /// <summary>Tiles of this kind created so far, checked out or waiting.</summary>
        public int GetCreatedCount(BlockPieceKind kind) => m_created[(int)kind];

        /// <summary>Tiles of this kind waiting in the pool.</summary>
        public int GetWaitingCount(BlockPieceKind kind) => m_waiting[(int)kind];

        /// <summary>
        /// Outside Playing: makes at least counts[kind] tiles of each kind exist. Each new tile caches its property
        /// block and Transform now, because a Rocket part may show it first during Playing (AC-16).
        /// </summary>
        public void Warm(IReadOnlyList<int> counts)
        {
            ThrowIfDisposed();
            if (counts == null || counts.Count != BlockPieceCapacity.KindCount)
                throw new ArgumentException("Pass one tile count per piece kind.", nameof(counts));
            for (int kind = 0; kind < BlockPieceCapacity.KindCount; kind++)
            {
                int missing = counts[kind] - m_created[kind];
                if (missing <= 0) continue;
                // Rent every waiting tile and the missing ones before returning any; a Get/Release loop would reuse
                // one tile.
                int rent = m_waiting[kind] + missing;
                m_warm.Clear();
                for (int index = 0; index < rent; index++) m_warm.Add(Rent(kind));
                for (int index = 0; index < m_warm.Count; index++) Release(kind, m_warm[index]);
                m_warm.Clear();
            }
        }

        /// <summary>
        /// Checks out one tile of kind, parented to owner. A waiting tile is reused; an empty pool instantiates a
        /// new one, which joins the pool on its return (DEC-184).
        /// </summary>
        public BoardPieceView Get(BlockPieceKind kind, Transform owner)
        {
            ThrowIfDisposed();
            BoardPieceView tile = Rent((int)kind);
            tile.transform.SetParent(owner, false);
            return tile;
        }

        /// <summary>Returns a checked-out tile to the pool object; a tile a scene unload destroyed is dropped.</summary>
        public void Return(BlockPieceKind kind, BoardPieceView tile)
        {
            int index = (int)kind;
            if (m_disposed || tile == null)
            {
                Forget(kind);
                return;
            }
            tile.transform.SetParent(m_root, false);
            Release(index, tile);
        }

        /// <summary>
        /// Drops a checked-out tile that is being destroyed with its root, so it never re-enters the pool.
        /// </summary>
        public void Forget(BlockPieceKind kind)
        {
            int index = (int)kind;
            if (m_created[index] > 0) m_created[index]--;
        }

        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;
            for (int kind = 0; kind < m_pools.Length; kind++)
            {
                m_pools[kind]?.Dispose();
                m_pools[kind] = null;
                m_waiting[kind] = 0;
            }
            if (m_root != null) UnityObjects.Destroy(m_root.gameObject);
        }

        private BoardPieceView Rent(int kind)
        {
            // Only released tiles wait in the Onity pool, so an empty count means this Get instantiates.
            bool fresh = m_waiting[kind] == 0;
            BoardPieceView tile = GetPool(kind).Get();
            if (!fresh)
            {
                m_waiting[kind]--;
                return tile;
            }
            m_created[kind]++;
            tile.name = s_tileNames[kind];
            tile.CreateCache();
            tile.transform.localScale = Vector3.one;
            return tile;
        }

        private void Release(int kind, BoardPieceView tile)
        {
            m_pools[kind].Release(tile);
            m_waiting[kind]++;
        }

        private PrefabComponentPool<BoardPieceView> GetPool(int kind)
        {
            if (m_pools[kind] != null) return m_pools[kind];
            if (m_root == null)
            {
                var root = new GameObject(m_name) { hideFlags = m_parent.gameObject.hideFlags };
                m_root = root.transform;
                m_root.SetParent(m_parent, false);
            }
            BlockPieceKind pieceKind = (BlockPieceKind)kind;
            return m_pools[kind] = new PrefabComponentPool<BoardPieceView>(
                m_catalog.Get(pieceKind), m_root, 0, k_maxRetained, "Board tiles " + pieceKind);
        }

        private void ThrowIfDisposed()
        {
            if (m_disposed) throw new ObjectDisposedException(nameof(BoardTilePool));
        }
    }
}
