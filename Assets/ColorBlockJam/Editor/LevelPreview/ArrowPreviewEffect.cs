using System;
using System.Collections.Generic;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.LevelPreview
{
    /// <summary>
    /// Puts each Arrow block's centred marker (catalog BLK-01) on its preview block root through the shared
    /// BlockArrowMarkers, the same Presentation owner the Game scene's ArrowEffectView wraps, so no second Arrow
    /// renderer exists. It rejects a level whose board piece catalog lacks a needed Arrow_N prefab before the host
    /// changes the scene.
    /// </summary>
    internal sealed class ArrowPreviewEffect : ILevelPreviewEffect, ILevelPreviewEffectCheck
    {
        // Markers per Arrow_N the last prepared level needs.
        private readonly int[] m_counts = new int[ArrowMarkerRun.MaxVariant];
        // The shown markers, in block order.
        private readonly List<BoardPieceView> m_active = new List<BoardPieceView>();
        private IReadOnlyList<LevelPreviewBlock> m_blocks = Array.Empty<LevelPreviewBlock>();
        private ArrowMarker[] m_markers = Array.Empty<ArrowMarker>();
        private AtlasColor m_color;
        // Owner of the marker pools: created by the first Prepare with the host's Blocks node as storage, grown by later
        // ones, and disposed in Dispose, which the host calls before it disposes its block factory, because attached
        // markers ride on the block roots.
        private BlockArrowMarkers m_owner;

        /// <summary>The shown Arrow markers, in block order.</summary>
        public IReadOnlyList<BoardPieceView> ActiveMarkers => m_active;

        /// <summary>Markers the marker pools have warmed; 0 before the first Prepare.</summary>
        public int PreparedMarkerCount => m_owner != null ? m_owner.PreparedCount : 0;

        /// <summary>
        /// The marker of the block at this index: its Arrow effect over its mapped cells, which are relative to its
        /// root, so the marker sits in the root's cells; none without a valid Arrow.
        /// </summary>
        public static ArrowMarker GetMarker(LevelData level, IReadOnlyList<LevelPreviewBlock> blocks, int block)
        {
            return ArrowMarkerRun.Create(level.GetBlockAt(block), blocks[block].Cells);
        }

        public bool TryCheck(LevelData level, IReadOnlyList<LevelPreviewBlock> blocks, BoardArt art, out string error)
        {
            for (int block = 0; block < blocks.Count; block++)
            {
                ArrowMarker marker = GetMarker(level, blocks, block);
                if (marker.HasMarker && !art.Catalog.TryGetArrow(marker.Variant, out _))
                {
                    error = $"Block {blocks[block].BlockId} needs the Arrow_{marker.Variant} marker prefab, which the " +
                            "board piece catalog lacks.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        // A variant whose need grows, or whose marker was deleted, gets a new pool; a level whose needs the pools
        // already cover (a reselect) instantiates nothing.
        public void Prepare(LevelData level, IReadOnlyList<LevelPreviewBlock> blocks, BoardArt art, Transform storage,
            Camera camera)
        {
            if (!TryCheck(level, blocks, art, out string error))
            {
                throw new InvalidOperationException(error);
            }

            if (m_markers.Length != blocks.Count)
            {
                m_markers = new ArrowMarker[blocks.Count];
            }

            Array.Clear(m_counts, 0, m_counts.Length);
            for (int block = 0; block < blocks.Count; block++)
            {
                m_markers[block] = GetMarker(level, blocks, block);
                if (m_markers[block].HasMarker)
                {
                    m_counts[m_markers[block].Variant - 1]++;
                }
            }

            m_blocks = blocks;
            m_color = art.ArrowColor;
            m_owner ??= new BlockArrowMarkers(art.Catalog, storage);
            m_owner.Prepare(m_counts);
        }

        public void Attach(int block, Transform root)
        {
            if (m_markers[block].HasMarker)
            {
                m_active.Add(m_owner.Attach(root, m_blocks[block].Cells, m_markers[block].Axis, m_color));
            }
        }

        // The markers face no camera and carry no screen-space label.
        public void Reproject()
        {
        }

        // Markers ride on the block roots, so the host returns them before the roots.
        public void ReturnAll()
        {
            m_owner?.ReturnAll();
            m_active.Clear();
        }

        public void Dispose()
        {
            m_active.Clear();
            m_blocks = Array.Empty<LevelPreviewBlock>();
            m_markers = Array.Empty<ArrowMarker>();
            m_owner?.Dispose();
            m_owner = null;
        }
    }
}
