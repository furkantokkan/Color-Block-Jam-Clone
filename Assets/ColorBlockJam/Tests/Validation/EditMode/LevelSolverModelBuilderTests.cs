using System.Collections.Generic;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Validation
{
    /// <summary>
    /// AC-10: the solver's LevelData to LevelModel builder gives the floor, doors and blocks that Gameplay's
    /// AttemptLevelConverter gives, for the shipped levels and the test fixtures.
    /// </summary>
    public sealed class LevelSolverModelBuilderTests
    {
        [Test]
        public void TheBuilderMatchesTheAttemptConverterOnShippedLevelsAndFixtures()
        {
            List<LevelData> levels = new List<LevelData>(ValidationLevelFixture.LoadShippedLevels());
            levels.AddRange(ValidationFixtures.CreateSearchable());
            levels.Add(ValidationFixtures.IceCountAboveOtherBlocks());
            levels.Add(ValidationFixtures.BlockWiderThanItsGate());
            for (int i = 0; i < levels.Count; i++)
            {
                LevelData level = levels[i];
                string label = level.Id + " #" + i;
                Assert.That(BoardLayout.TryCreate(level, out BoardLayout layout, out string error), Is.True, label + error);
                Assert.That(AttemptLevelConverter.TryConvert(level, layout, out AttemptLevel attempt, out error),
                    Is.True, label + error);
                Assert.That(LevelSolverModelBuilder.TryBuild(level, out LevelModel built, out error), Is.True, label + error);

                AssertSameModel(attempt.Model, built, label);
            }
        }

        [Test]
        public void TheBuilderRefusesAnInvalidLevelInDesignerWords()
        {
            LevelData level = new LevelData("level-bad", "Bad", 4, 4, 120, new LevelDoorData[0], new LevelBlockData[0]);

            Assert.That(LevelSolverModelBuilder.TryBuild(level, out LevelModel model, out string error), Is.False);
            Assert.That(model, Is.Null);
            Assert.That(error, Is.EqualTo("the level has errors; fix them in Issues first"));
        }

        private static void AssertSameModel(LevelModel expected, LevelModel actual, string label)
        {
            Assert.That(actual.Width, Is.EqualTo(expected.Width), label);
            Assert.That(actual.Height, Is.EqualTo(expected.Height), label);
            Assert.That(actual.TimerSeconds, Is.EqualTo(expected.TimerSeconds), label);
            for (int y = -1; y <= expected.Height; y++)
            {
                for (int x = -1; x <= expected.Width; x++)
                {
                    Assert.That(actual.IsFloor(x, y), Is.EqualTo(expected.IsFloor(x, y)), label + " floor " + x + "," + y);
                }
            }

            Assert.That(actual.DoorCount, Is.EqualTo(expected.DoorCount), label);
            for (int d = 0; d < expected.DoorCount; d++)
            {
                DoorDefinition want = expected.GetDoor(d);
                DoorDefinition got = actual.GetDoor(d);
                Assert.That(got.Index, Is.EqualTo(want.Index), label);
                Assert.That(got.Side, Is.EqualTo(want.Side), label);
                Assert.That(got.Start, Is.EqualTo(want.Start), label);
                Assert.That(got.Length, Is.EqualTo(want.Length), label);
                Assert.That(got.Color, Is.EqualTo(want.Color), label);
            }

            Assert.That(actual.BlockCount, Is.EqualTo(expected.BlockCount), label);
            for (int b = 0; b < expected.BlockCount; b++)
            {
                BlockDefinition want = expected.GetBlock(b);
                BlockDefinition got = actual.GetBlock(b);
                Assert.That(got.Index, Is.EqualTo(want.Index), label);
                Assert.That(got.Color, Is.EqualTo(want.Color), label);
                Assert.That(got.Origin, Is.EqualTo(want.Origin), label);
                Assert.That(got.CellCount, Is.EqualTo(want.CellCount), label);
                for (int c = 0; c < want.CellCount; c++)
                {
                    Assert.That(got.GetCell(c), Is.EqualTo(want.GetCell(c)), label + " block " + b + " cell " + c);
                }
            }
        }
    }
}
