using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Runtime.Layout;

namespace ColorBlockJam.Gameplay.Levels
{
    /// <summary>
    /// One converted level: the domain model plus, per block index, the visual spec and authored ID.
    /// It is immutable, so Restart reuses it to rebuild the authored board (AC-5.3).
    /// </summary>
    public sealed class AttemptLevel
    {
        private readonly BlockVisualSpec[] m_visuals;
        private readonly string[] m_blockIds;

        public AttemptLevel(LevelModel model, IBoardLayout layout, string levelId, BlockVisualSpec[] visuals, string[] blockIds)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            if (visuals == null || blockIds == null
                || visuals.Length != model.BlockCount || blockIds.Length != model.BlockCount)
            {
                throw new ArgumentException("Every block needs one visual spec and one ID.", nameof(visuals));
            }

            LevelId = levelId;
            m_visuals = (BlockVisualSpec[])visuals.Clone();
            m_blockIds = (string[])blockIds.Clone();
        }

        public LevelModel Model { get; }

        public IBoardLayout Layout { get; }

        public string LevelId { get; }

        public int BlockCount => m_visuals.Length;

        public BlockVisualSpec GetVisual(int block) => m_visuals[block];

        public string GetBlockId(int block) => m_blockIds[block];
    }
}
