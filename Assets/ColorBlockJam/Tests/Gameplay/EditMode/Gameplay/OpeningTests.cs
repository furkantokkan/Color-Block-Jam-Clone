using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Boosters;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// The starting-booster opening (B-16, B-17, B-29): StartingBoosterPreparer commits one combined debit on Home
    /// for the eligible selection, and LevelSession.Play(plan) runs the intro (the dim, then its fade), applies Extra
    /// Time before the Rocket once in the next command phase, then holds for the Rocket's flight (user request of
    /// 2026-09-28). Intro and hold keep the countdown, the pointer and the command source off. Pause keeps the progress,
    /// Teardown ends it, and a second Play does nothing (AC-13, AC-14).
    /// </summary>
    public sealed class OpeningTests
    {
        // An L block A whose bridge (0, 0) splits it, and a one-cell block B at (3, 0).
        private static readonly string[] s_board =
        {
            "A...",
            "A...",
            "AA.B"
        };

        // Only one-cell blocks: the starting Rocket has no candidate.
        private static readonly string[] s_singles =
        {
            "....",
            "A...",
            "...B"
        };

        private static readonly Cell s_bridge = new Cell(0, 0);

        private SessionFixture m_fixture;
        private BoosterFixture m_boosters;

        [TearDown]
        public void TearDown()
        {
            m_boosters?.Dispose();
            m_fixture?.Dispose();
            m_boosters = null;
            m_fixture = null;
        }

        [Test]
        public void ExtraTimeAppliesBeforeTheRocketOnceTheDimHasFaded()
        {
            List<string> order = new List<string>();
            RecordingListener listener = new RecordingListener("board", order);
            FixedHookSource hooks = new FixedHookSource(new AttemptHooksBuilder().Add(listener).Build());
            RecordingFreezeCountdown clock = new RecordingFreezeCountdown(order);
            m_fixture = new SessionFixture(s_board, new DoorSpec[0], hookSource: hooks, clock: clock);
            Prepare();

            Assert.That(m_fixture.Session.Play(BridgeRocketPlan(true)), Is.True);
            Assert.That(m_fixture.Session.IsOpening.Value, Is.True);
            Assert.That(m_fixture.Session.IsOpeningDimmed.Value, Is.True, "the intro starts dimmed");
            Assert.That(order, Is.Empty, "Play applies nothing itself");

            m_fixture.TickIdle();
            Assert.That(order, Is.Empty, "nothing applies while the screen is dimmed");

            m_fixture.TickIdle(OpeningPlan.k_defaultIntroSeconds);
            Assert.That(m_fixture.Session.IsOpeningDimmed.Value, Is.False, "the dim is gone");
            Assert.That(order, Is.Empty, "the boosters apply in the next command phase");

            m_fixture.TickIdle();

            Assert.That(order.IndexOf("clock.AddSeconds"), Is.GreaterThanOrEqualTo(0));
            Assert.That(order.IndexOf("clock.AddSeconds"), Is.LessThan(order.IndexOf("board:FootprintChanged")));
            Assert.That(clock.Remaining, Is.EqualTo(BoardFixture.k_timerSeconds + 10), "the hold kept the timer");
            BoardState board = m_fixture.Session.Attempt.Simulation.Board;
            Assert.That(board.BlockCount, Is.EqualTo(3), "the bridge split A into two blocks");
            Assert.That(board.RequiredRemaining, Is.EqualTo(3));
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.EqualTo(1));
            Assert.That(m_fixture.Session.DisplayedSeconds.Value, Is.EqualTo(BoardFixture.k_timerSeconds + 10));
        }

        [Test]
        public void TheHoldKeepsTheTimerThePointerAndTheCommandSourceOff()
        {
            AttemptCountdown clock = new AttemptCountdown();
            m_fixture = new SessionFixture(s_board, new DoorSpec[0], clock: clock);
            Prepare();
            ReplayCommandSource source = new ReplayCommandSource(new[] { GameCommand.Move(1, new Cell(3, 0), new Cell(3, 1)) });
            m_fixture.Session.TrySetCommandSource(source);
            m_fixture.Session.Play(BridgeRocketPlan(false));

            m_fixture.TickIdle(0.5f);
            m_fixture.Tick(PointerPhase.Down, 3f, 0f);
            m_fixture.Tick(PointerPhase.Held, 3f, 1f);

            Assert.That(clock.Remaining, Is.EqualTo(BoardFixture.k_timerSeconds));
            Assert.That(m_fixture.Presenter.SetHeldCount, Is.Zero, "no drag during the hold");
            Assert.That(source.Position, Is.Zero, "no source command during the hold");
            Assert.That(m_fixture.Session.IsOpening.Value, Is.True);

            m_fixture.Tick(PointerPhase.Up, 3f, 1f);
            m_fixture.TickIdle(OpeningPlan.k_defaultIntroSeconds);
            m_fixture.TickIdle();

            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.EqualTo(1), "the Rocket flew");
            Assert.That(m_fixture.Session.IsOpening.Value, Is.True, "input waits for the Rocket's flight");
            Assert.That(clock.Remaining, Is.EqualTo(BoardFixture.k_timerSeconds));

            m_fixture.TickIdle(OpeningPlan.k_defaultHoldSeconds);

            Assert.That(m_fixture.Session.IsOpening.Value, Is.False);
            Assert.That(clock.Remaining, Is.EqualTo(BoardFixture.k_timerSeconds),
                "no Playing time reaches the countdown before the first block press (DEC-189)");

            m_fixture.TickIdle();
            Assert.That(source.Position, Is.EqualTo(1), "the command source runs after the hold");

            m_fixture.Tick(PointerPhase.Down, 3f, 1f);
            Assert.That(clock.Remaining, Is.EqualTo(BoardFixture.k_timerSeconds - SessionFixture.k_frameSeconds).Within(1e-4f),
                "the first block press starts the countdown");
        }

        [Test]
        public void PauseKeepsTheHoldProgress()
        {
            AttemptCountdown clock = new AttemptCountdown();
            m_fixture = new SessionFixture(s_board, new DoorSpec[0], clock: clock);
            Prepare();
            m_fixture.Session.Play(BridgeRocketPlan(false));
            m_fixture.TickIdle(OpeningPlan.k_defaultIntroSeconds - 0.3f);

            m_fixture.Session.Pause(PauseReason.User);
            m_fixture.TickIdle(5f);
            m_fixture.Session.Resume();
            m_fixture.TickIdle(0.1f);

            Assert.That(m_fixture.Session.IsOpening.Value, Is.True, "0.2 s of the intro remains");
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.Zero);
            Assert.That(clock.Remaining, Is.EqualTo(BoardFixture.k_timerSeconds));

            m_fixture.TickIdle(0.3f);
            m_fixture.TickIdle();
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.EqualTo(1));

            m_fixture.Session.Pause(PauseReason.User);
            m_fixture.TickIdle(5f);
            m_fixture.Session.Resume();
            m_fixture.TickIdle(OpeningPlan.k_defaultHoldSeconds - SessionFixture.k_frameSeconds - 0.1f);
            Assert.That(m_fixture.Session.IsOpening.Value, Is.True, "0.1 s of the hold remains");

            m_fixture.TickIdle(0.2f);

            Assert.That(m_fixture.Session.IsOpening.Value, Is.False);
            Assert.That(clock.Remaining, Is.EqualTo(BoardFixture.k_timerSeconds), "no block was pressed yet (DEC-189)");
        }

        [Test]
        public void TeardownEndsTheOpeningAndRestartHasNone()
        {
            AttemptCountdown clock = new AttemptCountdown();
            m_fixture = new SessionFixture(s_board, new DoorSpec[0], clock: clock);
            Prepare();
            m_fixture.Session.Play(BridgeRocketPlan(true));
            m_fixture.Session.Pause(PauseReason.User);

            Assert.That(m_fixture.Session.Restart(), Is.True);

            Assert.That(m_fixture.Session.IsOpening.Value, Is.False);
            m_fixture.TickIdle();
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.Zero, "nothing unapplied carries over");
            Assert.That(clock.Remaining, Is.EqualTo(BoardFixture.k_timerSeconds), "no Extra Time carried over");
            m_fixture.Tick(PointerPhase.Down, 3f, 0f);
            Assert.That(m_fixture.Presenter.HeldBlock, Is.EqualTo(1), "no hold after Restart");
            Assert.That(clock.Remaining, Is.EqualTo(BoardFixture.k_timerSeconds - SessionFixture.k_frameSeconds).Within(1e-4f));
        }

        [Test]
        public void HomeEndsAnUnappliedOpening()
        {
            AttemptCountdown clock = new AttemptCountdown();
            m_fixture = new SessionFixture(s_board, new DoorSpec[0], clock: clock);
            Prepare();
            m_fixture.Session.Play(BridgeRocketPlan(true));
            m_fixture.Session.Pause(PauseReason.User);

            Assert.That(m_fixture.Session.GoHome(), Is.True);
            Assert.That(m_fixture.Session.IsOpening.Value, Is.False);

            Prepare();
            m_fixture.Session.Play();
            m_fixture.Tick(PointerPhase.Down, 3f, 0f);
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.Zero);
            Assert.That(clock.Remaining, Is.LessThan(BoardFixture.k_timerSeconds), "no hold: the first press starts the clock");
        }

        [Test]
        public void ASecondPlayAppliesNothingTwice()
        {
            AttemptCountdown clock = new AttemptCountdown();
            m_fixture = new SessionFixture(s_board, new DoorSpec[0], clock: clock);
            Prepare();
            OpeningPlan plan = BridgeRocketPlan(true);

            Assert.That(m_fixture.Session.Play(plan), Is.True);
            Assert.That(m_fixture.Session.Play(plan), Is.False);
            Assert.That(m_fixture.Session.Play(), Is.False);
            m_fixture.TickIdle(OpeningPlan.k_defaultIntroSeconds);
            m_fixture.TickIdle();
            m_fixture.TickIdle(1f);

            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.EqualTo(1));
            Assert.That(clock.Remaining, Is.EqualTo(BoardFixture.k_timerSeconds + 10f), "the bonus was added once");
        }

        [Test]
        public void AnEmptyPlanPlaysNormally()
        {
            m_fixture = new SessionFixture(s_board, new DoorSpec[0], clock: new AttemptCountdown());
            Prepare();

            Assert.That(m_fixture.Session.Play(OpeningPlan.None), Is.True);

            Assert.That(m_fixture.Session.IsOpening.Value, Is.False);
            m_fixture.Tick(PointerPhase.Down, 3f, 0f);
            Assert.That(m_fixture.Presenter.HeldBlock, Is.EqualTo(1));
        }

        [Test]
        public void ExtraTimeNeedsAClockThatCanAddTime()
        {
            m_fixture = new SessionFixture(s_board, new DoorSpec[0]);
            Prepare();

            Assert.Throws<InvalidOperationException>(() => m_fixture.Session.Play(BridgeRocketPlan(true)));
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Home));
            Assert.That(m_fixture.Session.Play(BridgeRocketPlan(false)), Is.True, "a Rocket-only opening needs no clock support");
        }

        [Test]
        public void NoSelectionSpendsNothingAndOpensNothing()
        {
            CreateWithBoosters(s_board);

            StartingBoosterPreparation preparation = PrepareBoosters(StartingBoosterSelection.None, 1);

            Assert.That(preparation.Outcome, Is.EqualTo(StartingBoosterOutcome.NoSelection));
            Assert.That(preparation.Plan.IsEmpty, Is.True);
            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero);
        }

        [Test]
        public void BothSelectionsCommitOneCombinedDebit()
        {
            CreateWithBoosters(s_board);

            StartingBoosterPreparation preparation = PrepareBoosters(new StartingBoosterSelection(true, true), 7);

            Assert.That(preparation.Outcome, Is.EqualTo(StartingBoosterOutcome.Committed));
            Assert.That(m_boosters.Store.ConsumeCount, Is.EqualTo(1));
            Assert.That(m_boosters.Store.Debits[0], Is.EqualTo(new[] { BoosterId.ExtraTime, BoosterId.StartingRocket }));
            Assert.That(m_boosters.Store.GetStock(BoosterId.ExtraTime).Value, Is.Zero);
            Assert.That(m_boosters.Store.GetStock(BoosterId.StartingRocket).Value, Is.Zero);
            Assert.That(m_boosters.Store.GetStock(BoosterId.Rocket).Value, Is.EqualTo(1), "the in-game Rocket pool is untouched");
            OpeningPlan plan = preparation.Plan;
            Assert.That(plan.HasExtraTime, Is.True);
            Assert.That(plan.BonusSeconds, Is.EqualTo(m_boosters.ExtraTime.BonusSeconds));
            Assert.That(plan.HasRocket, Is.True);
            Assert.That(plan.Rocket, Is.EqualTo(ExpectedRocket(7)), "the seeded candidate of the prepared board");
            Assert.That(plan.HoldSeconds, Is.EqualTo(OpeningPlan.k_defaultHoldSeconds));
            Assert.That(plan.IntroSeconds, Is.EqualTo(OpeningPlan.k_defaultIntroSeconds));
            Assert.That(plan.Includes(BoosterId.ExtraTime), Is.True);
            Assert.That(plan.Includes(BoosterId.StartingRocket), Is.True);
            Assert.That(plan.Includes(BoosterId.Rocket), Is.False, "the in-game Rocket is another booster (DEC-167)");
            Assert.That(plan.Includes(BoosterId.Hammer), Is.False);
        }

        [Test]
        public void TheSameSeedChoosesTheSameCellIncludingASplittingCell()
        {
            CreateWithBoosters(s_board, initialStock: 9);
            int seed = FindSeedFor(s_bridge);

            GameCommand first = PrepareBoosters(new StartingBoosterSelection(false, true), seed).Plan.Rocket;
            GameCommand second = PrepareBoosters(new StartingBoosterSelection(false, true), seed).Plan.Rocket;

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.To, Is.EqualTo(s_bridge));
            OpeningPlan plan = new OpeningPlan(false, 0, true, first);
            Assert.That(m_fixture.Session.Play(plan), Is.True);
            m_fixture.TickIdle(OpeningPlan.k_defaultIntroSeconds);
            m_fixture.TickIdle();
            Assert.That(m_fixture.Session.Attempt.Simulation.Board.BlockCount, Is.EqualTo(3));
        }

        [Test]
        public void ARocketWithoutACandidateIsDroppedFromTheDebit()
        {
            CreateWithBoosters(s_singles);

            StartingBoosterPreparation both = PrepareBoosters(new StartingBoosterSelection(true, true), 3);

            Assert.That(both.Outcome, Is.EqualTo(StartingBoosterOutcome.Committed));
            Assert.That(m_boosters.Store.Debits[0], Is.EqualTo(new[] { BoosterId.ExtraTime }));
            Assert.That(both.Plan.HasRocket, Is.False);
            Assert.That(m_boosters.Store.GetStock(BoosterId.StartingRocket).Value, Is.EqualTo(1), "no Rocket charge spent");

            StartingBoosterPreparation rocketOnly = PrepareBoosters(new StartingBoosterSelection(false, true), 3);
            Assert.That(rocketOnly.Outcome, Is.EqualTo(StartingBoosterOutcome.NothingEligible));
            Assert.That(m_boosters.Store.ConsumeCount, Is.EqualTo(1));
        }

        [Test]
        public void ABoosterWithoutStockIsDroppedFromTheDebit()
        {
            CreateWithBoosters(s_board);
            m_boosters.Store.SetStock(BoosterId.ExtraTime, 0);

            StartingBoosterPreparation preparation = PrepareBoosters(new StartingBoosterSelection(true, true), 3);

            Assert.That(m_boosters.Store.Debits[0], Is.EqualTo(new[] { BoosterId.StartingRocket }));
            Assert.That(preparation.Plan.HasExtraTime, Is.False);
            Assert.That(preparation.Plan.HasRocket, Is.True);
        }

        [Test]
        public void ACountdownThatCannotAddTimeDropsExtraTimeFromTheDebit()
        {
            // The fixture's journaling countdown has no IFreezeCountdown side, like the Game scene's interim clock.
            CreateWithBoosters(s_board, supportsTimeBoosters: false);
            Assert.That(m_fixture.Session.SupportsTimeBoosters, Is.False);

            StartingBoosterPreparation extraTimeOnly = PrepareBoosters(new StartingBoosterSelection(true, false), 3);

            Assert.That(extraTimeOnly.Outcome, Is.EqualTo(StartingBoosterOutcome.NothingEligible));
            Assert.That(extraTimeOnly.Plan.IsEmpty, Is.True);
            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero);

            StartingBoosterPreparation both = PrepareBoosters(new StartingBoosterSelection(true, true), 3);

            Assert.That(both.Outcome, Is.EqualTo(StartingBoosterOutcome.Committed));
            Assert.That(m_boosters.Store.ConsumeCount, Is.EqualTo(1));
            Assert.That(m_boosters.Store.Debits[0], Is.EqualTo(new[] { BoosterId.StartingRocket }));
            Assert.That(both.Plan.HasExtraTime, Is.False);
            Assert.That(both.Plan.HasRocket, Is.True);
            Assert.That(m_boosters.Store.GetStock(BoosterId.ExtraTime).Value, Is.EqualTo(1), "no Extra Time charge spent");
            bool played = false;
            Assert.DoesNotThrow(() => played = m_fixture.Session.Play(both.Plan), "Play never meets an unsupported Extra Time");
            Assert.That(played, Is.True);
        }

        [Test]
        public void AFailedStartingCommitStartsPlain()
        {
            CreateWithBoosters(s_board);
            m_boosters.Store.NextResult = BoosterConsumeResult.SaveFailed;

            StartingBoosterPreparation preparation = PrepareBoosters(new StartingBoosterSelection(true, true), 3);

            Assert.That(preparation.Outcome, Is.EqualTo(StartingBoosterOutcome.CommitFailed));
            Assert.That(preparation.ConsumeResult, Is.EqualTo(BoosterConsumeResult.SaveFailed));
            Assert.That(preparation.Plan.IsEmpty, Is.True);
            Assert.That(m_boosters.Store.GetStock(BoosterId.ExtraTime).Value, Is.EqualTo(1));
            Assert.That(m_fixture.Session.Play(preparation.Plan), Is.True);
            Assert.That(m_fixture.Session.IsOpening.Value, Is.False);
        }

        [Test]
        public void PreparationNeedsAPreparedAttemptOnHome()
        {
            CreateWithBoosters(s_board);
            m_fixture.Session.Play();

            Task<StartingBoosterPreparation> task = m_boosters.CreatePreparer().PrepareAsync(m_fixture.Session,
                new StartingBoosterSelection(true, false), 1, CancellationToken.None);

            Assert.That(task.IsFaulted, Is.True);
            Assert.That(task.Exception.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero);
        }

        [Test]
        public void TheSelectionReadsOnlyStartingBoosters()
        {
            StartingBoosterSelection selection = StartingBoosterSelection.From(new[] { BoosterId.StartingRocket, BoosterId.StartingRocket });

            Assert.That(selection.HasRocket, Is.True);
            Assert.That(selection.HasExtraTime, Is.False);
            Assert.That(StartingBoosterSelection.From(new BoosterId[0]).IsEmpty, Is.True);
            Assert.Throws<ArgumentException>(() => StartingBoosterSelection.From(new[] { BoosterId.Hammer }));
            Assert.Throws<ArgumentException>(() => StartingBoosterSelection.From(new[] { BoosterId.Rocket }), "DEC-167");
            Assert.That(new NoStartingBoosters().TryTake(out StartingBoosterSelection none), Is.False);
            Assert.That(none.IsEmpty, Is.True);
        }

        private void Prepare()
        {
            Assert.That(m_fixture.Session.TryPrepare(m_fixture.Level, out string error), Is.True, error);
        }

        private void CreateWithBoosters(string[] rows, int initialStock = 1, bool supportsTimeBoosters = true)
        {
            m_fixture = new SessionFixture(rows, new DoorSpec[0], clock: supportsTimeBoosters ? new AttemptCountdown() : null);
            m_boosters = new BoosterFixture(initialStock);
        }

        // The fake debit completes synchronously, so the preparation is complete when it returns.
        private StartingBoosterPreparation PrepareBoosters(StartingBoosterSelection selection, int seed)
        {
            Prepare();
            Task<StartingBoosterPreparation> task = m_boosters.CreatePreparer().PrepareAsync(m_fixture.Session, selection,
                seed, CancellationToken.None);
            Assert.That(task.IsCompleted, Is.True);
            return task.Result;
        }

        private OpeningPlan BridgeRocketPlan(bool withExtraTime)
        {
            IBoardReader board = m_fixture.Session.Attempt.Simulation.Board;
            GameCommand rocket = GameCommand.RemoveCell(0, board.GetOrigin(0), s_bridge);
            return new OpeningPlan(withExtraTime, 10, true, rocket);
        }

        private GameCommand ExpectedRocket(int seed)
        {
            IBoardReader board = m_fixture.Session.Attempt.Simulation.Board;
            Cell[] candidates = new Cell[board.Model.TotalCellCount];
            int count = StartingRocketCandidates.Collect(board, candidates);
            Assert.That(StartingRocketCandidates.TrySample(candidates, count, seed, out Cell cell), Is.True);
            int block = board.GetOccupant(cell);
            return GameCommand.RemoveCell(block, board.GetOrigin(block), cell);
        }

        private int FindSeedFor(Cell target)
        {
            Prepare();
            IBoardReader board = m_fixture.Session.Attempt.Simulation.Board;
            Cell[] candidates = new Cell[board.Model.TotalCellCount];
            int count = StartingRocketCandidates.Collect(board, candidates);
            for (int seed = 0; seed < 1000; seed++)
            {
                StartingRocketCandidates.TrySample(candidates, count, seed, out Cell cell);
                if (cell == target)
                {
                    return seed;
                }
            }

            Assert.Fail("No seed in 0..999 samples " + target);
            return 0;
        }
    }
}
