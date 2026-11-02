using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Validation
{
    /// <summary>
    /// AC-6 of the validation panel: every shipped level lists at least its check's own solution first, with the
    /// verdict's move count, and every listed entry replays through the Domain with the level's hooks and clears the
    /// board. The listing never changes a level.
    /// </summary>
    public sealed class ShippedLevelSolutionListingTests
    {
        [Test]
        public void EveryShippedLevelListsItsCheckSolutionFirstAndEveryEntryReplays()
        {
            LevelData[] levels = ValidationLevelFixture.LoadShippedLevels();
            // ShippedLevelSolvabilityTests pins how many levels ship.
            Assert.That(levels, Is.Not.Empty);
            for (int i = 0; i < levels.Length; i++)
            {
                LevelCheckResult verdict = LevelSolvabilityCheck.Run(levels[i]);

                LevelCheckResult listed = LevelSolvabilityCheck.RunAndList(levels[i], null, null, null);

                ValidationLevelFixture.AssertListedSolutions(levels[i], verdict, listed, levels[i].Id);
                Assert.That(listed.Solutions[0].MoveCount, Is.LessThanOrEqualTo(verdict.MoveCount), levels[i].Id);
                // DEC-181: the summary names entry #1's moves, the fewest listed.
                int fewest = listed.Solutions[0].MoveCount;
                Assert.That(listed.SummaryText,
                    Does.StartWith("Solvable in " + fewest + (fewest == 1 ? " move · " : " moves · ")), levels[i].Id);
            }
        }

        [Test]
        public void TheListingLeavesTheShippedLevelsUnchanged()
        {
            LevelData[] levels = ValidationLevelFixture.LoadShippedLevels();
            Assert.That(LevelDatabaseJson.TryWrite(levels, out string before, out string error), Is.True, error);

            for (int i = 0; i < levels.Length; i++)
            {
                LevelSolvabilityCheck.RunAndList(levels[i], null, null, null);
            }

            Assert.That(LevelDatabaseJson.TryWrite(levels, out string after, out error), Is.True, error);
            Assert.That(after, Is.EqualTo(before));
        }
    }
}
