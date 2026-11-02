namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>
    /// Receives the four board events in the command phase while Playing (XP-5, RULE-30). Events
    /// arrive by `in` reference, so dispatch neither boxes nor allocates.
    /// </summary>
    public interface IBoardEventListener
    {
        void OnBlockExited(in BlockExitedEvent e);

        void OnBlockRemoved(in BlockRemovedEvent e);

        void OnMoveCommitted(in MoveCommittedEvent e);

        void OnFootprintChanged(in FootprintChangedEvent e);
    }
}
