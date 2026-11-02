using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>Counts board events without storing them, so publishing to it allocates nothing.</summary>
    public sealed class CountingListener : IBoardEventListener
    {
        public int Count { get; private set; }

        public void OnBlockExited(in BlockExitedEvent e) => Count++;

        public void OnBlockRemoved(in BlockRemovedEvent e) => Count++;

        public void OnMoveCommitted(in MoveCommittedEvent e) => Count++;

        public void OnFootprintChanged(in FootprintChangedEvent e) => Count++;
    }
}
