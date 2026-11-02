using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Validation
{
    /// <summary>
    /// AC-2: every shipped level is Solvable and its found solution replays through the Domain's command
    /// executor with the level's Ice and Arrow hooks until the board is cleared. The check never changes the level.
    /// </summary>
    public sealed class ShippedLevelSolvabilityTests
    {
        [Test]
        public void EveryShippedLevelIsSolvable()
        {
            LevelData[] levels = ValidationLevelFixture.LoadShippedLevels();
            Assert.That(levels.Length, Is.EqualTo(5));
            for (int i = 0; i < levels.Length; i++)
            {
                LevelCheckResult result = LevelSolvabilityCheck.Run(levels[i]);

                Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.Solvable), levels[i].Id + ": " + result.Text);
                Assert.That(result.MoveCount, Is.GreaterThan(0), levels[i].Id);
                string unit = result.MoveCount == 1 ? " move." : " moves.";
                Assert.That(result.Text, Is.EqualTo("Solvable in " + result.MoveCount + unit), levels[i].Id);
                Assert.That(result.LimitHit, Is.EqualTo(LevelCheckLimit.None), levels[i].Id);
            }
        }

        [Test]
        public void EveryShippedSolutionReplaysAsPlayerMovesAndClearsTheBoard()
        {
            LevelData[] levels = ValidationLevelFixture.LoadShippedLevels();
            for (int i = 0; i < levels.Length; i++)
            {
                LevelCheckResult result = LevelSolvabilityCheck.Run(levels[i]);
                ValidationLevelFixture.AssertReplaysAsMoves(levels[i], result.Steps, result.MoveCount, levels[i].Id);
            }
        }

        [Test]
        public void EveryShippedSolutionReplaysAsOneCellCommandsAndClearsTheBoard()
        {
            LevelData[] levels = ValidationLevelFixture.LoadShippedLevels();
            for (int i = 0; i < levels.Length; i++)
            {
                LevelCheckResult result = LevelSolvabilityCheck.Run(levels[i]);
                ValidationLevelFixture.AssertReplaysAsSteps(levels[i], result.Steps, levels[i].Id);
            }
        }

        [Test]
        public void TheCheckLeavesTheLevelUnchanged()
        {
            LevelData[] levels = ValidationLevelFixture.LoadShippedLevels();
            Assert.That(LevelDatabaseJson.TryWrite(levels, out string before, out string error), Is.True, error);

            for (int i = 0; i < levels.Length; i++)
            {
                LevelSolvabilityCheck.Run(levels[i]);
            }

            Assert.That(LevelDatabaseJson.TryWrite(levels, out string after, out error), Is.True, error);
            Assert.That(after, Is.EqualTo(before));
        }
    }
}
