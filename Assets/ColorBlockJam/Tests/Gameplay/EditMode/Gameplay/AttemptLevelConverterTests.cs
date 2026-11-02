using ColorBlockJam.Domain.Board;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// Converts authored LevelData into the attempt model from each block's own cells (AC-3.1, AC-8.5). No shape
    /// library or catalog takes part any more (DEC-193), so the unknown-shape and learned-shape cases were removed.
    /// </summary>
    public sealed class AttemptLevelConverterTests
    {
        private static readonly Vector2Int[] s_square =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1)
        };

        [Test]
        public void ATwoBlockTwoDoorLevelConverts()
        {
            LevelData level = CreateLevel("LightBlue", "LightBlue");

            Assert.That(Convert(level, out AttemptLevel result, out string error), Is.True, error);

            LevelModel model = result.Model;
            Assert.That(result.LevelId, Is.EqualTo("level-test"));
            Assert.That(model.Width, Is.EqualTo(4));
            Assert.That(model.Height, Is.EqualTo(5));
            Assert.That(model.TimerSeconds, Is.EqualTo(120));
            Assert.That(model.DoorCount, Is.EqualTo(2));
            Assert.That(model.GetDoor(0).Side, Is.EqualTo(Side.Top));
            Assert.That(model.GetDoor(0).Start, Is.EqualTo(1));
            Assert.That(model.GetDoor(0).Length, Is.EqualTo(3));
            Assert.That(model.GetDoor(1).Side, Is.EqualTo(Side.Bottom));
            Assert.That(model.GetBlock(0).Color, Is.EqualTo(model.GetDoor(0).Color));
            Assert.That(model.GetBlock(1).Color, Is.EqualTo(model.GetDoor(1).Color));
            Assert.That(model.GetBlock(0).Color, Is.Not.EqualTo(model.GetBlock(1).Color));
            Assert.That(model.GetBlock(0).Origin, Is.EqualTo(new Cell(2, 2)));
            Assert.That(model.GetBlock(0).CellCount, Is.EqualTo(4));
            Assert.That(model.GetDoorAtLane(Side.Top, 2), Is.EqualTo(0));

            BlockVisualSpec visual = result.GetVisual(0);
            Assert.That(visual.Color, Is.EqualTo(PaletteColor.LightBlue));
            Assert.That(visual.Origin, Is.EqualTo(new Vector2Int(2, 2)));
            Assert.That(visual.Cells, Is.EqualTo(s_square));
            Assert.That(result.GetBlockId(0), Is.EqualTo("b1"));
            Assert.That(result.GetBlockId(1), Is.EqualTo("b2"));
        }

        [Test]
        public void AnUnknownBlockColourFails()
        {
            LevelData level = CreateLevel("Wall", "LightBlue");

            Assert.That(Convert(level, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("b1").And.Contain("Wall"));
        }

        [Test]
        public void AnUnknownDoorColourFails()
        {
            // BoardLayout already rejects this door, so the layout comes from the valid variant.
            Assert.That(BoardLayout.TryCreate(CreateLevel("LightBlue", "LightBlue"), out BoardLayout layout, out _), Is.True);
            LevelData level = CreateLevel("LightBlue", "Teal");

            Assert.That(AttemptLevelConverter.TryConvert(level, layout, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("d1").And.Contain("Teal"));
        }

        [Test]
        public void AnInvalidLevelFails()
        {
            LevelData level = new LevelData("level-test", "Test", 4, 5, 120, new LevelDoorData[0], new[]
            {
                new LevelBlockData("b1", "Pink", new Vector2Int(0, 0), s_square),
                new LevelBlockData("b2", "Pink", new Vector2Int(1, 1), s_square)
            });

            Assert.That(Convert(level, out AttemptLevel result, out string error), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void ALayoutOfAnotherSizeFails()
        {
            LevelData other = new LevelData("level-other", "Other", 5, 5, 120, new LevelDoorData[0], new[]
            {
                new LevelBlockData("b1", "Pink", new Vector2Int(0, 0), s_square)
            });
            Assert.That(BoardLayout.TryCreate(other, out BoardLayout layout, out _), Is.True);

            Assert.That(AttemptLevelConverter.TryConvert(CreateLevel("LightBlue", "LightBlue"), layout,
                out _, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void ConversionLeavesTheSourceLevelUnchanged()
        {
            LevelData level = CreateLevel("LightBlue", "LightBlue");
            Assert.That(LevelDatabaseJson.TryWrite(new[] { level }, out string before, out string writeError), Is.True, writeError);

            Assert.That(Convert(level, out _, out _), Is.True);

            Assert.That(LevelDatabaseJson.TryWrite(new[] { level }, out string after, out _), Is.True);
            Assert.That(after, Is.EqualTo(before));
        }

        private static bool Convert(LevelData level, out AttemptLevel result, out string error)
        {
            Assert.That(BoardLayout.TryCreate(level, out BoardLayout layout, out string layoutError), Is.True, layoutError);
            return AttemptLevelConverter.TryConvert(level, layout, out result, out error);
        }

        // Level 1's layout on a 4 x 5 board: a Top and a Bottom door and two squares.
        private static LevelData CreateLevel(string firstColor, string firstDoorColor)
        {
            LevelDoorData[] doors =
            {
                new LevelDoorData("d1", BoardSide.Top, 1, 3, firstDoorColor),
                new LevelDoorData("d2", BoardSide.Bottom, 0, 3, "Pink")
            };
            LevelBlockData[] blocks =
            {
                new LevelBlockData("b1", firstColor, new Vector2Int(2, 2), s_square),
                new LevelBlockData("b2", "Pink", new Vector2Int(0, 1), s_square)
            };
            return new LevelData("level-test", "Test", 4, 5, 120, doors, blocks);
        }
    }
}
