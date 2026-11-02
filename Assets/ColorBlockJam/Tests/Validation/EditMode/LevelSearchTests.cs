using ColorBlockJam.Domain.Board;
using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Validation
{
    /// <summary>
    /// AC-4, AC-5, AC-9 and AC-11 on the search: exhausted deadlocks, budget hits, determinism, eager exits
    /// against plain breadth-first search, progress and cancel.
    /// </summary>
    public sealed class LevelSearchTests
    {
        [Test]
        public void TwoBlocksBlockingEachOthersOnlyExitCannotBeSolvedAfterAnExhaustedSearch()
        {
            LevelData level = ValidationFixtures.MutualBlockInCorridor();
            LevelCheckResult result = LevelSolvabilityCheck.Run(level);

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Unsolvable));
            // DEC-176: the exhausted search names the blocks that never left.
            Assert.That(result.Text, Is.EqualTo(NeverLeave(level, 0, 1)));
            Assert.That(result.VisitedPositions, Is.GreaterThan(1));
        }

        [Test]
        public void TwoIceBlocksWaitingOnEachOtherCannotBeSolvedAfterAnExhaustedSearch()
        {
            LevelData level = ValidationFixtures.TwoIceBlocksWaitingOnEachOther();
            LevelCheckResult result = LevelSolvabilityCheck.Run(level);

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Unsolvable));
            Assert.That(result.Text, Is.EqualTo(NeverLeave(level, 0, 1)));
            Assert.That(result.VisitedPositions, Is.EqualTo(1));
        }

        [Test]
        public void AnExhaustedSearchNamesOnlyTheBlocksThatNeverLeave()
        {
            // DEC-176: the Yellow block leaves at once; the two Ice blocks then stay frozen for good.
            LevelData level = ValidationFixtures.TwoIceBlocksBesideAFreeBlock();
            LevelCheckResult result = LevelSolvabilityCheck.Run(level);

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Unsolvable), result.Text);
            Assert.That(result.Text, Is.EqualTo(NeverLeave(level, 0, 1)));
            Assert.That(result.Text, Does.Not.Contain(LevelDesignerText.DescribeBlock(level, 2)));
        }

        [Test]
        public void BlocksWedgedTogetherAreRefusedBeforeTheSearch()
        {
            // DEC-176 PRE-3: each block alone reaches its gate, but together none can move.
            LevelData level = ValidationFixtures.ThreeBlocksWedgedInARow();
            LevelCheckResult result = LevelSolvabilityCheck.Run(level);

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Unsolvable), result.Text);
            Assert.That(result.Text, Is.EqualTo("Cannot be solved: "
                + LevelDesignerText.DescribeBlocks(level, 0b111, LevelDesignerText.DescribeBlock)
                + " are wedged against each other and can never move."));
            Assert.That(result.VisitedPositions, Is.Zero, "No search ran.");
        }

        [Test]
        public void AnArrowBlockWedgedByItsAxisIsRefusedBeforeTheSearch()
        {
            LevelData level = ValidationFixtures.ArrowWedgedInARow();
            LevelCheckResult result = LevelSolvabilityCheck.Run(level);

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Unsolvable));
            Assert.That(result.Text, Is.EqualTo("Cannot be solved: " + LevelDesignerText.DescribeBlock(level, 0)
                + " is a Vertical Arrow block; its axis never lines it up to leave through a Red gate."));
            Assert.That(result.VisitedPositions, Is.Zero, "No search ran.");
        }

        [Test]
        public void ABlockListNamesThreeBlocksAndCountsTheRest()
        {
            LevelData level = ValidationFixtures.FiveSinglesInARow();
            string[] names = new string[5];
            for (int block = 0; block < names.Length; block++)
            {
                names[block] = LevelDesignerText.DescribeBlock(level, block);
            }

            Assert.That(LevelDesignerText.DescribeBlocks(level, 0b00100, LevelDesignerText.DescribeBlock), Is.EqualTo(names[2]));
            Assert.That(LevelDesignerText.DescribeBlocks(level, 0b01001, LevelDesignerText.DescribeBlock),
                Is.EqualTo(names[0] + " and " + names[3]));
            Assert.That(LevelDesignerText.DescribeBlocks(level, 0b10110, LevelDesignerText.DescribeBlock),
                Is.EqualTo(names[1] + ", " + names[2] + " and " + names[4]));
            Assert.That(LevelDesignerText.DescribeBlocks(level, 0b11111, LevelDesignerText.DescribeBlock),
                Is.EqualTo(names[0] + ", " + names[1] + ", " + names[2] + " and 2 other blocks"));
        }

        [Test]
        public void AnIceBlockThawsWhenAnotherBlockLeavesFirst()
        {
            LevelCheckResult result = LevelSolvabilityCheck.Run(ValidationFixtures.IceThawedByOtherExit());

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Solvable), result.Text);
            Assert.That(result.Steps[0].BlockIndex, Is.EqualTo(1), "the Orange block leaves before the frozen Red one");
        }

        [Test]
        public void AnExitWhosePathHoldsAnotherBlockWaitsUntilThatBlockMoves()
        {
            LevelData level = ValidationFixtures.NotchBlockedExit();
            Assert.That(LevelSolverModelBuilder.TryBuild(level, out LevelModel model, out string error), Is.True, error);
            LevelSolverTables tables = LevelSolverTables.Create(model, LevelSolvabilityCheck.ReadIceCounts(level),
                LevelSolvabilityCheck.ReadArrowAxes(level));
            int slot = tables.GetAuthoredSlot(0);

            // Cell index y * 3 + x: the Red L's top exit sweeps (1,1) and (1,2), the column above its notch.
            Assert.That(tables.ExitFits(0, slot, Side.Top), Is.True);
            Assert.That(tables.GetSwept(0, slot, Side.Top), Is.EqualTo(CellMask.Empty.With(4).With(7)));

            LevelCheckResult result = LevelSolvabilityCheck.Run(level);

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Solvable), result.Text);
            Assert.That(result.Steps[0].BlockIndex, Is.EqualTo(1), "the Orange block clears the Red block's path first");
            Assert.That(result.Steps[0].IsExit, Is.False);
        }

        [Test]
        public void ConsecutiveStepsOfOneBlockCountAsOneMove()
        {
            LevelCheckResult result = LevelSolvabilityCheck.Run(ValidationFixtures.SingleBlockToTopGate());

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Solvable));
            Assert.That(result.Steps.Count, Is.EqualTo(3), "two slides and the exit");
            Assert.That(result.MoveCount, Is.EqualTo(1));
            Assert.That(result.Text, Is.EqualTo("Solvable in 1 move."));
            Assert.That(result.Steps[2].IsExit, Is.True);
            Assert.That(result.Steps[2].ExitSide, Is.EqualTo(BoardSide.Top));
        }

        [Test]
        public void ATinyPositionBudgetOnASolvableLevelIsNotConfirmedNamingThePositionLimit()
        {
            LevelCheckOptions options = new LevelCheckOptions { MaxPositions = 2 };

            LevelCheckResult result = LevelSolvabilityCheck.Run(ValidationFixtures.TwoBlocksCrossing(), options, null);

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Unknown));
            Assert.That(result.LimitHit, Is.EqualTo(LevelCheckLimit.Positions));
            Assert.That(result.Text, Is.EqualTo("Not confirmed: the check stopped after 2 positions."));
            Assert.That(result.VisitedPositions, Is.EqualTo(2));
        }

        [Test]
        public void AZeroTimeBudgetOnASolvableLevelIsNotConfirmedNamingTheTimeLimit()
        {
            LevelCheckOptions options = new LevelCheckOptions { MaxSeconds = 0 };

            LevelCheckResult result = LevelSolvabilityCheck.Run(ValidationFixtures.TwoBlocksCrossing(), options, null);

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Unknown));
            Assert.That(result.LimitHit, Is.EqualTo(LevelCheckLimit.Time));
            Assert.That(result.Text, Is.EqualTo("Not confirmed: the check stopped after 0 seconds."));
        }

        // The exhausted search's sentence for the blocks that never leave (DEC-176).
        internal static string NeverLeave(LevelData level, params int[] blocks)
        {
            int mask = 0;
            foreach (int block in blocks)
            {
                mask |= 1 << block;
            }

            return "Cannot be solved: " + LevelDesignerText.DescribeBlocks(level, mask, LevelDesignerText.DescribeBlock)
                + " can never leave.";
        }

        [Test]
        public void TheDefaultBudgetTextNamesTenMillionPositionsAndFifteenSeconds()
        {
            // DEC-175 raised DEC-144's 250,000 positions and 3 seconds once Validate Level ran off the main thread.
            Assert.That(LevelDesignerText.FormatPositions(LevelCheckOptions.DefaultMaxPositions), Is.EqualTo("10,000,000 positions"));
            Assert.That(LevelDesignerText.FormatSeconds(LevelCheckOptions.DefaultMaxSeconds), Is.EqualTo("15 seconds"));
        }

        [Test]
        public void TheLevelFiveDraftWithASixthBlockIsConfirmedWithinTheDefaultBudget()
        {
            // The designer's draft read "Not confirmed: the check stopped after 250,000 positions." under DEC-144.
            LevelCheckResult result = LevelSolvabilityCheck.Run(ValidationFixtures.LevelFiveWithSixthBlock());

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Solvable), result.Text);
            Assert.That(result.VisitedPositions, Is.GreaterThan(250000), "The old budget could not confirm it.");
            Assert.That(result.VisitedPositions, Is.LessThanOrEqualTo(LevelCheckOptions.DefaultMaxPositions));
            TestContext.WriteLine($"{result.Text} {result.VisitedPositions} positions in {result.ElapsedSeconds:0.00} s.");
        }

        [Test]
        public void TheSameLevelGivesTheSameStatusAndStepsTwice()
        {
            LevelData[] levels = ValidationLevelFixture.LoadShippedLevels();
            LevelData[] fixtures = ValidationFixtures.CreateSearchable();
            LevelData[] all = new LevelData[levels.Length + fixtures.Length];
            levels.CopyTo(all, 0);
            fixtures.CopyTo(all, levels.Length);
            for (int i = 0; i < all.Length; i++)
            {
                LevelCheckResult first = LevelSolvabilityCheck.Run(all[i]);
                LevelCheckResult second = LevelSolvabilityCheck.Run(all[i]);

                Assert.That(second.Status, Is.EqualTo(first.Status), all[i].Id + " " + i);
                Assert.That(second.Text, Is.EqualTo(first.Text), all[i].Id + " " + i);
                Assert.That(second.Steps, Is.EqualTo(first.Steps), all[i].Id + " " + i);
            }
        }

        [Test]
        public void EagerExitSearchAndPlainBreadthFirstSearchAgreeOnEveryStatus()
        {
            LevelData[] levels = ValidationLevelFixture.LoadShippedLevels();
            LevelData[] fixtures = ValidationFixtures.CreateSearchable();
            LevelData[] all = new LevelData[levels.Length + fixtures.Length];
            levels.CopyTo(all, 0);
            fixtures.CopyTo(all, levels.Length);
            LevelCheckOptions plain = new LevelCheckOptions { EagerExits = false };
            for (int i = 0; i < all.Length; i++)
            {
                LevelCheckResult eager = LevelSolvabilityCheck.Run(all[i]);
                LevelCheckResult oracle = LevelSolvabilityCheck.Run(all[i], plain, null);

                Assert.That(eager.Status, Is.EqualTo(oracle.Status), all[i].Id + " " + i);
                Assert.That(eager.Status, Is.Not.EqualTo(LevelCheckStatus.Unknown), all[i].Id + " " + i);
                if (eager.Status == LevelCheckStatus.Solvable)
                {
                    Assert.That(eager.Steps.Count, Is.EqualTo(oracle.Steps.Count),
                        all[i].Id + " " + i + ": an eager exit never lengthens the shortest solution");
                }
            }
        }

        [Test]
        public void ACancelFromTheProgressReceiverReturnsNotCheckedCancelled()
        {
            ValidationLevelFixture.ScriptedProgress progress = new ValidationLevelFixture.ScriptedProgress(cancelFromReport: 1);

            LevelCheckResult result = LevelSolvabilityCheck.Run(ValidationFixtures.TwoBlocksCrossing(), null, progress);

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.NotChecked));
            Assert.That(result.Text, Is.EqualTo("Not checked: the check was cancelled."));
            Assert.That(progress.ReportCount, Is.GreaterThan(0));
        }

        [Test]
        public void TheSearchReportsItsPositionCountToTheProgressReceiver()
        {
            ValidationLevelFixture.ScriptedProgress progress = new ValidationLevelFixture.ScriptedProgress(cancelFromReport: 0);

            LevelCheckResult result = LevelSolvabilityCheck.Run(ValidationFixtures.TwoBlocksCrossing(), null, progress);

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Solvable));
            Assert.That(progress.ReportCount, Is.GreaterThan(0));
            Assert.That(progress.LastPositions, Is.GreaterThan(0));
        }
    }
}
