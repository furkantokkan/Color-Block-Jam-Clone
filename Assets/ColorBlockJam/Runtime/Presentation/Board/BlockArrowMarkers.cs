using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using Onity.Pooling;
using UnityEngine;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// Shows Arrow block markers (catalog BLK-01): one Onity pool per Arrow_N variant, and one marker per attached
    /// block root, parented to it at the footprint's bounding-box centre (ArrowMarkerRun), turned for its axis and
    /// tinted through BoardPieceView.SetColor on the shared material. It has no Update, and Attach never instantiates:
    /// an exhausted pool rejects. Owners: ArrowEffectView in the Game scene (stop points: Detach and ReturnAll return
    /// the markers before their roots, then Dispose or the next level's TryPrepare disposes this) and
    /// ArrowPreviewEffect in the LevelEditor preview (stop points: ReturnAll from the host's ReturnBlocks before each
    /// rebuild, and Dispose from the host's ClearScene, which returns every marker before releasing the pools).
    /// </summary>
    public sealed class BlockArrowMarkers : IDisposable
    {
        private sealed class Slot
        {
            public BoardPieceView Marker;
            public int Variant;
            public bool InUse;
        }

        // Board level y runs along local Z (BoardLayout.CellToLocal) and the calibrated prefabs are long along their
        // local Z, so a Vertical marker keeps the prefab's turn and a Horizontal one turns Y90 (PROJECT.md).
        private static readonly Quaternion s_horizontalRotation = Quaternion.Euler(0f, 90f, 0f);

        private readonly BoardPieceCatalog m_catalog;
        private readonly Transform m_storage;
        // Per variant (index = Arrow_N - 1): its pool, its warmed markers, how many are attached and how many were
        // destroyed while attached. A destroyed marker never returns to its pool, so its slot stays used until Prepare
        // replaces the variant's pool; otherwise the pool's Get would instantiate a clone this owner does not track.
        private readonly PrefabComponentPool<BoardPieceView>[] m_pools =
            new PrefabComponentPool<BoardPieceView>[ArrowMarkerRun.MaxVariant];
        private readonly Slot[][] m_slots = new Slot[ArrowMarkerRun.MaxVariant][];
        private readonly int[] m_attached = new int[ArrowMarkerRun.MaxVariant];
        private readonly int[] m_lost = new int[ArrowMarkerRun.MaxVariant];
        private readonly Dictionary<BoardPieceView, Slot> m_lookup = new Dictionary<BoardPieceView, Slot>();
        private bool m_disposed;

        /// <summary>The catalog supplies the Arrow_N prefabs; returned markers wait, inactive, under storage.</summary>
        public BlockArrowMarkers(BoardPieceCatalog catalog, Transform storage)
        {
            m_catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            m_storage = storage != null ? storage : throw new ArgumentNullException(nameof(storage));
            for (int variant = 0; variant < m_slots.Length; variant++)
            {
                m_slots[variant] = Array.Empty<Slot>();
            }
        }

        /// <summary>Markers warmed across all variants.</summary>
        public int PreparedCount => m_lookup.Count;

        /// <summary>Markers attached to block roots.</summary>
        public int AttachedCount { get; private set; }

        /// <summary>Markers warmed for Arrow_N (variant 1 to 3).</summary>
        public int GetPreparedCount(int variant) => m_slots[variant - 1].Length;

        /// <summary>
        /// Warms each variant's pool to hold at least counts[N - 1] Arrow_N markers, outside Playing. A pool that is
        /// too small, or that lost a marker destroyed while attached, is replaced by one of the larger size, so pools
        /// only grow and Attach never instantiates. Call it with no marker attached. Every prefab a replaced pool needs
        /// is checked first, so a missing one throws and changes nothing.
        /// </summary>
        public void Prepare(IReadOnlyList<int> counts)
        {
            ThrowIfDisposed();
            if (counts == null || counts.Count != ArrowMarkerRun.MaxVariant)
                throw new ArgumentException("Pass one marker count per Arrow variant.", nameof(counts));
            if (AttachedCount > 0) throw new InvalidOperationException("Return the attached markers before preparing.");
            for (int variant = 0; variant < counts.Count; variant++)
            {
                if (counts[variant] < 0) throw new ArgumentOutOfRangeException(nameof(counts));
                if (NeedsNewPool(variant, counts[variant]) && !m_catalog.TryGetArrow(variant + 1, out _))
                    throw new InvalidOperationException(
                        "The board piece catalog has no Arrow_" + (variant + 1) + " marker prefab in its environment list.");
            }

            for (int variant = 0; variant < counts.Count; variant++)
            {
                if (NeedsNewPool(variant, counts[variant]))
                {
                    PrepareVariant(variant, Math.Max(counts[variant], m_slots[variant].Length));
                }
            }
        }

        /// <summary>
        /// Takes a warmed marker for this footprint and axis and parents it to the block root: at the bounding-box
        /// centre of cells (relative to the root, one BoardLayout.CellSize per cell), at the root's height, turned for
        /// the axis, unscaled and tinted with color. Returns the marker for Return and SetClipPlane.
        /// </summary>
        public BoardPieceView Attach(Transform blockRoot, IReadOnlyList<Vector2Int> cells, LevelEffectAxis axis, AtlasColor color)
        {
            ThrowIfDisposed();
            if (blockRoot == null) throw new ArgumentNullException(nameof(blockRoot));
            ArrowMarker marker = ArrowMarkerRun.Create(cells, axis);
            if (!marker.HasMarker)
                throw new ArgumentException("Pass the block's cells and a Horizontal or Vertical axis.", nameof(cells));
            if (!HasFreeMarker(marker.Variant - 1))
                throw new InvalidOperationException("The prepared Arrow_" + marker.Variant + " marker pool is exhausted.");
            return AttachRun(blockRoot, marker, color);
        }

        /// <summary>
        /// Attach for a footprint that changes during Playing (a Rocket part, B-19): false, with no marker and no
        /// exception, when the variant these cells pick has no free warmed marker or the cells or axis give none.
        /// </summary>
        public bool TryAttach(Transform blockRoot, IReadOnlyList<Vector2Int> cells, LevelEffectAxis axis, AtlasColor color,
            out BoardPieceView marker)
        {
            marker = null;
            if (m_disposed || blockRoot == null)
            {
                return false;
            }

            ArrowMarker run = ArrowMarkerRun.Create(cells, axis);
            if (!run.HasMarker || !HasFreeMarker(run.Variant - 1))
            {
                return false;
            }

            marker = AttachRun(blockRoot, run, color);
            return true;
        }

        /// <summary>Clips an attached marker with its block (exit and effect clipping); allocates nothing.</summary>
        public void SetClipPlane(BoardPieceView marker, Vector4 plane, bool enabled)
        {
            GetAttached(marker).Marker.SetClipPlane(plane, enabled);
        }

        /// <summary>
        /// Moves an attached marker back under storage and returns it to its pool; the pool release clears its clip
        /// plane. A marker that was already destroyed (a scene unload, or a deletion in the Hierarchy) is detached
        /// without error, but its slot stays used: Attach rejects it as exhausted until Prepare replaces that pool.
        /// </summary>
        public void Return(BoardPieceView marker)
        {
            Return(GetAttached(marker));
        }

        /// <summary>Returns every attached marker.</summary>
        public void ReturnAll()
        {
            for (int variant = 0; variant < m_slots.Length; variant++)
            {
                Slot[] slots = m_slots[variant];
                for (int index = 0; index < slots.Length; index++)
                {
                    if (slots[index].InUse)
                    {
                        Return(slots[index]);
                    }
                }
            }
        }

        /// <summary>Returns every marker and disposes every pool, which destroys the warmed markers.</summary>
        public void Dispose()
        {
            if (m_disposed) return;
            try
            {
                ReturnAll();
            }
            finally
            {
                for (int variant = 0; variant < m_pools.Length; variant++)
                {
                    DisposeVariant(variant);
                }

                m_disposed = true;
            }
        }

        // Onity maxSize caps retained objects, not active growth; the warmed count less the markers destroyed while
        // attached is the hard bound.
        private bool HasFreeMarker(int variant)
        {
            return m_attached[variant] + m_lost[variant] < m_slots[variant].Length;
        }

        // Takes a warmed marker of the run's variant, which the caller checked is free, and parents it to the block root.
        private BoardPieceView AttachRun(Transform blockRoot, in ArrowMarker run, AtlasColor color)
        {
            int variant = run.Variant - 1;
            BoardPieceView view = m_pools[variant].Get();
            Slot slot = m_lookup[view];
            Transform transform = view.transform;
            transform.SetParent(blockRoot, false);
            transform.SetLocalPositionAndRotation(
                new Vector3(run.Center.x * BoardLayout.CellSize, 0f, run.Center.y * BoardLayout.CellSize),
                run.Axis == LevelEffectAxis.Horizontal ? s_horizontalRotation : Quaternion.identity);
            transform.localScale = Vector3.one;
            view.SetColor(color);
            slot.InUse = true;
            m_attached[variant]++;
            AttachedCount++;
            return view;
        }

        private void PrepareVariant(int variant, int count)
        {
            DisposeVariant(variant);
            m_catalog.TryGetArrow(variant + 1, out BoardPieceView prefab);
            var pool = new PrefabComponentPool<BoardPieceView>(prefab, m_storage, count, count,
                "Arrow markers Arrow_" + (variant + 1));
            var slots = new Slot[count];
            // Rent all before returning; a Get/Release loop warms only one instance. Each marker caches its property
            // block now, so a Playing clip write allocates nothing, and its Transform is written now, because the first
            // managed access to a component creates its C# wrapper and a Rocket part may attach the marker first during
            // Playing (AC-16).
            for (int index = 0; index < count; index++)
            {
                BoardPieceView marker = pool.Get();
                marker.CreateCache();
                marker.transform.localScale = Vector3.one;
                slots[index] = new Slot { Marker = marker, Variant = variant };
            }

            for (int index = 0; index < count; index++)
            {
                pool.Release(slots[index].Marker);
                m_lookup.Add(slots[index].Marker, slots[index]);
            }

            m_pools[variant] = pool;
            m_slots[variant] = slots;
        }

        private void DisposeVariant(int variant)
        {
            Slot[] slots = m_slots[variant];
            for (int index = 0; index < slots.Length; index++)
            {
                m_lookup.Remove(slots[index].Marker);
            }

            m_slots[variant] = Array.Empty<Slot>();
            m_attached[variant] = 0;
            m_lost[variant] = 0;
            m_pools[variant]?.Dispose();
            m_pools[variant] = null;
        }

        // A variant needs a new pool when the count outgrows it or when it lost a marker.
        private bool NeedsNewPool(int variant, int count)
        {
            return count > m_slots[variant].Length || m_lost[variant] > 0;
        }

        private void Return(Slot slot)
        {
            BoardPieceView marker = slot.Marker;
            slot.InUse = false;
            m_attached[slot.Variant]--;
            AttachedCount--;
            if (marker != null)
            {
                marker.transform.SetParent(m_storage, false);
                m_pools[slot.Variant].Release(marker);
            }
            else
            {
                // A scene unload destroys a marker with its block root, and a Hierarchy deletion destroys it alone.
                // It cannot go back to its pool, whose Dispose skips it, so its slot stays used until Prepare.
                m_lost[slot.Variant]++;
            }
        }

        private Slot GetAttached(BoardPieceView marker)
        {
            ThrowIfDisposed();
            if (ReferenceEquals(marker, null) || !m_lookup.TryGetValue(marker, out Slot slot) || !slot.InUse)
                throw new ArgumentException("Pass a marker attached by this owner.", nameof(marker));
            return slot;
        }

        private void ThrowIfDisposed()
        {
            if (m_disposed) throw new ObjectDisposedException(nameof(BlockArrowMarkers));
        }
    }
}
