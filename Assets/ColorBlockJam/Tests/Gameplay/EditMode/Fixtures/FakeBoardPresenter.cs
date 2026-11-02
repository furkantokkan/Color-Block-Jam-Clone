using System.Collections.Generic;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Session;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>
    /// Journals every presenter call in order, records what the last Sync saw in the event log and tracks
    /// the cell each block root rests on, written where BoardPresenter writes a root. Sync only writes
    /// fields and appends a constant string, so with a presized journal it allocates nothing and also
    /// serves the zero-allocation tick test. Blocked-drag feedback is counted, not journaled, so the fixed
    /// step orders stay the same; the count proves only that the router asked for the feedback.
    /// </summary>
    public sealed class FakeBoardPresenter : IBoardPresenter, IBlockedDragFeedback
    {
        private readonly List<string> m_journal;

        // The board Place received and the cell each root rests on, without the drag lead: Place, the held
        // follow and the settles in Sync, SetHeld and SnapHeld write it from that board, as BoardPresenter does.
        private IBoardReader m_board;
        private Cell[] m_shown = new Cell[0];

        public FakeBoardPresenter(List<string> journal)
        {
            m_journal = journal;
        }

        public float FinishSeconds { get; set; }

        public int SetHeldCount { get; private set; }

        public int HeldBlock { get; private set; } = -1;

        public int SyncCount { get; private set; }

        public int BlockedDragCount { get; private set; }

        public int LastBlockedBlock { get; private set; } = -1;

        /// <summary>The number of events in the log at the last Sync.</summary>
        public int LastEventCount { get; private set; }

        public int LastExitCount { get; private set; }

        public BoardEventKind LastFirstEventKind { get; private set; }

        /// <summary>When set, TryBuild rejects every level with this error, as a level with an unusable prefab.</summary>
        public string BuildError { get; set; }

        /// <summary>The board the session asked the presenter to read at the last Place.</summary>
        public IBoardReader PlacedBoard { get; private set; }

        /// <summary>The cell the block's root rests on, as BoardPresenter would show it without the drag lead.</summary>
        public Cell GetShownOrigin(int block) => m_shown[block];

        public bool TryBuild(AttemptLevel level, out string error)
        {
            m_journal.Add("presenter.Build");
            error = BuildError;
            return error == null;
        }

        public void Place(IBoardReader board)
        {
            PlacedBoard = board;
            m_board = board;
            if (m_shown.Length != board.Model.TotalCellCount)
            {
                m_shown = new Cell[board.Model.TotalCellCount];
            }

            for (int block = 0; block < board.BlockCount; block++)
            {
                m_shown[block] = board.GetOrigin(block);
            }

            m_journal.Add("presenter.Place");
        }

        public void Sync(BoardSimulation simulation, BoardPoint pointer)
        {
            m_journal.Add("presenter.Sync");
            SyncCount++;
            BoardEventLog events = simulation.Events;
            LastEventCount = events.Count;
            LastExitCount = 0;
            for (int i = 0; i < events.Count; i++)
            {
                BoardEvent boardEvent = events.GetAt(i);
                if (boardEvent.Kind == BoardEventKind.Exited)
                {
                    LastExitCount++;
                }

                bool settles = boardEvent.Kind == BoardEventKind.MoveCommitted
                    || (boardEvent.Kind == BoardEventKind.Exited && boardEvent.Outcome == ExitOutcome.Stay);
                if (settles && boardEvent.Block != HeldBlock)
                {
                    Settle(boardEvent.Block);
                }
            }

            if (events.Count > 0)
            {
                LastFirstEventKind = events.GetAt(0).Kind;
            }

            DragResolver drag = simulation.Drag;
            if (drag.IsActive && !drag.IsConsumed)
            {
                m_shown[drag.Block] = simulation.Preview.GetOrigin(drag.Block);
            }
        }

        public void SetHeld(int block)
        {
            SetHeldCount++;
            int previous = HeldBlock;
            HeldBlock = block;
            if (previous != -1 && previous != block)
            {
                Settle(previous);
            }

            m_journal.Add("presenter.SetHeld");
        }

        public void SnapHeld()
        {
            int block = HeldBlock;
            HeldBlock = -1;
            if (block != -1)
            {
                Settle(block);
            }

            m_journal.Add("presenter.SnapHeld");
        }

        public void SetTweensPaused(bool paused)
        {
            m_journal.Add(paused ? "presenter.PauseTweens" : "presenter.ResumeTweens");
        }

        public void StopTweens()
        {
            m_journal.Add("presenter.StopTweens");
        }

        public void ReturnPieces()
        {
            m_journal.Add("presenter.ReturnPieces");
        }

        public void ShowBlockedDrag(int block)
        {
            BlockedDragCount++;
            LastBlockedBlock = block;
        }

        // A settle or a snap ends with the root on the placed board's origin of an on-board block.
        private void Settle(int block)
        {
            if (m_board != null && m_board.GetStatus(block) == BlockStatus.OnBoard)
            {
                m_shown[block] = m_board.GetOrigin(block);
            }
        }
    }
}
