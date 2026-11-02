using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Proves the Level Editor's Create Level seed uses colour ids the board layout accepts.
    /// </summary>
    public sealed class LevelCreateColourTests
    {
        [Test]
        public void StarterLevelDoorAndBlockColoursParseAsRed()
        {
            LevelData level = LevelDatabaseWindow.CreateStarterLevel("level-001", "Level 1");

            Assert.That(level.DoorCount, Is.EqualTo(1));
            Assert.That(level.BlockCount, Is.EqualTo(1));
            Assert.That(BoardLayout.TryParseDoorColor(level.GetDoorAt(0).ColorId, out PaletteColor doorColor), Is.True);
            Assert.That(doorColor, Is.EqualTo(PaletteColor.Red));
            Assert.That(BoardLayout.TryParseDoorColor(level.GetBlockAt(0).ColorId, out PaletteColor blockColor), Is.True);
            Assert.That(blockColor, Is.EqualTo(PaletteColor.Red));
        }

        [Test]
        public void StarterLevelBuildsABoardLayoutAndValidates()
        {
            LevelData level = LevelDatabaseWindow.CreateStarterLevel("level-001", "Level 1");

            Assert.That(BoardLayout.TryCreate(level, out BoardLayout layout, out string layoutError), Is.True, layoutError);
            Assert.That(layout, Is.Not.Null);
            Assert.That(level.Validate(out string validationError), Is.True, validationError);
        }

        [Test]
        public void StarterLevelKeepsItsIdNameSizeTimerAndStarterShape()
        {
            LevelData level = LevelDatabaseWindow.CreateStarterLevel("level-007", "Level 7");

            Assert.That(level.Id, Is.EqualTo("level-007"));
            Assert.That(level.Name, Is.EqualTo("Level 7"));
            Assert.That(level.Width, Is.EqualTo(3));
            Assert.That(level.Height, Is.EqualTo(3));
            Assert.That(level.TimerSeconds, Is.EqualTo(60));
            Assert.That(level.GetDoorAt(0).Id, Is.EqualTo("d1"));
            Assert.That(level.GetBlockAt(0).Id, Is.EqualTo("b1"));
            Assert.That(level.GetBlockAt(0).CellCount, Is.EqualTo(1), "The starter block is a Single (DEC-193).");
        }
    }
}
