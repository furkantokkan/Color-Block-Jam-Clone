using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// BoosterUseCoordinator over a session with fake player ports (B-12, B-13, B-18): presses refuse outside a
    /// usable Playing attempt, an empty stock offers the purchase or blocks, targeted buttons toggle and switch for
    /// free, Rocket refuses a block's final cell, and each accepted use starts exactly one debit, after the accepting
    /// tick, whose result decides the effect (AC-3, AC-7, AC-10, AC-12).
    /// </summary>
    public sealed class BoosterUseCoordinatorTests
    {
        // A two-cell block A at (0, 1)-(1, 1) and a one-cell block B at (3, 0).
        private static readonly string[] s_board =
        {
            "....",
            "AA..",
            "...B"
        };

        // A at (0, 1)-(1, 1), B at (3, 0) and b at (0, 2): b is a second block of B's colour.
        private static readonly string[] s_twoOfOneColour =
        {
            "b...",
            "AA..",
            "...B"
        };

        private SessionFixture m_session;
        private BoosterFixture m_boosters;
        private BoosterUseCoordinator m_coordinator;

        [TearDown]
        public void TearDown()
        {
            m_boosters?.Store.Gate?.TrySetResult(true);
            m_boosters?.Dispose();
            m_session?.Dispose();
            m_boosters = null;
            m_session = null;
            m_coordinator = null;
        }

        [Test]
        public void PressesAreRefusedUnlessTheAttemptIsPlaying()
        {
            Create();
            Assert.That(m_session.Session.TryPrepare(m_session.Level, out _), Is.True);
            Assert.That(m_coordinator.Press(BoosterId.Hammer), Is.EqualTo(BoosterPressResult.Refused), "Home");

            m_session.Session.Play();
            m_session.Session.Pause(PauseReason.User);
            Assert.That(m_coordinator.Press(BoosterId.Hammer), Is.EqualTo(BoosterPressResult.Refused), "Paused");
            Assert.That(m_session.Targeting.Mode.Value, Is.EqualTo(BoosterId.None));
        }

        [Test]
        public void PressesAreRefusedDuringTheOpening()
        {
            Create(new AttemptCountdown());
            Assert.That(m_session.Session.TryPrepare(m_session.Level, out _), Is.True);
            OpeningPlan plan = new OpeningPlan(true, 10, false, default);
            Assert.That(m_session.Session.Play(plan), Is.True);

            Assert.That(m_coordinator.Press(BoosterId.Hammer), Is.EqualTo(BoosterPressResult.Refused));
            Assert.That(m_coordinator.Press(BoosterId.TimeFreeze), Is.EqualTo(BoosterPressResult.Refused));
        }

        [Test]
        public void PressesAreRefusedWhilePlayerDataIsSaving()
        {
            Create();
            m_session.PrepareAndPlay();
            m_boosters.Store.SetSaving(true);

            Assert.That(m_coordinator.Press(BoosterId.Rocket), Is.EqualTo(BoosterPressResult.Refused));
            Assert.That(m_coordinator.Press(BoosterId.TimeFreeze), Is.EqualTo(BoosterPressResult.Refused));
            Assert.That(m_session.Targeting.Mode.Value, Is.EqualTo(BoosterId.None));
            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero);
        }

        [Test]
        public void APreLevelOnlyBoosterIsRefusedInGame()
        {
            Create();
            m_session.PrepareAndPlay();

            Assert.That(m_coordinator.Press(BoosterId.ExtraTime), Is.EqualTo(BoosterPressResult.Refused));
        }

        [Test]
        public void AnEmptyStockWithoutAPurchasePresentationIsBlockedAndEndsTargeting()
        {
            Create();
            m_session.PrepareAndPlay();
            m_boosters.Host.Available = false;
            m_boosters.Store.SetStock(BoosterId.Hammer, 0);
            m_coordinator.Press(BoosterId.Rocket);

            Assert.That(m_coordinator.Press(BoosterId.Hammer), Is.EqualTo(BoosterPressResult.Blocked));

            Assert.That(m_session.Targeting.Mode.Value, Is.EqualTo(BoosterId.None));
            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Playing), "nothing pauses");
            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero);
        }

        [Test]
        public void AnEmptyStockOpensThePurchaseUnderPausedPurchaseAndCloseResumes()
        {
            Create();
            m_session.PrepareAndPlay();
            m_boosters.Store.SetStock(BoosterId.Vacuum, 0);

            Assert.That(m_coordinator.Press(BoosterId.Vacuum), Is.EqualTo(BoosterPressResult.PurchaseOpened));

            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_session.Session.PauseReason, Is.EqualTo(PauseReason.Purchase));
            Assert.That(m_boosters.Host.IsShowing, Is.True);
            Assert.That(m_boosters.Host.LastDefinition, Is.SameAs(m_boosters.Vacuum));
            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero, "the purchase never casts");

            Assert.That(m_coordinator.ClosePurchase(), Is.True);
            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_boosters.Host.IsShowing, Is.False);
            Assert.That(m_session.Targeting.Mode.Value, Is.EqualTo(BoosterId.None), "no automatic selection");
        }

        [Test]
        public void ClosingThePurchaseAfterBackgroundKeepsThePause()
        {
            Create(resumesOnForeground: false);
            m_session.PrepareAndPlay();
            m_boosters.Store.SetStock(BoosterId.Hammer, 0);
            m_coordinator.Press(BoosterId.Hammer);
            m_session.Session.NotifyBackground(true);

            Assert.That(m_coordinator.ClosePurchase(), Is.False);

            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_session.Session.PauseReason, Is.EqualTo(PauseReason.Background));
            Assert.That(m_boosters.Host.IsShowing, Is.False);
        }

        [Test]
        public void ThePurchaseHidesWhenPlayResumesWithoutClosingIt()
        {
            Create(resumesOnForeground: true);
            m_session.PrepareAndPlay();
            m_boosters.Store.SetStock(BoosterId.Hammer, 0);
            m_coordinator.Press(BoosterId.Hammer);
            m_session.Session.NotifyBackground(true);

            // The interim overlay resumes a background pause on foreground (DEC-116).
            m_session.Session.NotifyBackground(false);

            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_boosters.Host.IsShowing, Is.False, "no popup over a running game");
        }

        [Test]
        public void TargetedButtonsToggleAndSwitchForFree()
        {
            Create();
            m_session.PrepareAndPlay();

            Assert.That(m_coordinator.Press(BoosterId.Rocket), Is.EqualTo(BoosterPressResult.TargetingStarted));
            Assert.That(m_session.Targeting.Mode.Value, Is.EqualTo(BoosterId.Rocket));
            Assert.That(m_coordinator.Press(BoosterId.Hammer), Is.EqualTo(BoosterPressResult.TargetingStarted));
            Assert.That(m_session.Targeting.Mode.Value, Is.EqualTo(BoosterId.Hammer));
            Assert.That(m_coordinator.Press(BoosterId.Hammer), Is.EqualTo(BoosterPressResult.TargetingCancelled));
            Assert.That(m_session.Targeting.Mode.Value, Is.EqualTo(BoosterId.None));

            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero);
            Assert.That(m_boosters.Store.GetStock(BoosterId.Hammer).Value, Is.EqualTo(1));
            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Playing), "targeting does not pause");
        }

        [Test]
        public void TheBannerCloseAndBackCancelForFree()
        {
            Create();
            m_session.PrepareAndPlay();
            m_coordinator.Press(BoosterId.Vacuum);

            m_session.Targeting.Cancel();
            Assert.That(m_session.Targeting.Mode.Value, Is.EqualTo(BoosterId.None), "close");

            m_coordinator.Press(BoosterId.Vacuum);
            m_session.Pointer.PressBack();
            m_session.TickIdle();
            Assert.That(m_session.Targeting.Mode.Value, Is.EqualTo(BoosterId.None), "Back");
            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero);
        }

        [Test]
        public void ATimeFreezePressWhileTargetingEndsTargetingFirstAndSubmits()
        {
            Create(new AttemptCountdown());
            m_session.PrepareAndPlay();
            m_coordinator.Press(BoosterId.Rocket);

            Assert.That(m_coordinator.Press(BoosterId.TimeFreeze), Is.EqualTo(BoosterPressResult.Submitted));

            Assert.That(m_session.Targeting.Mode.Value, Is.EqualTo(BoosterId.None));
            Assert.That(m_session.Session.PauseReason, Is.EqualTo(PauseReason.Saving));
            Assert.That(m_coordinator.IsBusy.Value, Is.True);
            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero, "the debit starts only after a yield");
        }

        [Test]
        public void AOneCellRocketTargetIsRejectedKeepsTheModeAndRequestsTheBlockedFeedback()
        {
            Create();
            m_session.PrepareAndPlay();
            m_coordinator.Press(BoosterId.Rocket);

            m_session.Tap(3f, 0f);

            Assert.That(m_session.Targeting.Mode.Value, Is.EqualTo(BoosterId.Rocket));
            Assert.That(m_session.Presenter.BlockedDragCount, Is.EqualTo(1));
            Assert.That(m_session.Presenter.LastBlockedBlock, Is.EqualTo(1));
            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_coordinator.IsBusy.Value, Is.False);
            Assert.That(m_session.Session.Attempt.Simulation.Board.GetFootprint(1).CellCount, Is.EqualTo(1));
        }

        [Test]
        public void AnEmptyCellTargetEndsTheModeForFree()
        {
            Create();
            m_session.PrepareAndPlay();
            m_coordinator.Press(BoosterId.Hammer);

            m_session.Tap(2f, 2f);

            Assert.That(m_session.Targeting.Mode.Value, Is.EqualTo(BoosterId.None));
            Assert.That(m_coordinator.IsBusy.Value, Is.False);
        }

        [UnityTest]
        public IEnumerator AHammerUseDebitsOnceWithItsIdAfterTheAcceptingTickAndApplies()
        {
            Create();
            m_session.PrepareAndPlay();
            m_coordinator.Press(BoosterId.Hammer);

            m_session.Tap(0f, 1f);

            Assert.That(m_session.Session.PauseReason, Is.EqualTo(PauseReason.Saving));
            Assert.That(m_coordinator.IsBusy.Value, Is.True);
            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero, "no store work inside the accepting tick");
            Assert.That(m_coordinator.Press(BoosterId.Rocket), Is.EqualTo(BoosterPressResult.Refused), "one use at a time");

            yield return BoosterFixture.WaitForSave(m_coordinator);

            Assert.That(m_boosters.Store.ConsumeCount, Is.EqualTo(1));
            Assert.That(m_boosters.Store.Debits[0], Is.EqualTo(new[] { BoosterId.Hammer }));
            Assert.That(m_boosters.Store.GetStock(BoosterId.Hammer).Value, Is.Zero);
            Assert.That(m_coordinator.IsBusy.Value, Is.False);
            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Playing));

            m_session.TickIdle();

            BoardState board = m_session.Session.Attempt.Simulation.Board;
            Assert.That(board.GetStatus(0), Is.EqualTo(BlockStatus.Removed), "the whole two-cell block is removed");
            Assert.That(board.GetStatus(1), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(m_session.Session.Attempt.Simulation.Commands.Log.GetAt(0).Kind, Is.EqualTo(CommandKind.RemoveBlock));
        }

        [UnityTest]
        public IEnumerator ARocketUseRemovesTheTappedCell()
        {
            Create();
            m_session.PrepareAndPlay();
            m_coordinator.Press(BoosterId.Rocket);

            m_session.Tap(1f, 1f);
            yield return BoosterFixture.WaitForSave(m_coordinator);
            m_session.TickIdle();

            BlockFootprint footprint = m_session.Session.Attempt.Simulation.Board.GetFootprint(0);
            Assert.That(footprint.CellCount, Is.EqualTo(1));
            Assert.That(m_session.Session.Attempt.Simulation.Board.GetOccupant(new Cell(1, 1)), Is.LessThan(0));
            Assert.That(m_boosters.Store.Debits[0], Is.EqualTo(new[] { BoosterId.Rocket }));
        }

        [UnityTest]
        public IEnumerator AVacuumUseRemovesEveryBlockOfTheTappedColourInOneCommand()
        {
            Create(rows: s_twoOfOneColour);
            m_session.PrepareAndPlay();
            m_coordinator.Press(BoosterId.Vacuum);

            m_session.Tap(3f, 0f);
            yield return BoosterFixture.WaitForSave(m_coordinator);
            m_session.TickIdle();

            BoardState board = m_session.Session.Attempt.Simulation.Board;
            Assert.That(board.GetStatus(1), Is.EqualTo(BlockStatus.Removed), "the tapped block");
            Assert.That(board.GetStatus(2), Is.EqualTo(BlockStatus.Removed), "the other block of its colour");
            Assert.That(board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard), "another colour stays");
            CommandLog log = m_session.Session.Attempt.Simulation.Commands.Log;
            Assert.That(log.Count, Is.EqualTo(1));
            Assert.That(log.GetAt(0).Kind, Is.EqualTo(CommandKind.RemoveColor));
            Assert.That(m_boosters.Store.Debits[0], Is.EqualTo(new[] { BoosterId.Vacuum }));
        }

        [UnityTest]
        public IEnumerator AFailedDebitResumesWithoutTheEffect()
        {
            Create();
            m_session.PrepareAndPlay();
            m_boosters.Store.NextResult = BoosterConsumeResult.SaveFailed;
            m_coordinator.Press(BoosterId.Hammer);

            m_session.Tap(0f, 1f);
            yield return BoosterFixture.WaitForSave(m_coordinator);
            m_session.TickIdle();

            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_boosters.Store.GetStock(BoosterId.Hammer).Value, Is.EqualTo(1));
            Assert.That(m_session.Session.Attempt.Simulation.Board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(m_session.Session.Attempt.Simulation.Commands.Log.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AGatedDebitKeepsTheSessionSavingUntilItResolves()
        {
            Create();
            m_session.PrepareAndPlay();
            m_boosters.Store.Gate = new TaskCompletionSource<bool>();
            m_coordinator.Press(BoosterId.Hammer);
            m_session.Tap(0f, 1f);
            BoardState board = m_session.Session.Attempt.Simulation.Board;
            CommandLog log = m_session.Session.Attempt.Simulation.Commands.Log;
            string before = SnapshotBoard(board, log);

            // The write stays open over several frames and ticks; the board waits for it (AC-11).
            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
                m_session.TickIdle();
                Assert.That(SnapshotBoard(board, log), Is.EqualTo(before), "frame " + frame + " of the open write");
            }

            Assert.That(m_boosters.Store.ConsumeCount, Is.EqualTo(1));
            Assert.That(m_session.Session.PauseReason, Is.EqualTo(PauseReason.Saving));
            Assert.That(m_session.Session.Resume(), Is.False);
            Assert.That(m_coordinator.IsBusy.Value, Is.True);

            m_boosters.Store.Gate.SetResult(true);
            yield return BoosterFixture.WaitForSave(m_coordinator);

            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_boosters.Store.ConsumeCount, Is.EqualTo(1), "exactly one debit per use");
            Assert.That(SnapshotBoard(board, log), Is.EqualTo(before), "the commit itself applies nothing");
            m_session.TickIdle();
            Assert.That(board.GetStatus(0), Is.EqualTo(BlockStatus.Removed), "the first resumed command phase applies it");
        }

        [UnityTest]
        public IEnumerator HomeAfterACommittedUseKeepsItsChargeSpentAndItsDefinitionsUnchanged()
        {
            yield return CommitInTheBackgroundThenDiscard(goHome: true);
        }

        [UnityTest]
        public IEnumerator RestartAfterACommittedUseKeepsItsChargeSpentAndItsDefinitionsUnchanged()
        {
            yield return CommitInTheBackgroundThenDiscard(goHome: false);
        }

        [UnityTest]
        public IEnumerator TimeFreezeIsRefusedWhileFrozenAndSpendsNothing()
        {
            AttemptCountdown clock = new AttemptCountdown();
            Create(clock);
            m_session.PrepareAndPlay();
            m_boosters.Store.SetStock(BoosterId.TimeFreeze, 2);
            Assert.That(m_coordinator.Press(BoosterId.TimeFreeze), Is.EqualTo(BoosterPressResult.Submitted));
            yield return BoosterFixture.WaitForSave(m_coordinator);
            m_session.TickIdle();
            Assert.That(clock.IsFrozen, Is.True);
            Assert.That(m_coordinator.Press(BoosterId.Rocket), Is.EqualTo(BoosterPressResult.TargetingStarted));

            Assert.That(m_coordinator.Press(BoosterId.TimeFreeze), Is.EqualTo(BoosterPressResult.Refused));
            Assert.That(m_session.Targeting.Mode.Value, Is.EqualTo(BoosterId.None),
                "a refused Time Freeze still ends targeting first, without the Suspend of a submission");
            yield return null;

            Assert.That(m_boosters.Store.ConsumeCount, Is.EqualTo(1));
            Assert.That(m_boosters.Store.GetStock(BoosterId.TimeFreeze).Value, Is.EqualTo(1));
            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_session.Session.PauseReason, Is.EqualTo(PauseReason.None), "nothing paused for the refusal");
        }

        [UnityTest]
        public IEnumerator AFaultingDebitIsLoggedAndCountsAsFailed()
        {
            Create();
            m_session.PrepareAndPlay();
            m_boosters.Store.NextFault = new InvalidOperationException("store broke");
            LogAssert.Expect(LogType.Exception, new Regex("store broke"));
            m_coordinator.Press(BoosterId.Hammer);

            m_session.Tap(0f, 1f);
            yield return BoosterFixture.WaitForSave(m_coordinator);
            m_session.TickIdle();

            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_session.Session.Attempt.Simulation.Board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard));
        }

        [UnityTest]
        public IEnumerator DisposingTheCoordinatorStopsItsDebitFromReportingLater()
        {
            Create();
            m_session.PrepareAndPlay();
            m_boosters.Store.Gate = new TaskCompletionSource<bool>();
            m_coordinator.Press(BoosterId.Hammer);
            m_session.Tap(0f, 1f);
            yield return null;
            yield return null;

            m_coordinator.Dispose();
            m_boosters.Store.Gate.SetResult(true);
            yield return BoosterFixture.WaitForSave(m_coordinator);

            Assert.That(m_session.Session.IsBoosterWritePending, Is.True, "nothing reported after Dispose");
            Assert.That(m_coordinator.Press(BoosterId.Hammer), Is.EqualTo(BoosterPressResult.Refused));
        }

        // A Hammer use whose debit commits while the app is in the background, so the intent waits for an explicit
        // Resume; Home or Restart then discards it (B-13, AC-11). The charge stays spent without refund, replay or
        // reseed, and no definition asset of the catalog changes over the whole use.
        private IEnumerator CommitInTheBackgroundThenDiscard(bool goHome)
        {
            Create(resumesOnForeground: false);
            string[] definitions = SnapshotDefinitions();
            m_session.PrepareAndPlay();
            m_boosters.Store.Gate = new TaskCompletionSource<bool>();
            Assert.That(m_coordinator.Press(BoosterId.Hammer), Is.EqualTo(BoosterPressResult.TargetingStarted));
            m_session.Tap(0f, 1f);
            m_session.Session.NotifyBackground(true);
            m_boosters.Store.Gate.SetResult(true);
            yield return BoosterFixture.WaitForSave(m_coordinator);

            Assert.That(m_boosters.Store.GetStock(BoosterId.Hammer).Value, Is.Zero, "the debit committed in the background");
            Assert.That(m_session.Session.HasCommittedBooster, Is.True);
            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Paused), "a late commit never resumes");
            m_session.Session.NotifyBackground(false);

            if (goHome)
            {
                Assert.That(m_session.Session.GoHome(), Is.True);
                Assert.That(m_session.Session.TryPrepare(m_session.Level, out string error), Is.True, error);
                Assert.That(m_session.Session.Play(), Is.True);
            }
            else
            {
                Assert.That(m_session.Session.Restart(), Is.True);
            }

            m_session.TickIdle();
            m_session.TickIdle();

            Assert.That(m_session.Session.HasCommittedBooster, Is.False, "the committed intent was discarded");
            Assert.That(m_session.Session.Attempt.Simulation.Commands.Log.Count, Is.Zero, "and is never replayed");
            Assert.That(m_session.Session.Attempt.Simulation.Board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(m_boosters.Store.GetStock(BoosterId.Hammer).Value, Is.Zero, "no refund and no reseed");
            Assert.That(m_boosters.Store.ConsumeCount, Is.EqualTo(1), "no second debit");
            Assert.That(SnapshotDefinitions(), Is.EqualTo(definitions), "an attempt never mutates its definition assets");
        }

        private string[] SnapshotDefinitions()
        {
            BoosterDefinition[] all =
            {
                m_boosters.TimeFreeze, m_boosters.Rocket, m_boosters.Hammer, m_boosters.Vacuum, m_boosters.ExtraTime
            };
            string[] json = new string[all.Length];
            for (int index = 0; index < all.Length; index++)
            {
                json[index] = UnityEditor.EditorJsonUtility.ToJson(all[index]);
            }

            return json;
        }

        // Every block's status and origin, the occupancy grid and the command count, as one comparable string.
        private static string SnapshotBoard(BoardState board, CommandLog log)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            for (int block = 0; block < board.BlockCount; block++)
            {
                Cell origin = board.GetOrigin(block);
                text.Append(block).Append(':').Append(board.GetStatus(block)).Append('@')
                    .Append(origin.X).Append(',').Append(origin.Y).Append(';');
            }

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    text.Append(board.GetOccupant(new Cell(x, y))).Append(' ');
                }
            }

            return text.Append("log ").Append(log.Count).ToString();
        }

        private void Create(ICountdown clock = null, bool resumesOnForeground = true, string[] rows = null)
        {
            m_session = new SessionFixture(rows ?? s_board, new DoorSpec[0], resumesOnForeground, clock: clock,
                withTargeting: true);
            m_boosters = new BoosterFixture();
            m_coordinator = m_boosters.CreateCoordinator(m_session);
        }
    }
}
