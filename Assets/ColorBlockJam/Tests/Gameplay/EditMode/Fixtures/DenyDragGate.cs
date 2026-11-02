using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>A drag gate that refuses one block, like a frozen Ice block will.</summary>
    public sealed class DenyDragGate : IDragGate
    {
        private readonly int m_block;

        public DenyDragGate(int block)
        {
            m_block = block;
        }

        public bool AllowsDrag(IBoardReader board, int block) => block != m_block;
    }
}
