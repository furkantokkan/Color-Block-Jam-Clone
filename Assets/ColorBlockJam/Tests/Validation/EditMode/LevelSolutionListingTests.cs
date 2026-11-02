using System.Collections.Generic;
using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Validation
{
    /// <summary>
    /// The solution listing Validate Level asks for (RunAndList): distinct exit orders with the check's own solution
    /// first, replay of every entry, determinism, the 20+ cap, the listing's own budget and cancel, a kept verdict
    /// listed alone, and a Run that never lists.
    /// </summary>
    public sealed class LevelSolutionListingTests
    {
        [Test]
        public void TheListingHasItsOwnBudget()
        {
            LevelCheckOptions options = new LevelCheckOptions();

            // DEC-175 raised DEC-170's 250,000 positions and 2 seconds with the check's budget.
            Assert.That(options.MaxListPositions, Is.EqualTo(1000000));
            Assert.That(options.MaxListSeconds, Is.EqualTo(10.0));
            Assert.That(LevelCheckOptions.DefaultMaxListPositions, Is.EqualTo(1000000));
            Assert.That(LevelCheckOptions.DefaultMaxListSeconds, Is.EqualTo(10.0));
            Assert.That(LevelCheckOptions.MaxListedSolutions, Is.EqualTo(20));
        }

        [Test]
        public void ThreeSinglesAtTheirGatesListAllSixExitOrdersWithTheCheckSolutionFirst()
        {
            LevelData level = ValidationFixtures.ThreeSinglesAtTheirGates();
            LevelCheckResult verdict = LevelSolvabilityCheck.Run(level);

            LevelCheckResult listed = CheckAndList(level);

            ValidationLevelFixture.AssertListedSolutions(level, verdict, listed, "three singles");
            Assert.That(listed.SolutionListState, Is.EqualTo(LevelSolutionListState.Complete));
            int[][] orders =
            {
                new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 },
                new[] { 2, 1, 0 }
            };
            Assert.That(listed.Solutions.Count, Is.EqualTo(orders.Length));
            for (int i = 0; i < orders.Length; i++)
            {
                Assert.That(listed.Solutions[i].ExitOrder, Is.EqualTo(orders[i]), "#" + (i + 1));
                Assert.That(listed.Solutions[i].MoveCount, Is.EqualTo(3), "#" + (i + 1));
            }

            Assert.That(listed.SolutionCountText, Is.EqualTo("6 solutions"));
            Assert.That(listed.SummaryText, Is.EqualTo("Solvable in 3 moves · 6 solutions"));
        }

        [Test]
        public void TwoOrdersOfDifferentLengthListTheShorterCheckSolutionFirstAndCountPlayerMoves()
        {
            LevelData level = ValidationFixtures.TwoOrdersOfDifferentLength();
            LevelCheckResult verdict = LevelSolvabilityCheck.Run(level);

            LevelCheckResult listed = CheckAndList(level);

            ValidationLevelFixture.AssertListedSolutions(level, verdict, listed, "two orders");
            Assert.That(listed.Text, Is.EqualTo("Solvable in 2 moves."));
            Assert.That(listed.SolutionListState, Is.EqualTo(LevelSolutionListState.Complete));
            Assert.That(listed.Solutions.Count, Is.EqualTo(2));

            LevelSolution first = listed.Solutions[0];
            Assert.That(first.ExitOrder, Is.EqualTo(new[] { 1, 0 }), "Orange first");
            Assert.That(first.Steps.Count, Is.EqualTo(4));
            Assert.That(first.MoveCount, Is.EqualTo(2));

            // Red first: Orange steps down, Red moves right and leaves, Orange comes back up twice and leaves.
            LevelSolution second = listed.Solutions[1];
            Assert.That(second.ExitOrder, Is.EqualTo(new[] { 0, 1 }), "Red first");
            Assert.That(second.Steps, Is.EqualTo(new[]
            {
                new LevelSolutionStep(1, 2, 1, 2, 0, false, BoardSide.Top),
                new LevelSolutionStep(0, 1, 1, 2, 1, false, BoardSide.Top),
                new LevelSolutionStep(0, 2, 1, 2, 1, true, BoardSide.Right),
                new LevelSolutionStep(1, 2, 0, 2, 1, false, BoardSide.Top),
                new LevelSolutionStep(1, 2, 1, 2, 2, false, BoardSide.Top),
                new LevelSolutionStep(1, 2, 2, 2, 2, true, BoardSide.Top)
            }));
            Assert.That(second.MoveCount, Is.EqualTo(3), "Orange, Red, then Orange again");
            Assert.That(listed.SummaryText, Is.EqualTo("Solvable in 2 moves · 2 solutions"));
        }

        [Test]
        public void AnIceBlockLeavesLastInEveryListedOrder()
        {
            LevelData level = ValidationFixtures.IceThawedByTwoExits();
            LevelCheckResult verdict = LevelSolvabilityCheck.Run(level);

            LevelCheckResult listed = CheckAndList(level);

            ValidationLevelFixture.AssertListedSolutions(level, verdict, listed, "ice");
            Assert.That(listed.SolutionListState, Is.EqualTo(LevelSolutionListState.Complete));
            Assert.That(listed.Solutions.Count, Is.EqualTo(2));
            Assert.That(listed.Solutions[0].ExitOrder, Is.EqualTo(new[] { 1, 2, 0 }));
            Assert.That(listed.Solutions[1].ExitOrder, Is.EqualTo(new[] { 2, 1, 0 }));
        }

        [Test]
        public void PlainExpansionListsWhatEagerExitsWouldSkip()
        {
            LevelData level = ValidationFixtures.TwoOrdersOfDifferentLength();
            LevelCheckResult verdict = LevelSolvabilityCheck.Run(level, new LevelCheckOptions { EagerExits = false }, null);

            LevelCheckResult listed = CheckAndList(level, new LevelCheckOptions { EagerExits = false });

            // Without eager exits Orange may wait at its gate while Red leaves, so Red first also takes four steps.
            ValidationLevelFixture.AssertListedSolutions(level, verdict, listed, "plain");
            Assert.That(listed.SolutionListState, Is.EqualTo(LevelSolutionListState.Complete));
            Assert.That(listed.Solutions.Count, Is.EqualTo(2));
            Assert.That(listed.Solutions[0].Steps.Count, Is.EqualTo(4));
            Assert.That(listed.Solutions[1].Steps.Count, Is.EqualTo(4));
        }

        [Test]
        public void MoreThanTwentyExitOrdersListTwentyAndReadTwentyPlus()
        {
            LevelData level = ValidationFixtures.FiveSinglesAtTheirGates();
            LevelCheckResult verdict = LevelSolvabilityCheck.Run(level);

            LevelCheckResult listed = CheckAndList(level);

            ValidationLevelFixture.AssertListedSolutions(level, verdict, listed, "five singles");
            Assert.That(listed.SolutionListState, Is.EqualTo(LevelSolutionListState.More));
            Assert.That(listed.Solutions.Count, Is.EqualTo(LevelCheckOptions.MaxListedSolutions));
            Assert.That(listed.SolutionCountText, Is.EqualTo("20+ solutions"));
            Assert.That(listed.SummaryText, Is.EqualTo("Solvable in 5 moves · 20+ solutions"));

            // Every order needs five exits, so the orders arrive in the fixed block order: lexicographically.
            Assert.That(listed.Solutions[0].ExitOrder, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            Assert.That(listed.Solutions[1].ExitOrder, Is.EqualTo(new[] { 0, 1, 2, 4, 3 }));
            Assert.That(listed.Solutions[19].ExitOrder, Is.EqualTo(new[] { 0, 4, 1, 3, 2 }));
        }

        [Test]
        public void TheSameLevelListsTheSameSolutionsTwice()
        {
            LevelData[] levels = CreateListable();
            for (int i = 0; i < levels.Length; i++)
            {
                LevelCheckResult first = CheckAndList(levels[i]);
                LevelCheckResult second = CheckAndList(levels[i]);

                Assert.That(second.Text, Is.EqualTo(first.Text), "#" + i);
                ValidationLevelFixture.AssertSameList(first, second, "#" + i);
            }
        }

        // RunAndList equals Run followed by ListSolutions.
        [Test]
        public void ListingAKeptVerdictRunsOnlyTheListingAndGivesTheSameList()
        {
            LevelData[] levels = CreateListable();
            for (int i = 0; i < levels.Length; i++)
            {
                LevelCheckResult combined = CheckAndList(levels[i]);
                LevelCheckResult kept = LevelSolvabilityCheck.Run(levels[i]);

                LevelCheckResult listed = LevelSolvabilityCheck.ListSolutions(levels[i], kept, null, null);

                ValidationLevelFixture.AssertListedSolutions(levels[i], kept, listed, "#" + i);
                ValidationLevelFixture.AssertSameList(combined, listed, "#" + i);
                Assert.That(listed.VisitedPositions, Is.EqualTo(kept.VisitedPositions), "the check did not run again");
                Assert.That(kept.Solutions, Is.Empty, "the kept verdict itself is unchanged");
                Assert.That(kept.SolutionListState, Is.EqualTo(LevelSolutionListState.NotListed));
            }
        }

        [Test]
        public void RunListsNothingAndRunAndListKeepsItsVerdict()
        {
            List<LevelData> levels = new List<LevelData>(ValidationFixtures.CreateSearchable());
            levels.AddRange(CreateListable());
            for (int i = 0; i < levels.Count; i++)
            {
                LevelCheckResult defaults = LevelSolvabilityCheck.Run(levels[i]);
                LevelCheckResult off = LevelSolvabilityCheck.Run(levels[i], new LevelCheckOptions(), null);
                LevelCheckResult listed = CheckAndList(levels[i]);

                AssertNotListed(defaults, "#" + i);
                AssertNotListed(off, "#" + i);
                Assert.That(listed.Status, Is.EqualTo(defaults.Status), "#" + i);
                Assert.That(listed.Text, Is.EqualTo(defaults.Text), "#" + i);
                Assert.That(listed.Steps, Is.EqualTo(defaults.Steps), "#" + i);
                Assert.That(listed.MoveCount, Is.EqualTo(defaults.MoveCount), "#" + i);
                Assert.That(listed.LimitHit, Is.EqualTo(defaults.LimitHit), "#" + i);
                Assert.That(listed.VisitedPositions, Is.EqualTo(defaults.VisitedPositions), "#" + i);
            }
        }

        [Test]
        public void UnsolvableNotConfirmedAndNotCheckedResultsListNothing()
        {
            LevelCheckOptions tinyCheck = new LevelCheckOptions { MaxPositions = 2 };
            LevelData[] levels =
            {
                ValidationFixtures.MutualBlockInCorridor(), ValidationFixtures.BlockWithoutGateOfItsColour(),
                ValidationFixtures.TwoBlocksCrossing(), ValidationFixtures.SeventeenBlocks(),
                ValidationFixtures.TwoBlocksCrossing()
            };
            LevelCheckResult[] results =
            {
                CheckAndList(levels[0]),
                CheckAndList(levels[1]),
                CheckAndList(levels[2], tinyCheck),
                CheckAndList(levels[3]),
                // The check's own progress cancels at its first report.
                LevelSolvabilityCheck.RunAndList(levels[4], null, new ValidationLevelFixture.ScriptedProgress(1), null)
            };
            LevelCheckStatus[] statuses =
            {
                LevelCheckStatus.Unsolvable, LevelCheckStatus.Unsolvable, LevelCheckStatus.Unknown,
                LevelCheckStatus.NotChecked, LevelCheckStatus.NotChecked
            };

            for (int i = 0; i < results.Length; i++)
            {
                Assert.That(results[i].Status, Is.EqualTo(statuses[i]), "#" + i + ": " + results[i].Text);
                AssertNotListed(results[i], "#" + i);
                Assert.That(LevelSolvabilityCheck.ListSolutions(levels[i], results[i], null, null), Is.SameAs(results[i]),
                    "#" + i + ": a verdict that is not Solvable comes back unchanged");
            }
        }

        [Test]
        public void ATinyListingPositionBudgetKeepsTheVerdictAndReadsAtLeast()
        {
            LevelData level = ValidationFixtures.FiveSinglesAtTheirGates();
            LevelCheckResult verdict = LevelSolvabilityCheck.Run(level);
            LevelCheckOptions options = new LevelCheckOptions { MaxListPositions = 10 };

            LevelCheckResult listed = CheckAndList(level, options);

            // The start, its five exits and four of the second exits fill the budget before any board is cleared.
            ValidationLevelFixture.AssertListedSolutions(level, verdict, listed, "tiny budget");
            Assert.That(listed.SolutionListState, Is.EqualTo(LevelSolutionListState.Partial));
            Assert.That(listed.Solutions.Count, Is.EqualTo(1));
            Assert.That(listed.SolutionCountText, Is.EqualTo("at least 1 solution"));
            Assert.That(listed.SummaryText, Is.EqualTo("Solvable in 5 moves · at least 1 solution"));
        }

        [Test]
        public void AZeroListingTimeBudgetKeepsTheVerdictAndOnlyTheCheckSolution()
        {
            LevelData level = ValidationFixtures.ThreeSinglesAtTheirGates();
            LevelCheckResult verdict = LevelSolvabilityCheck.Run(level);
            LevelCheckOptions options = new LevelCheckOptions { MaxListSeconds = 0 };

            LevelCheckResult listed = CheckAndList(level, options);

            ValidationLevelFixture.AssertListedSolutions(level, verdict, listed, "zero seconds");
            Assert.That(listed.SolutionListState, Is.EqualTo(LevelSolutionListState.Partial));
            Assert.That(listed.Solutions.Count, Is.EqualTo(1));
        }

        [Test]
        public void EveryListingStoppedByItsPositionBudgetKeepsAPrefixOfTheFullList()
        {
            LevelData[] levels =
            {
                ValidationFixtures.TwoOrdersOfDifferentLength(), ValidationFixtures.ThreeSinglesAtTheirGates(),
                ValidationFixtures.IceThawedByTwoExits()
            };
            bool partialWithTwoEntries = false;
            for (int i = 0; i < levels.Length; i++)
            {
                LevelCheckResult verdict = LevelSolvabilityCheck.Run(levels[i]);
                LevelCheckResult full = CheckAndList(levels[i]);
                Assert.That(full.SolutionListState, Is.EqualTo(LevelSolutionListState.Complete), "#" + i);

                // Owner: this test. Stops: at the first budget large enough to finish, well below the guard.
                for (int budget = 0; ; budget++)
                {
                    Assert.That(budget, Is.LessThan(100000), "#" + i + ": the listing never finished");
                    LevelCheckOptions options = new LevelCheckOptions { MaxListPositions = budget };
                    LevelCheckResult partial = CheckAndList(levels[i], options);
                    string label = "#" + i + " budget " + budget;
                    ValidationLevelFixture.AssertListedSolutions(levels[i], verdict, partial, label);
                    if (partial.SolutionListState == LevelSolutionListState.Complete)
                    {
                        ValidationLevelFixture.AssertSameList(full, partial, label);
                        break;
                    }

                    Assert.That(partial.SolutionListState, Is.EqualTo(LevelSolutionListState.Partial), label);
                    Assert.That(partial.Solutions.Count, Is.LessThanOrEqualTo(full.Solutions.Count), label);
                    for (int e = 0; e < partial.Solutions.Count; e++)
                    {
                        Assert.That(partial.Solutions[e].Steps, Is.EqualTo(full.Solutions[e].Steps), label + " #" + (e + 1));
                    }

                    partialWithTwoEntries |= partial.Solutions.Count >= 2;
                }
            }

            Assert.That(partialWithTwoEntries, Is.True, "some budget stops after an entry beyond the check's own");
        }

        [Test]
        public void ACancelDuringTheListingKeepsTheVerdictAndTheEntriesFoundSoFar()
        {
            LevelData level = ValidationFixtures.TwoOrdersOfDifferentLength();
            LevelCheckResult verdict = LevelSolvabilityCheck.Run(level);
            ValidationLevelFixture.ScriptedProgress progress = new ValidationLevelFixture.ScriptedProgress(1);

            LevelCheckResult listed = LevelSolvabilityCheck.ListSolutions(level, verdict, null, progress);

            ValidationLevelFixture.AssertListedSolutions(level, verdict, listed, "cancel");
            Assert.That(listed.SolutionListState, Is.EqualTo(LevelSolutionListState.Partial));
            Assert.That(listed.Solutions.Count, Is.EqualTo(1));
            Assert.That(listed.SolutionCountText, Is.EqualTo("at least 1 solution"));
            Assert.That(progress.ReportCount, Is.EqualTo(1));
        }

        [Test]
        public void ACancelAfterTheCheckStopsOnlyTheListing()
        {
            LevelData level = ValidationFixtures.TwoOrdersOfDifferentLength();
            LevelCheckResult verdict = LevelSolvabilityCheck.Run(level);

            // The check reports once at its first poll on this small level and never cancels; the listing's first
            // report cancels.
            ValidationLevelFixture.ScriptedProgress checkProgress = new ValidationLevelFixture.ScriptedProgress(0);
            ValidationLevelFixture.ScriptedProgress listProgress = new ValidationLevelFixture.ScriptedProgress(1);
            LevelCheckResult listed = LevelSolvabilityCheck.RunAndList(level, null, checkProgress, listProgress);

            ValidationLevelFixture.AssertListedSolutions(level, verdict, listed, "cancel after the check");
            Assert.That(listed.SolutionListState, Is.EqualTo(LevelSolutionListState.Partial));
            Assert.That(listed.Solutions.Count, Is.EqualTo(1));
            Assert.That(checkProgress.ReportCount, Is.EqualTo(1), "the check reports through its own progress");
            Assert.That(listProgress.ReportCount, Is.EqualTo(1), "the listing reports through its own progress");
        }

        [Test]
        public void ListingAVerdictOfAnotherLevelThrows()
        {
            LevelCheckResult threeBlocks = LevelSolvabilityCheck.Run(ValidationFixtures.ThreeSinglesAtTheirGates());

            Assert.That(() => LevelSolvabilityCheck.ListSolutions(ValidationFixtures.TwoOrdersOfDifferentLength(),
                threeBlocks, null, null), Throws.ArgumentException);
            Assert.That(() => LevelSolvabilityCheck.ListSolutions(ValidationFixtures.SeventeenBlocks(), threeBlocks, null,
                null), Throws.ArgumentException);
            Assert.That(() => LevelSolvabilityCheck.ListSolutions(null, threeBlocks, null, null),
                Throws.ArgumentNullException);
            Assert.That(() => LevelSolvabilityCheck.ListSolutions(ValidationFixtures.ThreeSinglesAtTheirGates(), null,
                null, null), Throws.ArgumentNullException);
        }

        [Test]
        public void SolutionCountsReadInDesignerWords()
        {
            Assert.That(LevelDesignerText.FormatSolutionCount(1, LevelSolutionListState.Complete), Is.EqualTo("1 solution"));
            Assert.That(LevelDesignerText.FormatSolutionCount(12, LevelSolutionListState.Complete),
                Is.EqualTo("12 solutions"));
            Assert.That(LevelDesignerText.FormatSolutionCount(20, LevelSolutionListState.More), Is.EqualTo("20+ solutions"));
            Assert.That(LevelDesignerText.FormatSolutionCount(1, LevelSolutionListState.Partial),
                Is.EqualTo("at least 1 solution"));
            Assert.That(LevelDesignerText.FormatSolutionCount(3, LevelSolutionListState.Partial),
                Is.EqualTo("at least 3 solutions"));
            Assert.That(LevelDesignerText.FormatSolutionCount(0, LevelSolutionListState.NotListed), Is.Empty);
            Assert.That(LevelDesignerText.FormatSummary("Solvable in 5 moves.", "12 solutions"),
                Is.EqualTo("Solvable in 5 moves · 12 solutions"));
        }

        // Validate Level's call: the check, then the listing on its tables, with silent progress.
        private static LevelCheckResult CheckAndList(LevelData level, LevelCheckOptions options = null)
        {
            return LevelSolvabilityCheck.RunAndList(level, options, null, null);
        }

        // The Solvable fixtures whose listings finish well inside the default budget.
        private static LevelData[] CreateListable()
        {
            return new[]
            {
                ValidationFixtures.SingleBlockToTopGate(), ValidationFixtures.TwoBlocksCrossing(),
                ValidationFixtures.IceThawedByOtherExit(), ValidationFixtures.NotchBlockedExit(),
                ValidationFixtures.ArrowBlockingColumn("Horizontal"), ValidationFixtures.ThreeSinglesAtTheirGates(),
                ValidationFixtures.FiveSinglesAtTheirGates(), ValidationFixtures.TwoOrdersOfDifferentLength(),
                ValidationFixtures.IceThawedByTwoExits()
            };
        }

        private static void AssertNotListed(LevelCheckResult result, string label)
        {
            Assert.That(result.Solutions, Is.Empty, label);
            Assert.That(result.SolutionListState, Is.EqualTo(LevelSolutionListState.NotListed), label);
            Assert.That(result.SolutionCountText, Is.Empty, label);
            Assert.That(result.SummaryText, Is.EqualTo(result.Text), label);
        }
    }
}
