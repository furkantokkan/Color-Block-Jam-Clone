using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies that every Level Editor command keeps the level JSON v2 fields it does not edit (void cells, block
    /// and door effects, cell objects), and that a resize moves or removes them with the content (defect D-005).
    /// </summary>
    public sealed class LevelEditV2FieldsTests
    {
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_domino = { Vector2Int.zero, Vector2Int.right };

        // No gate effect type is registered (DEC-177); the commands copy a door's effect list without validating
        // it, so a placeholder type shows that the list survives.
        private const string k_gateEffectType = "sample-gate-effect";

        [Test]
        public void BlockCommandsKeepEffectsVoidCellsAndCellObjects()
        {
            LevelData level = CreateLevel();

            AssertKept(Run(LevelEditCommands.TryMoveBlock(level, 0, new Vector2Int(1, 0), out LevelData moved, out string issue), moved, issue));
            AssertKept(Run(LevelEditCommands.TryRecolorBlock(level, 0, "Blue", out LevelData recoloured, out issue), recoloured, issue));
            AssertKept(Run(LevelEditCommands.TryReplaceBlockCells(level, 0, s_single, out LevelData replaced, out issue),
                replaced, issue));
            AssertKept(Run(LevelEditCommands.TryPlaceBlock(level, s_single, "Blue", new Vector2Int(3, 0),
                out LevelData placed, out issue), placed, issue));
            AssertKept(Run(LevelEditCommands.TryRemoveBlock(level, 1, out LevelData removed, out issue), removed, issue));
        }

        [Test]
        public void GateCommandsKeepEffectsVoidCellsAndCellObjects()
        {
            LevelData level = CreateLevel();

            LevelData extended = Run(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Top, 1, "Red", out LevelData result,
                out _, out string issue), result, issue);
            Assert.That(extended.GetDoorAt(0).Length, Is.EqualTo(2), "The Pen extends d1.");
            AssertKept(extended);
            AssertKept(Run(LevelEditCommands.TryRecolorDoor(level, 0, "Blue", out LevelData recoloured, out issue), recoloured, issue));
            AssertKept(Run(LevelEditCommands.TryRemoveDoor(level, 1, out LevelData removed, out issue), removed, issue));
        }

        [Test]
        public void JoiningGatesRefusesToDropTheAbsorbedGatesEffects()
        {
            LevelData level = new LevelData("join", "Join", 5, 5, 60, new[]
            {
                new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red", new[] { GateEffect() }),
                new LevelDoorData("d2", BoardSide.Top, 2, 1, "Red", new[] { GateEffect() })
            }, new[] { new LevelBlockData("b1", "Red", Vector2Int.zero, s_single) });

            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Top, 1, "Red", out LevelData result,
                out _, out string issue), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(issue, Is.EqualTo("Joining would remove gate d2 and its effects; remove them first."));

            // Without effects on the absorbed gate, the join keeps the lower gate and its effects.
            LevelData plain = new LevelData("join", "Join", 5, 5, 60, new[]
            {
                new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red", new[] { GateEffect() }),
                new LevelDoorData("d2", BoardSide.Top, 2, 1, "Red")
            }, new[] { new LevelBlockData("b1", "Red", Vector2Int.zero, s_single) });
            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(plain, BoardSide.Top, 1, "Red", out result, out _, out issue),
                Is.True, issue);
            Assert.That(result.DoorCount, Is.EqualTo(1));
            Assert.That(result.GetDoorAt(0).Length, Is.EqualTo(3));
            Assert.That(result.GetDoorAt(0).EffectCount, Is.EqualTo(1));
        }

        [Test]
        public void GrowingLeftShiftsVoidCellsAndCellObjectsWithTheContent()
        {
            LevelData level = CreateLevel();

            Assert.That(LevelEditCommands.TryResizeBoard(level, BoardSide.Left, 1, out LevelData grown,
                out LevelResizeReport report, out string issue), Is.True, issue);

            Assert.That(report.HasRemovals, Is.False);
            Assert.That(grown.Width, Is.EqualTo(6));
            Assert.That(grown.VoidCellCount, Is.EqualTo(1));
            Assert.That(grown.GetVoidCellAt(0), Is.EqualTo(new Vector2Int(5, 4)), "The void cell keeps its place.");
            Assert.That(grown.GetCellObjectAt(0).GetCellAt(0), Is.EqualTo(new Vector2Int(2, 3)));
            Assert.That(grown.GetCellObjectAt(0).Effect.Type, Is.EqualTo("barrier"));
            Assert.That(grown.GetBlockAt(0).EffectCount, Is.EqualTo(1));
            Assert.That(grown.GetDoorAt(0).EffectCount, Is.EqualTo(1));
        }

        [Test]
        public void ShrinkingMovesTheTopEdgeVoidAndRemovesCellObjectsWithTheirLine()
        {
            LevelData level = CreateLevel();

            Assert.That(LevelEditCommands.TryResizeBoard(level, BoardSide.Top, -1, out LevelData lower,
                out LevelResizeReport report, out string issue), Is.True, issue);
            Assert.That(lower.VoidCellCount, Is.EqualTo(1), "The void cell is on the Top edge line, so it follows the edge.");
            Assert.That(lower.GetVoidCellAt(0), Is.EqualTo(new Vector2Int(4, 3)), "It moves onto the new top edge, free in this fixture.");
            Assert.That(lower.CellObjectCount, Is.EqualTo(1), "The barrier below the removed row stays.");
            Assert.That(report.RemovedCellObjectCount, Is.Zero);

            Assert.That(LevelEditCommands.TryResizeBoard(level, BoardSide.Left, -2, out LevelData narrow,
                out report, out issue), Is.True, issue);
            Assert.That(narrow.CellObjectCount, Is.Zero, "The barrier in column 1 goes with it.");
            Assert.That(report.RemovedCellObjectCount, Is.EqualTo(1));
            Assert.That(report.HasRemovals, Is.True);
            Assert.That(report.RemovedCells, Does.Contain(new Vector2Int(1, 3)));
            Assert.That(report.Describe(), Is.EqualTo("Remove 1 block, 1 door and 1 board object?"),
                "b1, the top gate d1 over column 0 and the barrier go with the two left columns.");
            Assert.That(narrow.GetVoidCellAt(0), Is.EqualTo(new Vector2Int(2, 4)), "The void cell shifts left.");
        }

        [Test]
        public void ShrinkingKeepsTheEffectsOfAShortenedGate()
        {
            LevelData level = new LevelData("cut", "Cut", 5, 5, 60, new Vector2Int[0],
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 2, "Red", new[] { GateEffect() }) },
                new[] { new LevelBlockData("b1", "Red", new Vector2Int(3, 0), s_single) },
                new LevelCellObjectData[0]);

            Assert.That(LevelEditCommands.TryResizeBoard(level, BoardSide.Left, -1, out LevelData narrow,
                out LevelResizeReport report, out string issue), Is.True, issue);

            Assert.That(report.ShortenedDoorCount, Is.EqualTo(1));
            Assert.That(report.RemovedDoorCount, Is.Zero);
            LevelDoorData door = narrow.GetDoorAt(0);
            Assert.That(door.Start, Is.Zero);
            Assert.That(door.Length, Is.EqualTo(1));
            Assert.That(door.EffectCount, Is.EqualTo(1), "The shortened gate keeps its effect.");
            Assert.That(door.GetEffectAt(0).Type, Is.EqualTo(k_gateEffectType));
        }

        [Test]
        public void ShrinkingRemovesAWholeMultiCellObjectWhenOneOfItsCellsIsInARemovedLine()
        {
            LevelData level = new LevelData("wide", "Wide", 5, 5, 60, new Vector2Int[0], new LevelDoorData[0],
                new[] { new LevelBlockData("b1", "Red", new Vector2Int(3, 0), s_single) },
                new[]
                {
                    LevelEffectTests.Object("o1", LevelEffectTests.Effect("barrier", ("startsOpen", "false")),
                        new Vector2Int(0, 2), new Vector2Int(1, 2))
                });

            Assert.That(LevelEditCommands.TryResizeBoard(level, BoardSide.Left, -1, out LevelData narrow,
                out LevelResizeReport report, out string issue), Is.True, issue);

            Assert.That(narrow.CellObjectCount, Is.Zero, "Only (0,2) is in the removed column, but the object goes whole.");
            Assert.That(report.RemovedCellObjectCount, Is.EqualTo(1));
            Assert.That(report.RemovedCells, Does.Contain(new Vector2Int(0, 2)));
            Assert.That(report.RemovedCells, Does.Contain(new Vector2Int(1, 2)), "The hatch shows every cell it takes.");
            Assert.That(report.Describe(), Is.EqualTo("Remove 1 board object?"));
        }

        [Test]
        public void ResizeReportListsBoardObjectsWithBlocksAndDoors()
        {
            Assert.That(new LevelResizeReport(0, 0, 0, null, null, 2).Describe(), Is.EqualTo("Remove 2 board objects?"));
            Assert.That(new LevelResizeReport(1, 1, 1, null, null, 2).Describe(),
                Is.EqualTo("Remove 1 block, 1 door and 2 board objects, and shorten 1 door?"));
        }

        private static LevelData Run(bool succeeded, LevelData result, string issue)
        {
            Assert.That(succeeded, Is.True, issue);
            return result;
        }

        // The fixture's v2 content survives: one void cell, the ice on b1, the gate effect on d1 and the barrier.
        private static void AssertKept(LevelData level)
        {
            Assert.That(level.VoidCellCount, Is.EqualTo(1), "Void cells survive.");
            Assert.That(level.GetVoidCellAt(0), Is.EqualTo(new Vector2Int(4, 4)));
            LevelBlockData iced = FindBlock(level, "b1");
            Assert.That(iced.EffectCount, Is.EqualTo(1), "Block effects survive.");
            Assert.That(iced.GetEffectAt(0).Type, Is.EqualTo("ice"));
            Assert.That(iced.GetEffectAt(0).FindValue("clearsToThaw"), Is.EqualTo("2"));
            Assert.That(level.GetDoorAt(0).EffectCount, Is.EqualTo(1), "Door effects survive.");
            Assert.That(level.GetDoorAt(0).GetEffectAt(0).Type, Is.EqualTo(k_gateEffectType));
            Assert.That(level.CellObjectCount, Is.EqualTo(1), "Cell objects survive.");
            Assert.That(level.GetCellObjectAt(0).Id, Is.EqualTo("o1"));
            Assert.That(level.GetCellObjectAt(0).GetCellAt(0), Is.EqualTo(new Vector2Int(1, 3)));
        }

        private static LevelBlockData FindBlock(LevelData level, string id)
        {
            for (int index = 0; index < level.BlockCount; index++)
            {
                if (level.GetBlockAt(index).Id == id)
                {
                    return level.GetBlockAt(index);
                }
            }

            Assert.Fail($"Block {id} is missing.");
            return null;
        }

        private static LevelEffectData GateEffect()
        {
            return LevelEffectTests.Effect(k_gateEffectType, ("clearsToOpen", "3"));
        }

        private static LevelData CreateLevel()
        {
            return new LevelData("v2", "V2", 5, 5, 60,
                new[] { new Vector2Int(4, 4) },
                new[]
                {
                    new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red", new[] { GateEffect() }),
                    new LevelDoorData("d2", BoardSide.Right, 0, 1, "Blue")
                },
                new[]
                {
                    new LevelBlockData("b1", "Red", Vector2Int.zero, s_single,
                        new[] { LevelEffectTests.Effect("ice", ("clearsToThaw", "2")) }),
                    new LevelBlockData("b2", "Blue", new Vector2Int(2, 2), s_domino)
                },
                new[] { LevelEffectTests.Object("o1", LevelEffectTests.Effect("barrier", ("startsOpen", "false")), new Vector2Int(1, 3)) });
        }
    }
}
