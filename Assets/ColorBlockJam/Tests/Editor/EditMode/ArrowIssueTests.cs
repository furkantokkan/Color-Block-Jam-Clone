using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// The Level Editor's PRE-1 check for Arrow blocks (catalog BLK-01, AC-BLK-01.5): a 1x1, I3 or L3 Arrow block with
    /// no door of its colour on either axis side within its row (Horizontal) or column (Vertical) span gets one warning
    /// naming the block and the missing sides; such a door removes it. It is a warning, so Save is never blocked.
    /// </summary>
    public sealed class ArrowIssueTests
    {
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_line = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) };
        private static readonly Vector2Int[] s_corner = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1) };

        [Test]
        public void AHorizontalArrowBlockWithItsDoorOnlyOnAPerpendicularSideWarnsNamingItsAxisSides()
        {
            LevelData level = CreateLevel(
                new[] { Door("d1", BoardSide.Top, 2, 1, "Red") },
                Block("b1", "Red", new Vector2Int(2, 2), s_single, "Horizontal"));

            LevelIssue issue = GetSingleIssue(level);

            Assert.That(issue.Severity, Is.EqualTo(LevelIssueSeverity.Warning));
            Assert.That(issue.Message, Is.EqualTo(
                "Block b1 can never exit: its Horizontal arrow needs a Red door on the Left or Right edge within row 2."));
            Assert.That(issue.EntityId, Is.EqualTo("b1"));
            Assert.That(issue.Cells, Is.EqualTo(new[] { new Vector2Int(2, 2) }), "The issue outlines the block.");
        }

        [Test]
        public void AVerticalArrowBlockWithItsDoorOnlyOnAPerpendicularSideWarnsNamingItsAxisSides()
        {
            LevelData level = CreateLevel(
                new[] { Door("d1", BoardSide.Right, 0, 1, "Blue") },
                Block("b2", "Blue", new Vector2Int(1, 0), s_line, "Vertical"));

            LevelIssue issue = GetSingleIssue(level);

            Assert.That(issue.Message, Is.EqualTo(
                "Block b2 can never exit: its Vertical arrow needs a Blue door on the Top or Bottom edge within columns 1-3."));
        }

        [Test]
        public void AnL3SpanningTwoRowsNamesBothRowsAndADoorOnEitherRowClearsTheWarning()
        {
            LevelBlockData corner = Block("b3", "Pink", new Vector2Int(1, 2), s_corner, "Horizontal");
            LevelData perpendicular = CreateLevel(new[] { Door("d1", BoardSide.Bottom, 1, 2, "Pink") }, corner);

            Assert.That(GetSingleIssue(perpendicular).Message, Is.EqualTo(
                "Block b3 can never exit: its Horizontal arrow needs a Pink door on the Left or Right edge within rows 2-3."));

            LevelData upperRow = CreateLevel(new[] { Door("d1", BoardSide.Right, 3, 1, "Pink") }, corner);
            LevelData lowerRow = CreateLevel(new[] { Door("d1", BoardSide.Left, 2, 1, "Pink") }, corner);
            Assert.That(Collect(upperRow), Is.Empty, "A Right door on its upper row is on an axis side.");
            Assert.That(Collect(lowerRow), Is.Empty, "A Left door on its lower row is on an axis side.");
        }

        [Test]
        public void AnAxisDoorOutsideTheSpanOrOfAnotherColourStillWarns()
        {
            LevelBlockData line = Block("b1", "Red", new Vector2Int(0, 1), s_line, "Horizontal");

            LevelData outsideSpan = CreateLevel(new[] { Door("d1", BoardSide.Left, 3, 1, "Red") }, line);
            LevelData otherColour = CreateLevel(
                new[] { Door("d1", BoardSide.Left, 1, 1, "Blue"), Door("d2", BoardSide.Top, 0, 3, "Red") }, line,
                Block("b2", "Blue", new Vector2Int(4, 4), s_single, null));

            const string expected =
                "Block b1 can never exit: its Horizontal arrow needs a Red door on the Left or Right edge within row 1.";
            Assert.That(GetSingleIssue(outsideSpan).Message, Is.EqualTo(expected));
            Assert.That(GetSingleIssue(otherColour).Message, Is.EqualTo(expected));
        }

        [Test]
        public void VerticalI3AndL3ClearTheWarningWithADoorOnTheirTopOrBottomEdgeWithinTheirColumns()
        {
            LevelBlockData line = Block("b2", "Blue", new Vector2Int(1, 0), s_line, "Vertical");
            LevelData bottomDoor = CreateLevel(new[] { Door("d1", BoardSide.Bottom, 3, 1, "Blue") }, line);
            Assert.That(Collect(bottomDoor), Is.Empty, "A Bottom door on its last column is on an axis side.");

            LevelBlockData corner = Block("b3", "Pink", new Vector2Int(1, 2), s_corner, "Vertical");
            LevelData perpendicular = CreateLevel(new[] { Door("d1", BoardSide.Left, 2, 2, "Pink") }, corner);
            LevelData topDoor = CreateLevel(new[] { Door("d1", BoardSide.Top, 2, 1, "Pink") }, corner);

            Assert.That(GetSingleIssue(perpendicular).Message, Is.EqualTo(
                "Block b3 can never exit: its Vertical arrow needs a Pink door on the Top or Bottom edge within columns 1-2."));
            Assert.That(Collect(topDoor), Is.Empty, "A Top door on its second column is on an axis side.");
        }

        [Test]
        public void AnAxisDoorOfItsColourOrNoArrowGivesNoWarningAndNoDoorAtAllKeepsTheOneExistingWarning()
        {
            LevelData axisDoor = CreateLevel(new[] { Door("d1", BoardSide.Top, 2, 1, "Red") },
                Block("b1", "Red", new Vector2Int(2, 2), s_single, "Vertical"));
            LevelData plain = CreateLevel(new[] { Door("d1", BoardSide.Top, 2, 1, "Red") },
                Block("b1", "Red", new Vector2Int(2, 2), s_single, null));
            LevelData noDoor = CreateLevel(new[] { Door("d1", BoardSide.Top, 2, 1, "Blue") },
                Block("b1", "Red", new Vector2Int(2, 2), s_single, "Vertical"),
                Block("b2", "Blue", new Vector2Int(0, 0), s_single, null));

            Assert.That(Collect(axisDoor), Is.Empty);
            Assert.That(Collect(plain), Is.Empty, "A block without Arrow exits through any side.");
            Assert.That(GetSingleIssue(noDoor).Message, Is.EqualTo("Block b1 can never exit: no Red door is on the board."),
                "The broader warning covers it, so the Arrow warning is not repeated.");
        }

        private static LevelIssue GetSingleIssue(LevelData level)
        {
            List<LevelIssue> issues = Collect(level);
            Assert.That(issues.Count, Is.EqualTo(1), Describe(issues));
            Assert.That(LevelDraftIssues.HasError(issues), Is.False, "PRE-1 never blocks Save.");
            return issues[0];
        }

        private static List<LevelIssue> Collect(LevelData level)
        {
            Assert.That(level.Validate(out string error), Is.True, error);
            return LevelDraftIssues.Collect(level);
        }

        private static string Describe(List<LevelIssue> issues)
        {
            string text = "";
            for (int index = 0; index < issues.Count; index++)
            {
                text += "\n" + issues[index].Severity + ": " + issues[index].Message;
            }

            return text;
        }

        private static LevelData CreateLevel(LevelDoorData[] doors, params LevelBlockData[] blocks)
        {
            return new LevelData("level-1", "Level 1", 5, 5, 60, doors, blocks);
        }

        private static LevelDoorData Door(string id, BoardSide side, int start, int length, string colorId)
        {
            return new LevelDoorData(id, side, start, length, colorId);
        }

        private static LevelBlockData Block(string id, string colorId, Vector2Int origin, Vector2Int[] cells, string axis)
        {
            LevelEffectData[] effects = axis == null
                ? new LevelEffectData[0]
                : new[] { new LevelEffectData("arrow", new[] { new LevelEffectParamData("axis", axis) }) };
            return new LevelBlockData(id, colorId, origin, cells, effects);
        }
    }
}
