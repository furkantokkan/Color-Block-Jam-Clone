using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Session;

namespace ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures
{
    /// <summary>
    /// Forwards every call to a real presenter but reports a final motion longer than the 0.8 s cap, so
    /// only the session's own deadline can end the Won Finish (DEC-93).
    /// </summary>
    public sealed class SlowFinishPresenter : IBoardPresenter
    {
        private readonly IBoardPresenter m_inner;

        public SlowFinishPresenter(IBoardPresenter inner, float finishSeconds)
        {
            m_inner = inner;
            FinishSeconds = finishSeconds;
        }

        public float FinishSeconds { get; }

        public bool TryBuild(AttemptLevel level, out string error) => m_inner.TryBuild(level, out error);

        public void Place(IBoardReader board) => m_inner.Place(board);

        public void Sync(BoardSimulation simulation, BoardPoint pointer) => m_inner.Sync(simulation, pointer);

        public void SetHeld(int block) => m_inner.SetHeld(block);

        public void SnapHeld() => m_inner.SnapHeld();

        public void SetTweensPaused(bool paused) => m_inner.SetTweensPaused(paused);

        public void StopTweens() => m_inner.StopTweens();

        public void ReturnPieces() => m_inner.ReturnPieces();
    }
}
