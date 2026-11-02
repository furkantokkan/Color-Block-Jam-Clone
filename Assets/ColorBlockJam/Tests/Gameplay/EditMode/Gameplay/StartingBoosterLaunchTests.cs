using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Composition;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// Hub's launch request as the Game scene's starting selection (B-16, B-17, AC-13, AC-14): neither, either or both
    /// of Extra Time and Rocket map onto the selection the loader hands over, the preparer commits their combined debit
    /// once on Home, and Play applies Extra Time before the Rocket in the first command phase after the opening's dimmed
    /// intro. A Rocket without a
    /// candidate is neither charged nor applied, and a second Play spends and applies nothing.
    /// </summary>
    public sealed class StartingBoosterLaunchTests
    {
        // An L block A whose cells can all be Rocket targets, and a one-cell block B.
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

        private const int k_seed = 7;

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
        public void NoRequestOrAnEmptyListSelectsNothing()
        {
            Assert.That(GameAttemptLoader.ReadStartingBoosters(null).IsEmpty, Is.True, "direct Play and Test Level");
            Assert.That(GameAttemptLoader.ReadStartingBoosters(new LevelLaunchRequest(0, null)).IsEmpty, Is.True);
            Assert.That(GameAttemptLoader.ReadStartingBoosters(new LevelLaunchRequest(2, new BoosterId[0])).IsEmpty, Is.True);
        }

        [Test]
        public void EitherOrBothStartingBoostersMapOntoTheSelection()
        {
            StartingBoosterSelection extraTime = Read(BoosterId.ExtraTime);
            StartingBoosterSelection rocket = Read(BoosterId.StartingRocket);
            StartingBoosterSelection both = Read(BoosterId.StartingRocket, BoosterId.ExtraTime);

            Assert.That(extraTime.HasExtraTime && !extraTime.HasRocket, Is.True);
            Assert.That(rocket.HasRocket && !rocket.HasExtraTime, Is.True);
            Assert.That(both.HasExtraTime && both.HasRocket, Is.True, "selector order does not matter");
        }

        [Test]
        public void ABoosterThatIsNotAStartingBoosterIsLoggedAndSelectsNothing()
        {
            LogAssert.Expect(LogType.Error, new Regex("Starting boosters ignored"));

            StartingBoosterSelection selection = Read(BoosterId.StartingRocket, BoosterId.Hammer);

            Assert.That(selection.IsEmpty, Is.True, "nothing is spent for a selection the popup never offered");
        }

        [Test]
        public void TheInGameRocketIsNotAStartingBooster()
        {
            // DEC-167: the pre-level Rocket is its own booster, so a launch naming the in-game Rocket is a wiring error.
            LogAssert.Expect(LogType.Error, new Regex("Starting boosters ignored"));

            Assert.That(Read(BoosterId.Rocket).IsEmpty, Is.True);
        }

        [Test]
        public void AStartingRocketSpendsItsOwnStockAndLeavesTheInGameRocketAlone()
        {
            PrepareFromRequest(s_board, BoosterId.StartingRocket);

            Assert.That(m_boosters.Store.GetStock(BoosterId.StartingRocket).Value, Is.Zero, "the starting charge is spent");
            Assert.That(m_boosters.Store.GetStock(BoosterId.Rocket).Value, Is.EqualTo(1), "the in-game pool is separate");
        }

        [Test]
        public void NoSelectionSpendsNothingAndPlaysPlain()
        {
            StartingBoosterPreparation preparation = PrepareFromRequest(s_board);

            Assert.That(preparation.Outcome, Is.EqualTo(StartingBoosterOutcome.NoSelection));
            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero);
            Assert.That(m_fixture.Session.Play(preparation.Plan), Is.True);
            Assert.That(m_fixture.Session.IsOpening.Value, Is.False);
        }

        [Test]
        public void ExtraTimeAloneAddsItsBonusOnce()
        {
            StartingBoosterPreparation preparation = PrepareFromRequest(s_board, BoosterId.ExtraTime);

            Assert.That(m_boosters.Store.Debits, Is.EqualTo(new[] { new[] { BoosterId.ExtraTime } }));
            Assert.That(m_fixture.Session.Play(preparation.Plan), Is.True);
            RunIntro();
            m_fixture.TickIdle(0f);

            Assert.That(m_fixture.Session.DisplayedSeconds.Value, Is.EqualTo(BoardFixture.k_timerSeconds + 10));
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.Zero, "no Rocket command");
        }

        [Test]
        public void RocketAloneRemovesOneSampledCellOnce()
        {
            StartingBoosterPreparation preparation = PrepareFromRequest(s_board, BoosterId.StartingRocket);
            int before = CountOccupiedCells();

            Assert.That(m_boosters.Store.Debits, Is.EqualTo(new[] { new[] { BoosterId.StartingRocket } }));
            Assert.That(m_fixture.Session.Play(preparation.Plan), Is.True);
            RunIntro();
            m_fixture.TickIdle(0f);
            m_fixture.TickIdle(0f);

            Assert.That(CountOccupiedCells(), Is.EqualTo(before - 1));
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.EqualTo(1));
            Assert.That(m_fixture.Session.DisplayedSeconds.Value, Is.EqualTo(BoardFixture.k_timerSeconds), "no Extra Time");
        }

        [Test]
        public void BothCommitOneCombinedDebitAndExtraTimeAppliesBeforeTheRocket()
        {
            List<string> order = new List<string>();
            RecordingListener listener = new RecordingListener("board", order);
            FixedHookSource hooks = new FixedHookSource(new AttemptHooksBuilder().Add(listener).Build());
            m_fixture = new SessionFixture(s_board, new DoorSpec[0], hookSource: hooks, clock: new RecordingFreezeCountdown(order));
            m_boosters = new BoosterFixture();
            StartingBoosterPreparation preparation = Prepare(Read(BoosterId.StartingRocket, BoosterId.ExtraTime));

            Assert.That(preparation.Outcome, Is.EqualTo(StartingBoosterOutcome.Committed));
            Assert.That(m_boosters.Store.Debits, Is.EqualTo(new[] { new[] { BoosterId.ExtraTime, BoosterId.StartingRocket } }),
                "one combined debit, Extra Time first");
            Assert.That(m_fixture.Session.Play(preparation.Plan), Is.True);
            RunIntro();
            m_fixture.TickIdle(0f);

            Assert.That(order.IndexOf("clock.AddSeconds"), Is.GreaterThanOrEqualTo(0));
            Assert.That(order.IndexOf("clock.AddSeconds"), Is.LessThan(order.IndexOf("board:FootprintChanged")));
        }

        [Test]
        public void ARocketWithoutACandidateIsNeitherChargedNorApplied()
        {
            StartingBoosterPreparation preparation = PrepareFromRequest(s_singles, BoosterId.ExtraTime, BoosterId.StartingRocket);

            Assert.That(preparation.Plan.HasRocket, Is.False);
            Assert.That(preparation.Plan.HasExtraTime, Is.True);
            Assert.That(m_boosters.Store.Debits, Is.EqualTo(new[] { new[] { BoosterId.ExtraTime } }));
            Assert.That(m_boosters.Store.GetStock(BoosterId.StartingRocket).Value, Is.EqualTo(1), "the Rocket charge is kept");

            Assert.That(m_fixture.Session.Play(preparation.Plan), Is.True);
            m_fixture.TickIdle(0f);
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.Zero);
        }

        [Test]
        public void ARocketOnlySelectionWithoutACandidateSpendsNothing()
        {
            StartingBoosterPreparation preparation = PrepareFromRequest(s_singles, BoosterId.StartingRocket);

            Assert.That(preparation.Outcome, Is.EqualTo(StartingBoosterOutcome.NothingEligible));
            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero);
            Assert.That(preparation.Plan.IsEmpty, Is.True);
        }

        [Test]
        public void ASecondPlayNeitherSpendsNorAppliesAgain()
        {
            StartingBoosterPreparation preparation = PrepareFromRequest(s_board, BoosterId.ExtraTime, BoosterId.StartingRocket);
            Assert.That(m_fixture.Session.Play(preparation.Plan), Is.True);

            Assert.That(m_fixture.Session.Play(preparation.Plan), Is.False);
            RunIntro();
            m_fixture.TickIdle(0f);
            m_fixture.TickIdle(0f);

            Assert.That(m_boosters.Store.ConsumeCount, Is.EqualTo(1));
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.EqualTo(1), "one Rocket command");
            Assert.That(m_fixture.Session.DisplayedSeconds.Value, Is.EqualTo(BoardFixture.k_timerSeconds + 10), "one bonus");
        }

        // The opening's dimmed intro: the boosters apply in the command phase after it (user request of 2026-09-28).
        private void RunIntro()
        {
            m_fixture.TickIdle(OpeningPlan.k_defaultIntroSeconds);
        }

        private static StartingBoosterSelection Read(params BoosterId[] boosters)
        {
            return GameAttemptLoader.ReadStartingBoosters(new LevelLaunchRequest(0, boosters));
        }

        // The launch request's selection, prepared on Home against a fresh attempt with one charge of each booster.
        private StartingBoosterPreparation PrepareFromRequest(string[] rows, params BoosterId[] boosters)
        {
            m_fixture = new SessionFixture(rows, new DoorSpec[0], clock: new AttemptCountdown());
            m_boosters = new BoosterFixture();
            return Prepare(Read(boosters));
        }

        // The fake debit completes synchronously, so the preparation is complete when it returns.
        private StartingBoosterPreparation Prepare(StartingBoosterSelection selection)
        {
            Assert.That(m_fixture.Session.TryPrepare(m_fixture.Level, out string error), Is.True, error);
            Task<StartingBoosterPreparation> task = m_boosters.CreatePreparer().PrepareAsync(m_fixture.Session, selection,
                k_seed, CancellationToken.None);
            Assert.That(task.IsCompleted, Is.True);
            return task.Result;
        }

        private int CountOccupiedCells()
        {
            IBoardReader board = m_fixture.Session.Attempt.Simulation.Board;
            int count = 0;
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (board.GetOccupant(new Cell(x, y)) >= 0)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
