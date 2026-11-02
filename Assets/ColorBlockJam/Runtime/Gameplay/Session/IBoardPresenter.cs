using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Levels;

namespace ColorBlockJam.Gameplay.Session
{
    /// <summary>
    /// The presentation port the level session drives: spawn and place block roots, sync them to the
    /// board each Playing tick, pause, stop and return. Views render what the domain decided and never
    /// decide domain state themselves.
    /// </summary>
    public interface IBoardPresenter
    {
        /// <summary>How long the final exit motion needs after Won; the session caps it at 0.8 s (DEC-93).</summary>
        float FinishSeconds { get; }

        /// <summary>
        /// Prepares pooled block roots for a level on Home; building the same level again reuses them.
        /// When the block root prefab cannot be pooled it returns false with an error and changes nothing.
        /// </summary>
        bool TryBuild(AttemptLevel level, out string error);

        /// <summary>Checks out one root per on-board block and puts it on its cell.</summary>
        void Place(IBoardReader board);

        /// <summary>The view sync phase: plays this tick's board events and follows the held block.</summary>
        void Sync(BoardSimulation simulation, BoardPoint pointer);

        /// <summary>Marks the held block, or -1 when the press ends; the release settles the block on its cell.</summary>
        void SetHeld(int block);

        /// <summary>Puts the held block straight onto its cell without a tween and clears the held mark.</summary>
        void SnapHeld();

        void SetTweensPaused(bool paused);

        void StopTweens();

        /// <summary>Returns every checked-out block root to its pool.</summary>
        void ReturnPieces();
    }
}
