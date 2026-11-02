using System.Collections.Generic;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>Records every board event it hears and, when given a shared list, the order listeners heard them.</summary>
    public sealed class RecordingListener : IBoardEventListener
    {
        private readonly string m_name;
        private readonly List<string> m_order;

        public RecordingListener(string name = null, List<string> order = null)
        {
            m_name = name;
            m_order = order;
        }

        public List<BlockExitedEvent> Exited { get; } = new List<BlockExitedEvent>();

        public List<BlockRemovedEvent> Removed { get; } = new List<BlockRemovedEvent>();

        public List<MoveCommittedEvent> Moves { get; } = new List<MoveCommittedEvent>();

        public List<FootprintChangedEvent> Footprints { get; } = new List<FootprintChangedEvent>();

        public int Total => Exited.Count + Removed.Count + Moves.Count + Footprints.Count;

        public void OnBlockExited(in BlockExitedEvent e)
        {
            Exited.Add(e);
            m_order?.Add(m_name + ":Exited");
        }

        public void OnBlockRemoved(in BlockRemovedEvent e)
        {
            Removed.Add(e);
            m_order?.Add(m_name + ":Removed");
        }

        public void OnMoveCommitted(in MoveCommittedEvent e)
        {
            Moves.Add(e);
            m_order?.Add(m_name + ":MoveCommitted");
        }

        public void OnFootprintChanged(in FootprintChangedEvent e)
        {
            Footprints.Add(e);
            m_order?.Add(m_name + ":FootprintChanged");
        }
    }
}
