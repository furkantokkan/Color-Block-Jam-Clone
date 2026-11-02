using System;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Effect Editor commands (TASK-20260925-100308276-f4a07e8a): block effects through the v2 registry,
    /// the Barrier's optional startsOpen, the one-effect rule of board objects and the gate span, each refusing with the
    /// validation's or the canvas's message and keeping every other field. Levels live only in memory.
    /// </summary>
    public sealed class LevelEffectCommandsTests
    {
        private const LevelEffectTarget k_block = LevelEffectTarget.Block;
        private const LevelEffectTarget k_door = LevelEffectTarget.Door;
        private const LevelEffectTarget k_object = LevelEffectTarget.CellObject;

        // No gate effect type is registered (DEC-177); a gate still keeps an effect list, which the span and remove
        // commands copy without validating, so a placeholder type stands in for one.
        private const string k_gateEffectType = "sample-gate-effect";

        /// <summary>
        /// A 5 x 6 level with a void top-right corner, gates d1 (Bottom 1-2), d2 (Bottom 3) and d3 (Top 0), a Red domino
        /// b1 at (1,1), a Blue single b2 with Ice at (1,3), a closed barrier-1 without params at (3,2) and an open
        /// barrier-2 under b2 at (1,3) and (2,3).
        /// </summary>
        internal static LevelData CreateLevel(params LevelEffectData[] d3Effects)
        {
            return LevelTestData.EffectEditorFixture(d3Effects);
        }

        // Kept as a forwarding wrapper: LevelEffectEditorTests.cs still calls this string[] shape. It converts the
        // pairs and forwards to the one canonical builder, LevelTestData.Effect.
        internal static LevelEffectData Effect(string type, params string[] keysAndValues)
        {
            (string Key, string Value)[] pairs = new (string, string)[keysAndValues.Length / 2];
            for (int index = 0; index < pairs.Length; index++)
            {
                pairs[index] = (keysAndValues[index * 2], keysAndValues[index * 2 + 1]);
            }

            return LevelTestData.Effect(type, pairs);
        }

        [Test]
        public void TheFixtureIsAValidLevel()
        {
            Assert.That(CreateLevel().Validate(out string error), Is.True, error);
        }

        [Test]
        public void ANewEffectStartsWithItsRequiredKeysAtTheirDefaults()
        {
            LevelEffectData arrow = LevelEffectCommands.CreateEffect("arrow");
            Assert.That(arrow.ParamCount, Is.EqualTo(1));
            Assert.That(arrow.FindValue("axis"), Is.EqualTo("Horizontal"), "The first enum value.");
            Assert.That(LevelEffectCommands.CreateEffect("ice").FindValue("clearsToThaw"), Is.EqualTo("1"), "The minimum.");
            Assert.That(LevelEffectCommands.CreateEffect("barrier").ParamCount, Is.Zero, "startsOpen is optional.");
        }

        [Test]
        public void TheEffectEditorAuthorsArrowIceAndTheBarrierOnly()
        {
            Assert.That(LevelEffectCommands.IsEditable(k_block, "arrow"), Is.True);
            Assert.That(LevelEffectCommands.IsEditable(k_block, "ice"), Is.True);
            Assert.That(LevelEffectCommands.IsEditable(k_door, "ice"), Is.False, "No gate effect exists (DEC-177).");
            Assert.That(LevelEffectCommands.IsEditable(k_object, "barrier"), Is.True);
            Assert.That(LevelEffectCommands.IsEditable(k_object, "crate"), Is.False);
            Assert.That(LevelEffectCommands.IsEditable(k_block, "barrier"), Is.False, "A target keeps its own types.");
        }

        [Test]
        public void AddingArrowAppendsItAndKeepsEveryOtherField()
        {
            LevelData level = CreateLevel();

            Assert.That(LevelEffectCommands.TryAddEffect(level, k_block, 0, "arrow", out LevelData result, out string issue),
                Is.True, issue);

            Assert.That(level.GetBlockAt(0).EffectCount, Is.Zero, "The source level stays unchanged.");
            LevelBlockData block = result.GetBlockAt(0);
            Assert.That(block.EffectCount, Is.EqualTo(1));
            Assert.That(block.GetEffectAt(0).Type, Is.EqualTo("arrow"));
            Assert.That(block.GetEffectAt(0).FindValue("axis"), Is.EqualTo("Horizontal"));
            Assert.That(block.Id, Is.EqualTo("b1"));
            Assert.That(block.ColorId, Is.EqualTo("Red"));
            Assert.That(block.Key, Is.EqualTo(LevelTestShapes.Key(new[] { Vector2Int.zero, Vector2Int.right })));
            Assert.That(block.Origin, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(LevelRecordCopy.GetCells(block), Is.EqualTo(new[] { Vector2Int.zero, Vector2Int.right }));
            AssertOthersKept(level, result, k_block, 0);
            Assert.That(result.Validate(out string error), Is.True, error);
        }

        [Test]
        public void ADoubledOrUnknownEffectIsRefusedWithTheValidationMessage()
        {
            LevelData level = CreateLevel();
            LevelEffectCommands.TryAddEffect(level, k_block, 0, "arrow", out LevelData arrowed, out _);

            Assert.That(LevelEffectCommands.TryAddEffect(arrowed, k_block, 0, "arrow", out LevelData result,
                out string issue), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(issue, Is.EqualTo("Level L1 block b1 lists effect arrow twice."));

            Assert.That(LevelEffectCommands.TryAddEffect(level, k_block, 0, "no-such-effect", out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Level L1 block b1 effect no-such-effect is not a known effect type."));
        }

        [Test]
        public void SettingAParamReplacesItInPlaceAndRefusesAValueOutsideItsRule()
        {
            LevelData level = CreateLevel();

            Assert.That(LevelEffectCommands.TrySetParam(level, k_block, 1, 0, "clearsToThaw", "5", out LevelData result,
                out string issue), Is.True, issue);
            Assert.That(result.GetBlockAt(1).GetEffectAt(0).FindValue("clearsToThaw"), Is.EqualTo("5"));
            Assert.That(result.GetBlockAt(1).GetEffectAt(0).ParamCount, Is.EqualTo(1), "Replaced, not appended.");
            AssertOthersKept(level, result, k_block, 1);

            Assert.That(LevelEffectCommands.TrySetParam(level, k_block, 1, 0, "clearsToThaw", "25", out result, out issue),
                Is.False);
            Assert.That(result, Is.Null);
            Assert.That(issue, Is.EqualTo(
                "Level L1 block b2 effect ice key clearsToThaw has value '25'; expected an integer from 1 to 20."));

            Assert.That(LevelEffectCommands.TrySetParam(level, k_block, 1, 0, "clearsToThaw", "2", out result, out issue),
                Is.False, "The same value changes nothing.");
            Assert.That(issue, Is.Null);
        }

        [Test]
        public void ARequiredParamCannotBeRemovedAndAnAbsentOneChangesNothing()
        {
            LevelEffectCommands.TryAddEffect(CreateLevel(), k_block, 0, "arrow", out LevelData arrowed, out _);

            Assert.That(LevelEffectCommands.TryRemoveParam(arrowed, k_block, 0, 0, "axis", out LevelData result,
                out string issue), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(issue, Is.EqualTo("Level L1 block b1 effect arrow is missing required key axis."));

            Assert.That(LevelEffectCommands.TryRemoveParam(arrowed, k_block, 0, 0, "startsOpen", out _, out issue),
                Is.False);
            Assert.That(issue, Is.Null);
        }

        [Test]
        public void RemovingAnEffectKeepsTheOthersInOrder()
        {
            LevelData level = CreateLevel();
            LevelEffectCommands.TryAddEffect(level, k_block, 1, "arrow", out LevelData both, out _);
            Assert.That(both.GetBlockAt(1).EffectCount, Is.EqualTo(2));

            Assert.That(LevelEffectCommands.TryRemoveEffect(both, k_block, 1, 0, out LevelData result, out string issue),
                Is.True, issue);

            Assert.That(result.GetBlockAt(1).EffectCount, Is.EqualTo(1));
            Assert.That(result.GetBlockAt(1).GetEffectAt(0).Type, Is.EqualTo("arrow"));
            Assert.That(LevelEffectCommands.TryRemoveEffect(both, k_block, 1, 2, out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Select an effect first."));
        }

        [Test]
        public void TheBarrierStartsOpenParamIsAddedChangedAndRemoved()
        {
            LevelData level = CreateLevel();
            Assert.That(LevelEffectCommands.TrySetParam(level, k_object, 0, 0, "startsOpen", "false", out _,
                out string issue), Is.False, "An absent startsOpen already holds the default, closed.");
            Assert.That(issue, Is.Null);

            Assert.That(LevelEffectCommands.TrySetParam(level, k_object, 0, 0, "startsOpen", "true", out LevelData added,
                out issue), Is.True, issue);
            // The Barrier's own command makes the change.
            LevelBarrierCommands.TrySetStartsOpen(level, 0, true, out LevelData viaBarrier, out _);
            Assert.That(JsonUtility.ToJson(added), Is.EqualTo(JsonUtility.ToJson(viaBarrier)));
            LevelCellObjectData barrier = added.GetCellObjectAt(0);
            Assert.That(barrier.Effect.ParamCount, Is.EqualTo(1));
            Assert.That(barrier.Effect.FindValue("startsOpen"), Is.EqualTo("true"));
            Assert.That(barrier.Id, Is.EqualTo("barrier-1"));
            Assert.That(LevelRecordCopy.GetCells(barrier), Is.EqualTo(new[] { new Vector2Int(3, 2) }));
            AssertOthersKept(level, added, k_object, 0);

            Assert.That(LevelEffectCommands.TrySetParam(added, k_object, 0, 0, "startsOpen", "false", out LevelData changed,
                out issue), Is.True, issue);
            Assert.That(changed.GetCellObjectAt(0).Effect.FindValue("startsOpen"), Is.EqualTo("false"));

            Assert.That(LevelEffectCommands.TryRemoveParam(changed, k_object, 0, 0, "startsOpen", out LevelData removed,
                out issue), Is.True, issue);
            Assert.That(removed.GetCellObjectAt(0).Effect.ParamCount, Is.Zero, "Back to the default: closed.");
            Assert.That(LevelEffectCommands.TryRemoveParam(removed, k_object, 0, 0, "startsOpen", out _, out issue),
                Is.False);
            Assert.That(issue, Is.Null);

            Assert.That(LevelEffectCommands.TrySetParam(level, k_object, 0, 0, "startsOpen", "yes", out _, out issue),
                Is.False);
            Assert.That(issue, Is.EqualTo(
                "Level L1 cell object barrier-1 effect barrier key startsOpen has value 'yes'; expected true or false."));
        }

        [Test]
        public void ClosingABarrierOverABlockIsRefusedWithTheValidationMessage()
        {
            LevelData level = CreateLevel();
            const string closedOnBlock =
                "Level L1 cell object barrier-2 effect barrier starts closed on block b2 at (1, 3).";

            Assert.That(LevelEffectCommands.TrySetParam(level, k_object, 1, 0, "startsOpen", "false", out LevelData result,
                out string issue), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(issue, Is.EqualTo(closedOnBlock));

            Assert.That(LevelEffectCommands.TryRemoveParam(level, k_object, 1, 0, "startsOpen", out result, out issue),
                Is.False, "Without startsOpen the barrier starts closed.");
            Assert.That(issue, Is.EqualTo(closedOnBlock));
        }

        [Test]
        public void ABoardObjectKeepsExactlyOneEffect()
        {
            LevelData level = CreateLevel();

            Assert.That(LevelEffectCommands.TryAddEffect(level, k_object, 0, "crate", out _, out string issue), Is.False);
            Assert.That(issue, Is.EqualTo(LevelEffectCommands.k_objectEffectIssue));
            Assert.That(LevelEffectCommands.TryRemoveEffect(level, k_object, 0, 0, out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo(LevelEffectCommands.k_objectEffectIssue));
            Assert.That(LevelEffectCommands.TrySetParam(level, k_object, 5, 0, "startsOpen", "true", out _, out issue),
                Is.False);
            Assert.That(issue, Is.EqualTo("Select a board object first."));
        }

        [Test]
        public void AnUnrelatedAuthoredProblemDoesNotBlockAnEdit()
        {
            LevelData level = CreateLevel();
            LevelEffectCommands.TryAddEffect(level, k_block, 0, "arrow", out LevelData arrowed, out _);
            // b2's Ice written out of range by hand, as a JSON file could hold it.
            LevelBlockData[] blocks = LevelRecordCopy.GetBlocks(arrowed);
            blocks[1] = LevelRecordCopy.WithEffects(blocks[1], new[] { Effect("ice", "clearsToThaw", "0") });
            LevelData broken = LevelRecordCopy.WithBlocks(arrowed, blocks);
            Assert.That(broken.Validate(out _), Is.False);

            Assert.That(LevelEffectCommands.TrySetParam(broken, k_block, 0, 0, "axis", "Vertical", out LevelData result,
                out string issue), Is.True, issue);
            Assert.That(result.GetBlockAt(0).GetEffectAt(0).FindValue("axis"), Is.EqualTo("Vertical"));
            Assert.That(result.GetBlockAt(1).GetEffectAt(0).FindValue("clearsToThaw"), Is.EqualTo("0"), "Kept as it was.");
        }

        [Test]
        public void AGateSpanMovesAlongItsSideAndRefusesTheCanvasCases()
        {
            LevelData level = CreateLevel();

            Assert.That(LevelEffectCommands.TrySetDoorSpan(level, 0, 0, 1, out LevelData result, out string issue),
                Is.True, issue);
            LevelDoorData door = result.GetDoorAt(0);
            Assert.That(door.Start, Is.EqualTo(0));
            Assert.That(door.Length, Is.EqualTo(1));
            Assert.That(door.Id, Is.EqualTo("d1"));
            Assert.That(door.Side, Is.EqualTo(BoardSide.Bottom));
            Assert.That(door.ColorId, Is.EqualTo("Red"));
            Assert.That(result.Validate(out string error), Is.True, error);

            Assert.That(LevelEffectCommands.TrySetDoorSpan(level, 0, 1, 4, out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Gate d1 would span 4 cells; gates span 1 to 3."));
            Assert.That(LevelEffectCommands.TrySetDoorSpan(level, 0, 1, 3, out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("The Bottom edge at 3 already holds door d2."));
            Assert.That(LevelEffectCommands.TrySetDoorSpan(level, 1, 4, 2, out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Segment 5 is off the Bottom edge."));
            Assert.That(LevelEffectCommands.TrySetDoorSpan(level, 2, 4, 1, out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Cell (4,5) behind the Top edge at 4 is a void."));
            Assert.That(LevelEffectCommands.TrySetDoorSpan(level, 0, 1, 2, out result, out issue), Is.False,
                "An unchanged span changes nothing.");
            Assert.That(issue, Is.Null);

            // The gate keeps its effect list when it moves; the move copies the list without validating it.
            LevelData withEffect = CreateLevel(Effect(k_gateEffectType, "startsOpen", "true"));
            Assert.That(LevelEffectCommands.TrySetDoorSpan(withEffect, 2, 1, 2, out result, out issue), Is.True, issue);
            Assert.That(result.GetDoorAt(2).EffectCount, Is.EqualTo(1));
            Assert.That(result.GetDoorAt(2).GetEffectAt(0).FindValue("startsOpen"), Is.EqualTo("true"));
        }

        [Test]
        public void GateEffectsCanBeRemoved()
        {
            LevelData level = CreateLevel(Effect(k_gateEffectType, "startsOpen", "true"));

            Assert.That(LevelEffectCommands.TryRemoveEffect(level, k_door, 2, 0, out LevelData result, out string issue),
                Is.True, issue);

            Assert.That(result.GetDoorAt(2).EffectCount, Is.Zero);
            Assert.That(result.GetDoorAt(2).Start, Is.EqualTo(0));
            AssertOthersKept(level, result, k_door, 2);
        }

        // Every record other than the edited one is the same as before.
        private static void AssertOthersKept(LevelData before, LevelData after, LevelEffectTarget target, int index)
        {
            Assert.That(after.Id, Is.EqualTo(before.Id));
            Assert.That(after.Width, Is.EqualTo(before.Width));
            Assert.That(after.Height, Is.EqualTo(before.Height));
            Assert.That(after.TimerSeconds, Is.EqualTo(before.TimerSeconds));
            Assert.That(LevelRecordCopy.GetVoidCells(after), Is.EqualTo(LevelRecordCopy.GetVoidCells(before)));
            Assert.That(after.BlockCount, Is.EqualTo(before.BlockCount));
            Assert.That(after.DoorCount, Is.EqualTo(before.DoorCount));
            Assert.That(after.CellObjectCount, Is.EqualTo(before.CellObjectCount));
            for (int block = 0; block < before.BlockCount; block++)
            {
                if (target != LevelEffectTarget.Block || block != index)
                {
                    Assert.That(JsonUtility.ToJson(after.GetBlockAt(block)),
                        Is.EqualTo(JsonUtility.ToJson(before.GetBlockAt(block))));
                }
            }

            for (int door = 0; door < before.DoorCount; door++)
            {
                if (target != LevelEffectTarget.Door || door != index)
                {
                    Assert.That(JsonUtility.ToJson(after.GetDoorAt(door)),
                        Is.EqualTo(JsonUtility.ToJson(before.GetDoorAt(door))));
                }
            }

            for (int item = 0; item < before.CellObjectCount; item++)
            {
                if (target != LevelEffectTarget.CellObject || item != index)
                {
                    Assert.That(JsonUtility.ToJson(after.GetCellObjectAt(item)),
                        Is.EqualTo(JsonUtility.ToJson(before.GetCellObjectAt(item))));
                }
            }
        }
    }
}
