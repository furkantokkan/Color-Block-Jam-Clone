using System;
using System.Collections.Generic;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>
    /// Shows each Arrow block's centred marker (BLK-01) through BlockArrowMarkers: it warms one pool per Arrow_N on
    /// Home for the Arrow blocks and every Rocket part they can show at once, parents a marker to each Arrow block's
    /// root over its current cells, re-attaches it when a Rocket recomposes the block, gives each part a marker along
    /// its parent's axis, clips it with its block's exit and returns it before its root. The marker is a static child
    /// of the root, so drag, settle and exit motion carry it and it needs no tick. It never reads or changes domain
    /// state. Owner: the board presenter's effect list. Stop points: Detach and ReturnAll return the markers before
    /// their roots; the next level's TryPrepare and Dispose dispose the pools.
    /// </summary>
    public sealed class ArrowEffectView : IBlockEffectView, IBlockFootprintView
    {
        private readonly BlockEffectViewContext m_viewContext;
        private BlockEffectContext m_context;
        private ArrowEffectLevel m_level;
        private BlockArrowMarkers m_markers;
        // The authored footprints, which an authored block's marker is attached over on every Attach (Place).
        private IReadOnlyList<Vector2Int>[] m_footprints = Array.Empty<IReadOnlyList<Vector2Int>>();
        // Per block ID the level can issue (LevelModel.TotalCellCount, B-1): the attached block's axis, its root and
        // its marker; a null marker means none is attached.
        private LevelEffectAxis?[] m_axes = Array.Empty<LevelEffectAxis?>();
        private Transform[] m_roots = Array.Empty<Transform>();
        private BoardPieceView[] m_arrows = Array.Empty<BoardPieceView>();
        private int m_generation;
        private bool m_disposed;

        public ArrowEffectView(BlockEffectViewContext context)
        {
            m_viewContext = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string TypeId => LevelEffectTypes.Arrow;

        /// <summary>Arrow markers the marker pools warmed for the current level; Restart keeps this number.</summary>
        public int PreparedArrowMarkerCount => m_markers != null ? m_markers.PreparedCount : 0;

        /// <summary>The Arrow marker on a block's checked-out root, or null when it shows none.</summary>
        public BoardPieceView GetArrowMarker(int block)
            => block >= 0 && block < m_arrows.Length ? m_arrows[block] : null;

        /// <summary>
        /// Warms one marker pool per Arrow_N, sized to the level's Arrow blocks and the Rocket parts each can show at once
        /// (FragmentCapacity.AddArrowMarkerCapacity over its resolved cells). The same level (Restart) keeps its pools.
        /// Every Arrow prefab is checked before any pool changes: a missing one rejects the level with one error naming
        /// the block. BoardPresenter then disposes every effect view, and no level stays built.
        /// </summary>
        public bool TryPrepare(BlockEffectContext context, IBlockEffectLevel metadata, out string error)
        {
            if (m_disposed) throw new ObjectDisposedException(nameof(ArrowEffectView));
            if (!(metadata is ArrowEffectLevel arrow) || context == null)
            {
                error = "Arrow view requires its typed level metadata.";
                return false;
            }

            if (ReferenceEquals(m_context, context))
            {
                error = null;
                return true;
            }

            BoardPieceCatalog catalog = m_viewContext.Art.Catalog;
            var footprints = new IReadOnlyList<Vector2Int>[arrow.BlockCount];
            int[] counts = new int[ArrowMarkerRun.MaxVariant];
            int[] blockCounts = new int[ArrowMarkerRun.MaxVariant];
            for (int block = 0; block < footprints.Length; block++)
            {
                IReadOnlyList<Vector2Int> cells = context.Level.GetVisual(block).Cells;
                footprints[block] = cells;
                if (!arrow.TryGetAxis(block, out LevelEffectAxis axis))
                {
                    continue;
                }

                int variant = ArrowMarkerRun.Create(cells, axis).Variant;
                if (!catalog.TryGetArrow(variant, out _))
                {
                    error = $"Block {context.Level.GetBlockId(block)} needs the Arrow_{variant} marker prefab, which the board piece catalog lacks.";
                    return false;
                }

                Array.Clear(blockCounts, 0, blockCounts.Length);
                FragmentCapacity.AddArrowMarkerCapacity(cells, axis, blockCounts);
                for (int part = 1; part <= blockCounts.Length; part++)
                {
                    if (blockCounts[part - 1] > 0 && !catalog.TryGetArrow(part, out _))
                    {
                        error = $"Block {context.Level.GetBlockId(block)} needs the Arrow_{part} marker prefab for its Rocket parts, which the board piece catalog lacks.";
                        return false;
                    }

                    counts[part - 1] += blockCounts[part - 1];
                }
            }

            // Owner: this view; markers ride on block roots and wait, inactive, under BoardRoot. Stops: Detach and
            // ReturnAll return each marker before its root; the next level's TryPrepare or Dispose disposes it.
            BlockArrowMarkers markers = null;
            if (arrow.HasArrow)
            {
                markers = new BlockArrowMarkers(catalog, m_viewContext.BoardRoot);
                try
                {
                    markers.Prepare(counts);
                }
                catch (Exception exception)
                {
                    markers.Dispose();
                    error = exception.Message;
                    return false;
                }
            }

            ReturnAll();
            m_markers?.Dispose();
            m_markers = markers;
            m_context = context;
            m_level = arrow;
            m_footprints = footprints;
            int capacity = context.Level.Model.TotalCellCount;
            m_axes = new LevelEffectAxis?[capacity];
            m_roots = new Transform[capacity];
            m_arrows = new BoardPieceView[capacity];
            error = null;
            return true;
        }

        public void BindAttempt(BlockEffectAttempt attempt, IBlockEffectState state)
        {
            if (attempt == null || !ReferenceEquals(attempt.Context, m_context)
                || attempt.Generation <= m_generation || !(state is ArrowEffectState))
                throw new InvalidOperationException("Bind Arrow presentation to the newly published attempt for its prepared level.");
            // A Rocket part of the previous attempt read its parent's axis; a new attempt issues its part IDs afresh.
            ReturnAll();
            m_generation = attempt.Generation;
        }

        /// <summary>An authored block: its marker over its authored footprint, even after a Restart.</summary>
        public void Attach(int block, Transform root)
        {
            if (block < 0 || block >= m_footprints.Length) return;
            AttachOver(block, root, m_level.TryGetAxis(block, out LevelEffectAxis axis) ? axis : (LevelEffectAxis?)null,
                m_footprints[block]);
        }

        /// <summary>A Rocket part: a marker along its authored source's axis over the part's own cells.</summary>
        public void AttachPart(int block, int source, Transform root, IReadOnlyList<Vector2Int> cells)
        {
            if (block < 0 || block >= m_axes.Length) return;
            LevelEffectAxis? axis = source >= 0 && source < m_level.BlockCount
                && m_level.TryGetAxis(source, out LevelEffectAxis sourceAxis) ? sourceAxis : (LevelEffectAxis?)null;
            AttachOver(block, root, axis, cells);
        }

        /// <summary>A block a Rocket recomposed keeps its axis; its marker is replaced by one over the remaining cells.</summary>
        public void SetFootprint(int block, IReadOnlyList<Vector2Int> cells)
        {
            if (block < 0 || block >= m_axes.Length || m_roots[block] is null) return;
            AttachOver(block, m_roots[block], m_axes[block], cells);
        }

        public void Sync(IBoardReader board) { }
        public void Reproject() { }

        /// <summary>The marker clips with its block: exit and effect clipping forward the block's plane here.</summary>
        public void SetClipPlane(int block, Vector4 plane, bool enabled)
        {
            if (block >= 0 && block < m_arrows.Length && m_arrows[block] is not null)
            {
                m_markers.SetClipPlane(m_arrows[block], plane, enabled);
            }
        }

        // The marker is a static child of its root: the root's tweens carry it, and it runs none of its own.
        public void SetPaused(bool paused) { }
        public void StopTweens() { }

        /// <summary>Returns the block's marker before the presenter returns its root, so no marker rides a pooled root.</summary>
        public void Detach(int block)
        {
            if (block < 0 || block >= m_arrows.Length) return;
            ReturnMarker(block);
            m_roots[block] = null;
            m_axes[block] = null;
        }

        public void ReturnAll()
        {
            for (int block = 0; block < m_arrows.Length; block++)
            {
                ReturnMarker(block);
            }

            Array.Clear(m_roots, 0, m_roots.Length);
            Array.Clear(m_axes, 0, m_axes.Length);
        }

        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;
            ReturnAll();
            // Disposing the markers skips any a scene unload destroyed, so it also runs after one.
            m_markers?.Dispose();
            m_markers = null;
        }

        // Puts the block's marker, if it has an axis, on its root over cells, replacing the marker a Rocket recompose
        // made stale. A part whose variant has no warmed marker left shows none.
        private void AttachOver(int block, Transform root, LevelEffectAxis? axis, IReadOnlyList<Vector2Int> cells)
        {
            ReturnMarker(block);
            m_roots[block] = root;
            m_axes[block] = axis;
            if (!axis.HasValue || m_markers == null || cells == null)
            {
                return;
            }

            if (m_markers.TryAttach(root, cells, axis.Value, m_viewContext.Art.ArrowColor, out BoardPieceView marker))
            {
                m_arrows[block] = marker;
            }
        }

        // A scene unload may destroy the roots, and the markers on them, before the container disposes this view; the
        // marker owner then only marks the marker free.
        private void ReturnMarker(int block)
        {
            BoardPieceView marker = m_arrows[block];
            m_arrows[block] = null;
            if (marker is not null && m_markers != null)
            {
                m_markers.Return(marker);
            }
        }
    }
}
