using System;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// One Ice look per block ID an attempt can issue (B-10), created once outside Playing: attaching one only
    /// shows the shared Ice material on that block's own tiles (DEC-184), so a Rocket part of a frozen block takes
    /// its slot's look without creating anything. Owner: its Ice effect view. Stop point: ReturnAll and Dispose
    /// detach every look.
    /// </summary>
    public sealed class BlockIceOverlays : IDisposable
    {
        private readonly BlockIceOverlay[] m_slots;
        private bool m_disposed;

        /// <summary>capacity slots (LevelModel.TotalCellCount in play; the block count in a static preview).</summary>
        public BlockIceOverlays(UnityEngine.Material material, int capacity)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            m_slots = new BlockIceOverlay[capacity];
            for (int block = 0; block < capacity; block++) m_slots[block] = new BlockIceOverlay(material);
        }

        public int Capacity => m_slots.Length;
        public int ActiveCount { get; private set; }

        /// <summary>The attached look of block, or null.</summary>
        public BlockIceOverlay GetAt(int block) => m_slots[block].IsAttached ? m_slots[block] : null;

        /// <summary>
        /// Shows ice over view's tiles for block; a thawed count attaches nothing. Throws for a slot out of range or
        /// one that already shows ice.
        /// </summary>
        public void Attach(int block, ModularBlockView view, int remaining, int initial)
        {
            if (m_disposed) throw new ObjectDisposedException(nameof(BlockIceOverlays));
            if (remaining <= 0) return;
            if (block < 0 || block >= m_slots.Length || m_slots[block].IsAttached)
                throw new InvalidOperationException("The Ice slot does not exist or already shows a block.");
            m_slots[block].Attach(view, remaining, initial);
            ActiveCount++;
        }

        public void SetCount(int block, int remaining, int initial)
        {
            BlockIceOverlay look = GetAt(block);
            if (look == null) return;
            look.SetCount(remaining, initial);
            if (remaining == 0) look.Thaw();
        }

        public void Sync()
        {
            for (int block = 0; block < m_slots.Length; block++)
            {
                BlockIceOverlay look = m_slots[block];
                if (!look.IsAttached) continue;
                look.Sync();
                if (look.ThawFinished) Detach(block);
            }
        }

        public void Shake(int block) { GetAt(block)?.Shake(); }

        /// <summary>A Rocket split keeps the block root on its cell, so an unfinished punch of that root stops first.</summary>
        public void StopFeedback(int block) { GetAt(block)?.StopFeedback(); }

        public void SetPaused(bool paused)
        {
            for (int block = 0; block < m_slots.Length; block++) m_slots[block].SetPaused(paused);
        }

        public void StopTweens()
        {
            for (int block = 0; block < m_slots.Length; block++) m_slots[block].StopTweens();
        }

        public void Detach(int block)
        {
            if (block < 0 || block >= m_slots.Length || !m_slots[block].IsAttached) return;
            m_slots[block].Detach();
            ActiveCount--;
        }

        public void ReturnAll()
        {
            for (int block = 0; block < m_slots.Length; block++) Detach(block);
        }

        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;
            ReturnAll();
        }
    }
}
