using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor issues list: it has an error exactly when LevelData.Validate fails (on the
    /// LevelCatalogTests fixtures, hand-built cases, random drafts and the authored levels), each error carries the
    /// cells or door span the canvas highlights, and play-only warnings never count as errors.
    /// </summary>
    public sealed class LevelDraftIssuesTests
    {
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_domino = { Vector2Int.zero, Vector2Int.right };
        private static readonly Vector2Int[] s_corner = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };
        private static readonly Vector2Int[][] s_shapes = { s_single, s_domino, s_corner };
        private static readonly string[] s_colors = { "Red", "Blue", "LightBlue", "Pink" };

        [Test]
        public void AValidLevelWithADoorForEveryColourHasNoIssuesAndStaysUnchanged()
        {
            LevelData level = CreateLevel(
                new[]
                {
                    CreateDoor("d1", BoardSide.Top, 0, 2, "Red"),
                    CreateDoor("d2", BoardSide.Left, 1, 1, "Blue")
                },
                CreateBlock("b1", "Red", Vector2Int.zero, s_domino),
                CreateBlock("b2", "Blue", new Vector2Int(1, 2), s_corner));
            string before = JsonUtility.ToJson(level);

            List<LevelIssue> issues = AssertAgreement(level);

            Assert.That(issues, Is.Empty, Describe(issues));
            Assert.That(JsonUtility.ToJson(level), Is.EqualTo(before), "Collect never changes the level.");
        }

        [Test]
        public void NoLevelHasNoIssues()
        {
            Assert.That(LevelDraftIssues.Collect(null), Is.Empty);
            Assert.That(LevelDraftIssues.HasError(null), Is.False);
        }

        [Test]
        public void IssuesAgreeWithValidateOnTheLevelDatabaseFixtures()
        {
            LevelData[] fixtures =
            {
                LevelCatalogTests.CreateLevel("level-1", "First", Vector2Int.zero),
                LevelCatalogTests.CreateLevel("level-2", "Second", new Vector2Int(1, 0)),
                new LevelData("level-1", "Overlap", 3, 3, 60, new LevelDoorData[0],
                    new[]
                    {
                        LevelTestData.SingleBlock("block-1", Vector2Int.zero),
                        LevelTestData.SingleBlock("block-2", Vector2Int.zero)
                    }),
                new LevelData("level-2", "Doors", 3, 3, 60,
                    new[]
                    {
                        CreateDoor("door-1", BoardSide.Top, 0, 2, "Red"),
                        CreateDoor("door-2", BoardSide.Top, 1, 2, "Blue")
                    },
                    new[] { LevelTestData.SingleBlock("block-1", Vector2Int.zero) }),
                new LevelData("level-1", "Disconnected", 3, 3, 60, new LevelDoorData[0],
                    new[]
                    {
                        new LevelBlockData("block-1", "Red", Vector2Int.zero,
                            new[] { Vector2Int.zero, new Vector2Int(2, 0) })
                    }),
                LevelCatalogTests.CreateLevel("level-2", "Outside", new Vector2Int(3, 0))
            };
            bool[] expectedErrors = { false, false, true, true, true, true };

            for (int index = 0; index < fixtures.Length; index++)
            {
                List<LevelIssue> issues = AssertAgreement(fixtures[index]);
                Assert.That(LevelDraftIssues.HasError(issues), Is.EqualTo(expectedErrors[index]), fixtures[index].Name);
            }
        }

        [Test]
        public void IssuesAgreeWithValidateOnTheAuthoredLevels()
        {
            LevelData[] levels = ShippedLevels.Read();

            for (int index = 0; index < levels.Length; index++)
            {
                List<LevelIssue> issues = AssertAgreement(levels[index]);
                Assert.That(LevelDraftIssues.HasError(issues), Is.False, levels[index].Id + ": " + Describe(issues));
            }
        }

        [Test]
        public void AnOffBoardBlockListsEachCellOffTheBoard()
        {
            LevelDoorData[] doors = { CreateDoor("d1", BoardSide.Top, 0, 1, "Red") };

            LevelIssue issue = GetSingleError(AssertAgreement(
                CreateLevel(doors, CreateBlock("b1", "Red", new Vector2Int(3, 4), s_corner))));

            Assert.That(issue.Message, Is.EqualTo("Block b1 has 2 cells off the 4 x 5 board."));
            Assert.That(issue.EntityId, Is.EqualTo("b1"));
            Assert.That(issue.Cells, Is.EqualTo(new[] { new Vector2Int(4, 4), new Vector2Int(3, 5) }));
            Assert.That(issue.HasSegment, Is.False);

            issue = GetSingleError(AssertAgreement(
                CreateLevel(doors, CreateBlock("b1", "Red", new Vector2Int(3, 0), s_domino))));

            Assert.That(issue.Message, Is.EqualTo("Block b1 has cell (4,0) off the 4 x 5 board."));
            Assert.That(issue.Cells, Is.EqualTo(new[] { new Vector2Int(4, 0) }));
        }

        [Test]
        public void OverlappingBlocksListEverySharedCellOnTheLaterBlock()
        {
            LevelDoorData[] doors =
            {
                CreateDoor("d1", BoardSide.Top, 0, 1, "Red"),
                CreateDoor("d2", BoardSide.Bottom, 0, 1, "Blue")
            };

            LevelIssue issue = GetSingleError(AssertAgreement(CreateLevel(doors,
                CreateBlock("b1", "Red", Vector2Int.zero, s_domino),
                CreateBlock("b2", "Blue", Vector2Int.zero, s_corner))));

            Assert.That(issue.Message, Is.EqualTo("Blocks b1 and b2 overlap on 2 cells."));
            Assert.That(issue.EntityId, Is.EqualTo("b2"));
            Assert.That(issue.Cells, Is.EqualTo(new[] { Vector2Int.zero, Vector2Int.right }));

            issue = GetSingleError(AssertAgreement(CreateLevel(doors,
                CreateBlock("b1", "Red", Vector2Int.zero, s_domino),
                CreateBlock("b2", "Blue", Vector2Int.right, s_corner))));

            Assert.That(issue.Message, Is.EqualTo("Blocks b1 and b2 overlap at (1,0)."));
            Assert.That(issue.Cells, Is.EqualTo(new[] { Vector2Int.right }));
        }

        [Test]
        public void ADisconnectedBlockListsTheCellsOutsideItsLargestGroup()
        {
            LevelDoorData[] doors = { CreateDoor("d1", BoardSide.Top, 0, 1, "Red") };

            LevelIssue issue = GetSingleError(AssertAgreement(CreateLevel(doors, CreateBlock("b1", "Red",
                new Vector2Int(0, 1), new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(3, 0) }))));

            Assert.That(issue.Message, Is.EqualTo("Block b1 has a cell at (3,1) that does not touch the rest of the block."));
            Assert.That(issue.EntityId, Is.EqualTo("b1"));
            Assert.That(issue.Cells, Is.EqualTo(new[] { new Vector2Int(3, 1) }));

            issue = GetSingleError(AssertAgreement(CreateLevel(doors, CreateBlock("b1", "Red",
                Vector2Int.zero, new[] { Vector2Int.zero, new Vector2Int(2, 0), new Vector2Int(3, 0) }))));

            Assert.That(issue.Cells, Is.EqualTo(new[] { Vector2Int.zero }), "The larger group stays, whatever its order.");
        }

        [Test]
        public void ARepeatedBlockIdIsListedOnTheLaterBlock()
        {
            LevelIssue issue = GetSingleError(AssertAgreement(CreateLevel(
                new[] { CreateDoor("d1", BoardSide.Top, 0, 1, "Red") },
                CreateBlock("b1", "Red", Vector2Int.zero, s_single),
                CreateBlock("b1", "Red", new Vector2Int(2, 0), s_single))));

            Assert.That(issue.Message, Is.EqualTo("Another block already uses the ID b1."));
            Assert.That(issue.EntityId, Is.EqualTo("b1"));
            Assert.That(issue.Cells, Is.EqualTo(new[] { new Vector2Int(2, 0) }));
        }

        [Test]
        public void ADoorOutsideItsEdgeHighlightsItsAuthoredSpan()
        {
            LevelIssue issue = GetSingleError(AssertAgreement(CreateLevel(
                new[] { CreateDoor("d1", BoardSide.Top, 2, 3, "Red") },
                CreateBlock("b1", "Red", Vector2Int.zero, s_single))));

            Assert.That(issue.Message, Is.EqualTo("Door d1 runs off the 4-cell Top edge."));
            Assert.That(issue.EntityId, Is.EqualTo("d1"));
            Assert.That(issue.Cells, Is.Empty);
            AssertSegment(issue, BoardSide.Top, 2, 3);
        }

        [Test]
        public void OverlappingDoorsHighlightTheSharedSpanOnTheLaterDoor()
        {
            LevelIssue issue = GetSingleError(AssertAgreement(CreateLevel(
                new[]
                {
                    CreateDoor("d1", BoardSide.Right, 0, 3, "Red"),
                    CreateDoor("d2", BoardSide.Right, 2, 2, "Blue")
                },
                CreateBlock("b1", "Red", Vector2Int.zero, s_single),
                CreateBlock("b2", "Blue", new Vector2Int(2, 0), s_single))));

            Assert.That(issue.Message, Is.EqualTo("Doors d1 and d2 overlap on the Right edge."));
            Assert.That(issue.EntityId, Is.EqualTo("d2"));
            AssertSegment(issue, BoardSide.Right, 2, 1);
        }

        [Test]
        public void MissingColoursAreErrorsOnTheBlockAndTheDoor()
        {
            List<LevelIssue> issues = AssertAgreement(CreateLevel(
                new[] { CreateDoor("d1", BoardSide.Bottom, 1, 2, null) },
                CreateBlock("b1", " ", Vector2Int.zero, s_domino)));

            Assert.That(issues.Count, Is.EqualTo(2), Describe(issues));
            Assert.That(issues[0].Severity, Is.EqualTo(LevelIssueSeverity.Error));
            Assert.That(issues[0].Message, Is.EqualTo("Block b1 needs a colour."));
            Assert.That(issues[0].EntityId, Is.EqualTo("b1"));
            Assert.That(issues[0].Cells, Is.EqualTo(new[] { Vector2Int.zero, Vector2Int.right }));
            Assert.That(issues[1].Severity, Is.EqualTo(LevelIssueSeverity.Error));
            Assert.That(issues[1].Message, Is.EqualTo("Door d1 needs a colour."));
            Assert.That(issues[1].EntityId, Is.EqualTo("d1"));
            AssertSegment(issues[1], BoardSide.Bottom, 1, 2);
        }

        [Test]
        public void ALevelWithoutBlocksIsALevelWideError()
        {
            List<LevelIssue> issues = AssertAgreement(CreateLevel(
                new[] { CreateDoor("d1", BoardSide.Top, 0, 1, "Red") }));

            Assert.That(issues.Count, Is.EqualTo(1), Describe(issues));
            Assert.That(issues[0].Severity, Is.EqualTo(LevelIssueSeverity.Error));
            Assert.That(issues[0].Message, Is.EqualTo("The level needs at least one block."));
            Assert.That(issues[0].EntityId, Is.Null);
            Assert.That(issues[0].Cells, Is.Empty);
            Assert.That(issues[0].HasSegment, Is.False);
        }

        [Test]
        public void ABoardOutsideTheSizeLimitsIsALevelWideError()
        {
            LevelIssue issue = GetSingleError(AssertAgreement(new LevelData("level-1", "Level 1", 9, 2, 60,
                new[] { CreateDoor("d1", BoardSide.Top, 0, 1, "Red") },
                new[] { CreateBlock("b1", "Red", Vector2Int.zero, s_single) })));

            Assert.That(issue.Message, Is.EqualTo("The board is 9 x 2; it must be 3-8 wide and 3-10 tall."));
            Assert.That(issue.EntityId, Is.Null);
            Assert.That(issue.Cells, Is.Empty);
        }

        [Test]
        public void EveryOtherValidateRuleGivesAnErrorThatAgrees()
        {
            LevelDoorData door = CreateDoor("d1", BoardSide.Top, 0, 1, "Red");
            LevelDoorData[] doors = { door };
            LevelBlockData block = CreateBlock("b1", "Red", Vector2Int.zero, s_single);
            LevelBlockData[] blocks = { block };

            AssertError(new LevelData(" ", "Level 1", 4, 5, 60, doors, blocks), "The level needs an ID.");
            AssertError(new LevelData("level-1", null, 4, 5, 60, doors, blocks), "The level needs a name.");
            AssertError(new LevelData("level-1", "Level 1", 4, 5, 9, doors, blocks),
                "The timer is 9 seconds; it must be 10-300 seconds.");
            AssertError(new LevelData("level-1", "Level 1", 4, 5, 301, doors, blocks),
                "The timer is 301 seconds; it must be 10-300 seconds.");
            AssertError(CreateLevel(doors, block, null), "Block 2 in the list is empty.");
            AssertError(CreateLevel(new[] { door, null }, block), "Door 2 in the list is empty.");
            AssertError(CreateLevel(doors, CreateBlock("b1", "Red", Vector2Int.zero, new Vector2Int[0])),
                "Block b1 has no cells.");
            AssertError(CreateLevel(doors, CreateBlock("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero, Vector2Int.zero })),
                "Block b1 lists cell (0,0) more than once.");
            AssertError(CreateLevel(doors, new LevelBlockData(" ", "", Vector2Int.zero, s_single)),
                "A block needs an ID and a colour.");
            AssertError(CreateLevel(new[] { CreateDoor("d1", (BoardSide)7, 0, 1, "Red") }, block),
                "Door d1 is on an unknown edge.");
            AssertError(CreateLevel(new[] { CreateDoor("d1", BoardSide.Top, 0, 0, "Red") }, block),
                "Door d1 needs a length of at least one cell.");
            AssertError(CreateLevel(new[] { CreateDoor("d1", BoardSide.Left, -1, 2, "Red") }, block),
                "Door d1 runs off the 5-cell Left edge.");
            AssertError(CreateLevel(new[] { door, CreateDoor("d1", BoardSide.Bottom, 0, 1, "Red") }, block),
                "Another door already uses the ID d1.");
            AssertError(CreateLevel(new[] { CreateDoor("", BoardSide.Right, 0, 1, "Red") }, block),
                "A door on the Right edge needs an ID.");
        }

        [Test]
        public void AValidateRuleWithoutALocationFallsBackToValidatesMessage()
        {
            LevelData level = new LevelData("level-1", "Level 1", 4, 5, 60, null,
                new[] { CreateBlock("b1", "Red", Vector2Int.zero, s_single) });
            Assert.That(level.Validate(out string error), Is.False);

            LevelIssue issue = GetSingleError(AssertAgreement(level));

            Assert.That(issue.Message, Is.EqualTo(error));
            Assert.That(issue.Cells, Is.Empty);
            Assert.That(issue.HasSegment, Is.False);
        }

        [Test]
        public void ADoorColourThatIsNotAGateColourIsAnError()
        {
            LevelData level = CreateLevel(
                new[]
                {
                    CreateDoor("d1", BoardSide.Top, 0, 1, "Red"),
                    CreateDoor("d2", BoardSide.Bottom, 2, 2, "Wall")
                },
                CreateBlock("b1", "Red", Vector2Int.zero, s_single));

            List<LevelIssue> issues = AssertAgreement(level);

            Assert.That(level.Validate(out _), Is.False, "Schema v2 validates door colours.");
            Assert.That(issues.Count, Is.EqualTo(1), "The layout refusal repeats nothing. " + Describe(issues));
            Assert.That(issues[0].Severity, Is.EqualTo(LevelIssueSeverity.Error));
            Assert.That(issues[0].Message, Is.EqualTo("Door d2 uses 'Wall', which is not a gate colour."));
            Assert.That(issues[0].EntityId, Is.EqualTo("d2"));
            AssertSegment(issues[0], BoardSide.Bottom, 2, 2);
        }

        [Test]
        public void AGateLongerThanThreeCellsIsAnError()
        {
            LevelData level = CreateLevel(
                new[] { CreateDoor("d1", BoardSide.Top, 0, 4, "Red") },
                CreateBlock("b1", "Red", Vector2Int.zero, s_single));

            List<LevelIssue> issues = AssertAgreement(level);

            Assert.That(level.Validate(out _), Is.False, "Schema v2 validates the gate length.");
            Assert.That(issues.Count, Is.EqualTo(1), Describe(issues));
            Assert.That(issues[0].Severity, Is.EqualTo(LevelIssueSeverity.Error));
            Assert.That(issues[0].Message, Is.EqualTo("Door d1 spans 4 cells; gates span 1 to 3."));
            AssertSegment(issues[0], BoardSide.Top, 0, 4);
        }

        [Test]
        public void ABlockColourWithoutADoorOfThatColourWarnsThatItCanNeverExit()
        {
            List<LevelIssue> issues = AssertAgreement(CreateLevel(
                new[] { CreateDoor("d1", BoardSide.Top, 0, 1, "Red") },
                CreateBlock("b1", "Red", Vector2Int.zero, s_single),
                CreateBlock("b2", "Blue", new Vector2Int(1, 1), s_domino)));

            Assert.That(LevelDraftIssues.HasError(issues), Is.False);
            Assert.That(issues.Count, Is.EqualTo(1), Describe(issues));
            Assert.That(issues[0].Severity, Is.EqualTo(LevelIssueSeverity.Warning));
            Assert.That(issues[0].Message, Is.EqualTo("Block b2 can never exit: no Blue door is on the board."));
            Assert.That(issues[0].EntityId, Is.EqualTo("b2"));
            Assert.That(issues[0].Cells, Is.EqualTo(new[] { new Vector2Int(1, 1), new Vector2Int(2, 1) }));
        }

        [Test]
        public void ABlockColourThatIsNotAGateColourWarnsOnce()
        {
            List<LevelIssue> issues = AssertAgreement(CreateLevel(
                new[] { CreateDoor("d1", BoardSide.Top, 0, 1, "Red") },
                CreateBlock("b1", "Grey", Vector2Int.zero, s_single)));

            Assert.That(LevelDraftIssues.HasError(issues), Is.False);
            Assert.That(issues.Count, Is.EqualTo(1), Describe(issues));
            Assert.That(issues[0].Severity, Is.EqualTo(LevelIssueSeverity.Warning));
            Assert.That(issues[0].Message, Is.EqualTo("Block b1 uses 'Grey', which is not a gate colour."));
        }

        [Test]
        public void ErrorsComeFirstThenWarningsEachWithBlocksBeforeDoors()
        {
            List<LevelIssue> issues = AssertAgreement(CreateLevel(
                new[]
                {
                    CreateDoor("d1", BoardSide.Top, 0, 1, "Wall"),
                    CreateDoor("d2", BoardSide.Top, 3, 2, "Red")
                },
                CreateBlock("b1", "Blue", Vector2Int.zero, s_single),
                CreateBlock("b2", "Red", new Vector2Int(5, 0), s_single)));

            string[] messages = new string[issues.Count];
            LevelIssueSeverity[] severities = new LevelIssueSeverity[issues.Count];
            for (int index = 0; index < issues.Count; index++)
            {
                messages[index] = issues[index].Message;
                severities[index] = issues[index].Severity;
            }

            Assert.That(messages, Is.EqualTo(new[]
            {
                "Block b2 has cell (5,0) off the 4 x 5 board.",
                "Door d1 uses 'Wall', which is not a gate colour.",
                "Door d2 runs off the 4-cell Top edge.",
                "Block b1 can never exit: no Blue door is on the board."
            }));
            Assert.That(severities, Is.EqualTo(new[]
            {
                LevelIssueSeverity.Error, LevelIssueSeverity.Error, LevelIssueSeverity.Error,
                LevelIssueSeverity.Warning
            }));
        }

        [Test]
        public void RandomDraftsAgreeWithValidate()
        {
            System.Random random = new System.Random(20260925);
            int validCount = 0;
            int invalidCount = 0;
            for (int run = 0; run < 400; run++)
            {
                LevelData level = CreateRandomLevel(random);
                int mutations = random.Next(0, 3);
                for (int step = 0; step < mutations; step++)
                {
                    level = Mutate(level, random);
                }

                AssertAgreement(level);
                if (level.Validate(out _))
                {
                    validCount++;
                }
                else
                {
                    invalidCount++;
                }
            }

            Assert.That(validCount, Is.GreaterThan(0), "The random drafts include valid levels.");
            Assert.That(invalidCount, Is.GreaterThan(0), "The random drafts include invalid levels.");
        }

        // The agreement rule, plus the order rule: no error follows a warning.
        private static List<LevelIssue> AssertAgreement(LevelData level)
        {
            List<LevelIssue> issues = LevelDraftIssues.Collect(level);
            bool valid = level.Validate(out string error);
            Assert.That(LevelDraftIssues.HasError(issues), Is.EqualTo(!valid), valid
                ? "Validate accepts the level, but the list has an error: " + Describe(issues)
                : "Validate refuses the level (" + error + "), but the list has no error: " + Describe(issues));

            bool warningSeen = false;
            for (int index = 0; index < issues.Count; index++)
            {
                LevelIssue issue = issues[index];
                Assert.That(issue.Message, Is.Not.Null.And.Not.Empty);
                Assert.That(issue.Cells, Is.Not.Null);
                if (issue.Severity == LevelIssueSeverity.Warning)
                {
                    warningSeen = true;
                }
                else
                {
                    Assert.That(warningSeen, Is.False, "Errors come before warnings: " + Describe(issues));
                }
            }

            return issues;
        }

        private static void AssertError(LevelData level, string message)
        {
            List<LevelIssue> issues = AssertAgreement(level);
            bool found = false;
            for (int index = 0; index < issues.Count && !found; index++)
            {
                found = issues[index].Severity == LevelIssueSeverity.Error
                    && string.Equals(issues[index].Message, message, StringComparison.Ordinal);
            }

            Assert.That(found, Is.True, $"Expected the error \"{message}\" in: {Describe(issues)}");
        }

        private static LevelIssue GetSingleError(List<LevelIssue> issues)
        {
            LevelIssue error = null;
            int count = 0;
            for (int index = 0; index < issues.Count; index++)
            {
                if (issues[index].Severity == LevelIssueSeverity.Error)
                {
                    error = issues[index];
                    count++;
                }
            }

            Assert.That(count, Is.EqualTo(1), Describe(issues));
            return error;
        }

        private static void AssertSegment(LevelIssue issue, BoardSide side, int start, int length)
        {
            Assert.That(issue.HasSegment, Is.True);
            Assert.That(issue.Side, Is.EqualTo(side));
            Assert.That(issue.SegmentStart, Is.EqualTo(start));
            Assert.That(issue.SegmentLength, Is.EqualTo(length));
        }

        private static string Describe(List<LevelIssue> issues)
        {
            if (issues.Count == 0)
            {
                return "(no issues)";
            }

            string[] lines = new string[issues.Count];
            for (int index = 0; index < lines.Length; index++)
            {
                lines[index] = issues[index].Severity + ": " + issues[index].Message;
            }

            return string.Join(" | ", lines);
        }

        private static LevelData CreateLevel(LevelDoorData[] doors, params LevelBlockData[] blocks)
        {
            return new LevelData("level-1", "Level 1", 4, 5, 60, doors, blocks);
        }

        private static LevelBlockData CreateBlock(string id, string colorId, Vector2Int origin, Vector2Int[] cells)
        {
            return new LevelBlockData(id, colorId, origin, cells);
        }

        private static LevelDoorData CreateDoor(string id, BoardSide side, int start, int length, string colorId)
        {
            return new LevelDoorData(id, side, start, length, colorId);
        }

        // A level that is valid apart from chance overlaps of its blocks or doors.
        private static LevelData CreateRandomLevel(System.Random random)
        {
            int width = random.Next(3, 9);
            int height = random.Next(3, 11);
            LevelDoorData[] doors = new LevelDoorData[random.Next(0, 3)];
            for (int index = 0; index < doors.Length; index++)
            {
                doors[index] = CreateRandomDoor(random, "d" + (index + 1), width, height);
            }

            LevelBlockData[] blocks = new LevelBlockData[random.Next(1, 4)];
            for (int index = 0; index < blocks.Length; index++)
            {
                // Every shape spans at most two cells per axis, so these origins keep it on the board.
                Vector2Int origin = new Vector2Int(random.Next(0, width - 1), random.Next(0, height - 1));
                blocks[index] = CreateBlock("b" + (index + 1), s_colors[random.Next(s_colors.Length)], origin,
                    s_shapes[random.Next(s_shapes.Length)]);
            }

            return new LevelData("level-r", "Random", width, height, 60, doors, blocks);
        }

        private static LevelDoorData CreateRandomDoor(System.Random random, string id, int width, int height)
        {
            BoardSide side = (BoardSide)random.Next(0, 4);
            int sideLength = side == BoardSide.Top || side == BoardSide.Bottom ? width : height;
            int start = random.Next(0, sideLength);
            int length = random.Next(1, Math.Min(2, sideLength - start) + 1);
            return CreateDoor(id, side, start, length, s_colors[random.Next(s_colors.Length)]);
        }

        // Breaks, or sometimes only bends, one rule so the random drafts reach every check.
        private static LevelData Mutate(LevelData level, System.Random random)
        {
            string id = level.Id;
            string name = level.Name;
            int width = level.Width;
            int height = level.Height;
            int timer = level.TimerSeconds;
            LevelDoorData[] doors = LevelRecordCopy.GetDoors(level);
            LevelBlockData[] blocks = LevelRecordCopy.GetBlocks(level);
            int blockIndex = blocks.Length > 0 ? random.Next(blocks.Length) : -1;
            LevelBlockData block = blockIndex >= 0 ? blocks[blockIndex] : null;
            int doorIndex = doors.Length > 0 ? random.Next(doors.Length) : -1;
            LevelDoorData door = doorIndex >= 0 ? doors[doorIndex] : null;
            switch (random.Next(0, 20))
            {
                case 0:
                    id = " ";
                    break;
                case 1:
                    name = null;
                    break;
                case 2:
                    width = random.Next(0, 2) == 0 ? 2 : 9;
                    break;
                case 3:
                    height = random.Next(0, 2) == 0 ? 2 : 11;
                    break;
                case 4:
                    timer = random.Next(0, 2) == 0 ? 9 : 301;
                    break;
                case 5:
                    blocks = new LevelBlockData[0];
                    break;
                case 6:
                    doors = null;
                    break;
                case 7:
                    if (block != null)
                    {
                        blocks[blockIndex] = null;
                    }

                    break;
                case 8:
                    if (door != null)
                    {
                        doors[doorIndex] = null;
                    }

                    break;
                case 9:
                    if (block != null)
                    {
                        int shape = random.Next(0, 3);
                        Vector2Int[] cells = shape == 0 ? new Vector2Int[0]
                            : shape == 1 ? new[] { Vector2Int.zero, Vector2Int.zero }
                            : new[] { Vector2Int.zero, new Vector2Int(2, 0) };
                        blocks[blockIndex] = LevelRecordCopy.WithCells(block, cells);
                    }

                    break;
                case 10:
                case 11:
                    if (block != null)
                    {
                        blocks[blockIndex] = LevelRecordCopy.WithOrigin(block,
                            new Vector2Int(random.Next(-2, width + 2), random.Next(-2, height + 2)));
                    }

                    break;
                case 12:
                    if (block != null && blocks[0] != null)
                    {
                        blocks[blockIndex] = new LevelBlockData(blocks[0].Id, block.ColorId, block.Origin,
                            LevelRecordCopy.GetCells(block));
                    }

                    break;
                case 13:
                    if (block != null)
                    {
                        // The third draw was the removed shape id (DEC-193); it is kept so the random stream
                        // and every later draft stay the same.
                        int field = random.Next(0, 3);
                        blocks[blockIndex] = new LevelBlockData(field == 0 ? " " : block.Id,
                            field == 1 ? "" : block.ColorId, block.Origin, LevelRecordCopy.GetCells(block));
                    }

                    break;
                case 14:
                    if (block != null)
                    {
                        blocks[blockIndex] = LevelRecordCopy.WithColor(block, random.Next(0, 2) == 0 ? "Wall" : "Grey");
                    }

                    break;
                case 15:
                    if (door != null)
                    {
                        int choice = random.Next(0, 3);
                        doors[doorIndex] = CreateDoor(door.Id, door.Side, choice == 0 ? -1 : door.Start,
                            choice == 1 ? 0 : choice == 2 ? door.Length + 8 : door.Length, door.ColorId);
                    }

                    break;
                case 16:
                    if (door != null)
                    {
                        doors[doorIndex] = CreateDoor(door.Id, (BoardSide)7, door.Start, door.Length, door.ColorId);
                    }

                    break;
                case 17:
                    if (door != null)
                    {
                        int field = random.Next(0, 3);
                        doors[doorIndex] = CreateDoor(field == 0 ? "" : door.Id, door.Side, door.Start, door.Length,
                            field == 1 ? null : field == 2 ? "Wall" : door.ColorId);
                    }

                    break;
                case 18:
                    if (door != null && doors[0] != null)
                    {
                        doors[doorIndex] = CreateDoor(doors[0].Id, door.Side, door.Start, door.Length, door.ColorId);
                    }

                    break;
                default:
                    Array.Resize(ref doors, doors.Length + 1);
                    doors[doors.Length - 1] = CreateRandomDoor(random, "d" + doors.Length, Math.Max(2, width),
                        Math.Max(2, height));
                    break;
            }

            return new LevelData(id, name, width, height, timer, doors, blocks);
        }
    }
}
