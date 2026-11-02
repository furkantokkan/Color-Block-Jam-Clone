using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Commands
{
    /// <summary>
    /// A driver of an attempt other than the pointer, such as a replay, a bot or a tutorial. The
    /// session asks it for its next command in the command phase, executes that command like a
    /// pointer move and reports the outcome back, so the source can advance, retry or stop.
    /// </summary>
    public interface ICommandSource
    {
        /// <summary>Offers the next command; false when the source has nothing to submit this tick.</summary>
        bool TryGetNext(IBoardReader board, out GameCommand command);

        /// <summary>Receives the outcome of the command the last TryGetNext offered.</summary>
        void OnExecuted(in GameCommand command, CommandOutcome outcome);
    }
}
