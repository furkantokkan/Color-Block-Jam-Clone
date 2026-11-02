using System;
using System.Collections.Generic;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>
    /// Reads one attempt's Ice state in the existing board view phase; never changes its rules. Its Ice looks and
    /// labels have a slot per block ID the attempt can issue, so a Rocket part of a frozen block shows the counter it
    /// inherited and a split parent's ice and label follow its new footprint (B-10). The ice is the shared Ice
    /// material on the block's own tiles (DEC-184), so the block view keeps it through a recompose.
    /// </summary>
    public sealed class IceEffectView : IBlockEffectView, IBlockedDragFeedback, IBlockFootprintView
    {
        private readonly BlockEffectViewContext m_viewContext;
        private readonly Material m_material;
        private BlockEffectContext m_context;
        private IceEffectState m_state;
        private BlockIceOverlays m_overlays;
        private IceCounterLabels m_labels;
        // The authored footprints, which an authored block's shell and label return to on every Attach.
        private IReadOnlyList<Vector2Int>[] m_footprints = Array.Empty<IReadOnlyList<Vector2Int>>();
        private int[] m_shownCounts = Array.Empty<int>();
        private bool[] m_attached = Array.Empty<bool>();
        private int m_generation;
        private bool m_disposed;

        public IceEffectView(BlockEffectViewContext context, Material material)
        {
            m_viewContext = context ?? throw new ArgumentNullException(nameof(context));
            m_material = material;
        }

        public string TypeId => LevelEffectTypes.Ice;
        public BlockIceOverlays Overlays => m_overlays;
        public IceCounterLabels Labels => m_labels;

        public bool TryPrepare(BlockEffectContext context, IBlockEffectLevel metadata, out string error)
        {
            if (m_disposed) throw new ObjectDisposedException(nameof(IceEffectView));
            if (!(metadata is IceEffectLevel ice) || context == null)
            {
                error = "Ice view requires its typed level metadata.";
                return false;
            }
            if (ReferenceEquals(m_context, context))
            {
                error = null;
                return true;
            }
            int capacity = context.Level.Model.TotalCellCount;
            if (ice.Capacity != capacity)
            {
                error = "Ice metadata must hold one slot per block ID of its level.";
                return false;
            }
            bool hasIce = false;
            for (int block = 0; block < ice.BlockCount; block++) hasIce |= ice.GetCount(block) > 0;
            var footprints = new IReadOnlyList<Vector2Int>[ice.BlockCount];
            for (int block = 0; block < footprints.Length; block++) footprints[block] = context.Level.GetVisual(block).Cells;
            BlockIceOverlays overlays = null;
            IceCounterLabels labels = null;
            try
            {
                if (hasIce)
                {
                    if (m_material == null || !m_material.HasProperty("_IceOpacity")
                        || !m_material.HasProperty("_IceCracks") || !m_material.HasProperty("_IceTint"))
                        throw new InvalidOperationException("The Game scene needs the shared Ice Shader Graph material.");
                    if (m_viewContext.Camera == null || m_viewContext.Document == null
                        || m_viewContext.Document.rootVisualElement == null)
                        throw new InvalidOperationException("The Game scene needs its Ice counter document and camera.");
                    overlays = new BlockIceOverlays(m_material, capacity);
                    labels = new IceCounterLabels(m_viewContext.Camera,
                        m_viewContext.Document.rootVisualElement, footprints, capacity);
                }
            }
            catch (Exception exception)
            {
                labels?.Dispose();
                overlays?.Dispose();
                error = exception.Message;
                return false;
            }
            m_labels?.Dispose();
            m_overlays?.Dispose();
            m_overlays = overlays;
            m_labels = labels;
            m_context = context;
            m_footprints = footprints;
            m_shownCounts = new int[capacity];
            m_attached = new bool[capacity];
            m_state = null;
            error = null;
            return true;
        }

        public void BindAttempt(BlockEffectAttempt attempt, IBlockEffectState state)
        {
            if (attempt == null || !ReferenceEquals(attempt.Context, m_context)
                || attempt.Generation <= m_generation || !(state is IceEffectState ice))
                throw new InvalidOperationException("Bind Ice presentation to the newly published attempt for its prepared level.");
            ReturnAll();
            m_state = ice;
            m_generation = attempt.Generation;
        }

        /// <summary>An authored block: its shell and label show its authored footprint again, even after a Restart.</summary>
        public void Attach(int block, Transform root)
        {
            if (block < m_footprints.Length) AttachPart(block, block, root, m_footprints[block]);
            else AttachPart(block, -1, root, null);
        }

        /// <summary>A Rocket part: ice over its own tiles, with the count it inherited and a label on its own cells.</summary>
        public void AttachPart(int block, int source, Transform root, IReadOnlyList<Vector2Int> cells)
        {
            if (m_state == null) throw new InvalidOperationException("Bind Ice attempt state before attaching blocks.");
            int count = m_state.Rules.GetRemaining(block);
            m_attached[block] = true;
            m_shownCounts[block] = count;
            if (count == 0 || cells == null) return;
            try
            {
                // A root without a block view shows no ice; its rules still hold it frozen.
                if (root != null && root.TryGetComponent(out ModularBlockView view))
                    m_overlays.Attach(block, view, count, m_state.Rules.GetInitial(block));
                m_labels.SetFootprint(block, cells);
                m_labels.Attach(block, root, count);
            }
            catch { Detach(block); throw; }
        }

        /// <summary>
        /// A split parent keeps its label, re-anchored on its remaining cells; its block view already kept the ice
        /// on the recomposed tiles.
        /// </summary>
        public void SetFootprint(int block, IReadOnlyList<Vector2Int> cells)
        {
            if (block < 0 || block >= m_attached.Length || !m_attached[block] || m_shownCounts[block] == 0) return;
            // A frozen parent cannot settle later, so an unfinished punch stops and its root goes back on its cell.
            m_overlays?.StopFeedback(block);
            m_labels?.SetFootprint(block, cells);
        }

        public void Sync(IBoardReader board)
        {
            if (m_state == null) return;
            for (int block = 0; block < m_shownCounts.Length; block++)
            {
                if (!m_attached[block]) continue;
                if (board != null && board.GetStatus(block) != BlockStatus.OnBoard)
                {
                    // A removed block keeps its shell while its root shrinks away, but no count reaches it and its
                    // screen-space label leaves at once, as the label does not follow a moving root.
                    if (m_shownCounts[block] > 0) m_labels?.Detach(block);
                    continue;
                }
                int remaining = m_state.Rules.GetRemaining(block);
                if (remaining == m_shownCounts[block]) continue;
                m_shownCounts[block] = remaining;
                m_overlays?.SetCount(block, remaining, m_state.Rules.GetInitial(block));
                m_labels?.SetCount(block, remaining);
            }
            m_overlays?.Sync();
        }

        public void ShowBlockedDrag(int block)
        {
            if (m_state != null && block >= 0 && block < m_shownCounts.Length
                && m_state.Rules.GetRemaining(block) > 0)
                m_overlays?.Shake(block);
        }

        public void Reproject() { m_labels?.Reproject(); }
        // The Ice pass reads the tile's own clip plane, which the block view already set (DEC-184).
        public void SetClipPlane(int block, Vector4 plane, bool enabled) { }
        public void SetPaused(bool paused) { m_overlays?.SetPaused(paused); }
        public void StopTweens() { m_overlays?.StopTweens(); }

        public void Detach(int block)
        {
            if (block < 0 || block >= m_attached.Length) return;
            m_overlays?.Detach(block);
            m_labels?.Detach(block);
            m_attached[block] = false;
            m_shownCounts[block] = 0;
        }

        public void ReturnAll()
        {
            m_overlays?.ReturnAll();
            m_labels?.ReturnAll();
            Array.Clear(m_attached, 0, m_attached.Length);
            Array.Clear(m_shownCounts, 0, m_shownCounts.Length);
            m_state = null;
        }

        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;
            ReturnAll();
            m_labels?.Dispose();
            m_overlays?.Dispose();
            m_labels = null;
            m_overlays = null;
        }
    }
}
