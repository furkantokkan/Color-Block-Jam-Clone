using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// The Barrier metadata read on Home (contract TASK-20260925-103527585-3d1b0a34, Expected behavior 2): barrier cell
    /// objects are copied in authored order through the v2 schema API, other board objects are ignored, the level is
    /// never changed, and each CreateRule starts at the authored state, so every attempt, Restart included, begins at
    /// startsOpen (acceptance 5, domain half).
    /// </summary>
    public sealed class BarrierEffectLevelTests
    {
        [Test]
        public void CreateCopiesEveryBarrierInAuthoredOrderAndIgnoresOtherBoardObjects()
        {
            LevelData level = CreateLevel(
                Barrier("lane", false, new Vector2Int(2, 0), new Vector2Int(2, 1)),
                new LevelCellObjectData("crate", new[] { new Vector2Int(4, 0) },
                    new LevelEffectData("crate", new[] { new LevelEffectParamData("clearsToOpen", "2") })),
                Barrier("gate", true, new Vector2Int(4, 3)));

            BarrierEffectLevel barriers = BarrierEffectLevel.Create(level);

            Assert.That(barriers.TypeId, Is.EqualTo("barrier"));
            Assert.That(barriers.BlockCount, Is.EqualTo(2), "Effect metadata reports the authored block count.");
            Assert.That(barriers.Width, Is.EqualTo(6));
            Assert.That(barriers.Height, Is.EqualTo(4));
            Assert.That(barriers.BarrierCount, Is.EqualTo(2), "The crate is not a barrier.");
            Assert.That(barriers.GetId(0), Is.EqualTo("lane"));
            Assert.That(barriers.GetStartsOpen(0), Is.False);
            Assert.That(barriers.GetCellCount(0), Is.EqualTo(2));
            Assert.That(barriers.GetCell(0, 0), Is.EqualTo(new Cell(2, 0)));
            Assert.That(barriers.GetCell(0, 1), Is.EqualTo(new Cell(2, 1)));
            Assert.That(barriers.GetId(1), Is.EqualTo("gate"));
            Assert.That(barriers.GetStartsOpen(1), Is.True);
            Assert.That(barriers.GetCell(1, 0), Is.EqualTo(new Cell(4, 3)));

            Assert.That(level.CellObjectCount, Is.EqualTo(3), "The level is never changed.");
            Assert.That(level.GetCellObjectAt(0).GetCellAt(0), Is.EqualTo(new Vector2Int(2, 0)));
        }

        [Test]
        public void EveryRuleStartsAtTheAuthoredStateSoRestartRestoresIt()
        {
            BarrierEffectLevel barriers = BarrierEffectLevel.Create(CreateLevel(
                Barrier("lane", false, new Vector2Int(2, 0)), Barrier("gate", true, new Vector2Int(3, 3))));

            BarrierRule first = barriers.CreateRule();
            first.OnBlockRemoved(new BlockRemovedEvent(0));
            Assert.That(first.IsOpen(0), Is.True);
            Assert.That(first.IsOpen(1), Is.False);

            BarrierRule second = barriers.CreateRule();
            Assert.That(second, Is.Not.SameAs(first), "Each attempt owns its rule.");
            Assert.That(second.IsOpen(0), Is.False, "A new attempt starts at startsOpen.");
            Assert.That(second.IsOpen(1), Is.True);
            Assert.That(second.GetVersion(0), Is.Zero);
            Assert.That(second.GetBarrierAt(new Cell(3, 3)), Is.EqualTo(1));
        }

        [Test]
        public void ALevelWithoutBarriersGivesARuleThatAllowsEveryCell()
        {
            BarrierEffectLevel barriers = BarrierEffectLevel.Create(CreateLevel());
            BarrierRule rule = barriers.CreateRule();
            BoardState board = new BoardState(BoardFixture.Create(new[] { "A....." }));

            Assert.That(barriers.BarrierCount, Is.Zero);
            Assert.That(rule.BarrierCount, Is.Zero, "The module registers no hook for it.");
            Assert.That(rule.AllowsCell(board, 0, new Cell(3, 0)), Is.True);
            rule.OnBlockRemoved(new BlockRemovedEvent(0));
            Assert.That(rule.AllowsCell(board, 0, new Cell(3, 0)), Is.True);
        }

        [Test]
        public void InvalidMetadataIsRefusedOnHome()
        {
            Cell[][] oneCell = { new[] { new Cell(0, 0) } };

            Assert.Throws<ArgumentNullException>(() => BarrierEffectLevel.Create(null));
            Assert.Throws<ArgumentException>(() => new BarrierEffectLevel(1, 3, 3, new[] { "a", "b" }, oneCell, new[] { true }));
            Assert.Throws<ArgumentException>(() => new BarrierEffectLevel(1, 3, 3, new[] { "" }, oneCell, new[] { true }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BarrierEffectLevel(1, 3, 3, new[] { "a" }, new[] { new[] { new Cell(3, 0) } }, new[] { true }),
                "A cell outside the board fails when the metadata is read, not in an attempt.");
        }

        // A 6 x 4 board with two single blocks at (0, 0) and (5, 3).
        private static LevelData CreateLevel(params LevelCellObjectData[] cellObjects)
        {
            LevelBlockData[] blocks =
            {
                new LevelBlockData("b0", "Red", Vector2Int.zero, new[] { Vector2Int.zero }),
                new LevelBlockData("b1", "Blue", new Vector2Int(5, 3), new[] { Vector2Int.zero })
            };
            return new LevelData("barriers", "Barriers", 6, 4, 60, Array.Empty<Vector2Int>(), Array.Empty<LevelDoorData>(),
                blocks, cellObjects);
        }

        private static LevelCellObjectData Barrier(string id, bool startsOpen, params Vector2Int[] cells)
        {
            return new LevelCellObjectData(id, cells, new LevelEffectData("barrier",
                new[] { new LevelEffectParamData("startsOpen", startsOpen ? "true" : "false") }));
        }
    }
}
