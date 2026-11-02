using System;
using System.Collections.Generic;
using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.Validation
{
    /// <summary>
    /// The playback frames of a solution: one frame per step, slides move origins, exits remove blocks, a frozen
    /// block's Ice count follows the exits and its Ice is gone at 0, everything else is copied unchanged, the last
    /// frame of a full solution has no blocks, and the level itself never changes.
    /// </summary>
    public sealed class LevelSolutionFramesTests
    {
        private const string k_iceCountKey = "clearsToThaw";

        [Test]
        public void EachStepGivesOneFrameThatMovesItsBlockOrRemovesIt()
        {
            LevelData level = ValidationFixtures.TwoOrdersOfDifferentLength();
            LevelCheckResult listed = LevelSolvabilityCheck.RunAndList(level, null, null, null);
            Assert.That(listed.Solutions.Count, Is.EqualTo(2), listed.SummaryText);

            for (int i = 0; i < listed.Solutions.Count; i++)
            {
                IReadOnlyList<LevelSolutionStep> steps = listed.Solutions[i].Steps;

                IReadOnlyList<LevelData> frames = LevelSolutionFrames.Create(level, steps);

                AssertFramesFollowSteps(level, steps, frames, "#" + (i + 1));
            }

            // Orange (b2) steps up and leaves, then Red (b1) steps right and leaves.
            IReadOnlyList<LevelData> first = LevelSolutionFrames.Create(level, listed.Solutions[0].Steps);
            Assert.That(first.Count, Is.EqualTo(4));
            AssertBlocks(first[0], ("b1", new Vector2Int(1, 1)), ("b2", new Vector2Int(2, 2)));
            AssertBlocks(first[1], ("b1", new Vector2Int(1, 1)));
            AssertBlocks(first[2], ("b1", new Vector2Int(2, 1)));
            AssertBlocks(first[3]);
        }

        [Test]
        public void AFrozenBlocksCountFollowsTheExitsAndItsIceIsGoneAtZero()
        {
            LevelData level = ValidationFixtures.IceThawedByTwoExits();
            LevelCheckResult verdict = LevelSolvabilityCheck.Run(level);
            Assert.That(verdict.Status, Is.EqualTo(LevelCheckStatus.Solvable), verdict.Text);

            IReadOnlyList<LevelData> frames = LevelSolutionFrames.Create(level, verdict.Steps);

            AssertFramesFollowSteps(level, verdict.Steps, frames, "ice");

            // Orange and Yellow leave first: Red's clearsToThaw 2 reads 1 after one exit and is gone after two.
            Assert.That(frames.Count, Is.EqualTo(4));
            LevelBlockData afterOneExit = FindBlock(frames[0], "b1");
            Assert.That(afterOneExit.TryGetEffect(out IceEffect ice), Is.True);
            Assert.That(ice.ClearsToThaw, Is.EqualTo(1));
            Assert.That(afterOneExit.GetEffectAt(0).FindValue(k_iceCountKey), Is.EqualTo("1"));
            LevelBlockData afterTwoExits = FindBlock(frames[1], "b1");
            Assert.That(afterTwoExits.EffectCount, Is.Zero);
            Assert.That(afterTwoExits.TryGetEffect(out IceEffect _), Is.False);
            Assert.That(FindBlock(frames[2], "b1").EffectCount, Is.Zero);
        }

        [Test]
        public void ArrowEffectsVoidCellsAndDoorsStayInEveryFrame()
        {
            LevelData level = ValidationFixtures.ArrowBlockingColumn("Horizontal");
            LevelCheckResult verdict = LevelSolvabilityCheck.Run(level);
            Assert.That(verdict.Status, Is.EqualTo(LevelCheckStatus.Solvable), verdict.Text);
            Assert.That(level.VoidCellCount, Is.GreaterThan(0), "the fixture has void cells to copy");

            IReadOnlyList<LevelData> frames = LevelSolutionFrames.Create(level, verdict.Steps);

            AssertFramesFollowSteps(level, verdict.Steps, frames, "arrow");

            // The Orange Arrow block (b2) steps right before it leaves; its marker axis stays.
            LevelBlockData arrowBlock = FindBlock(frames[0], "b2");
            Assert.That(arrowBlock.Origin, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(arrowBlock.TryGetEffect(out ArrowEffect arrow), Is.True);
            Assert.That(arrow.Axis, Is.EqualTo(LevelEffectAxis.Horizontal));
        }

        [Test]
        public void DoorsAndBoardObjectsAreCopiedToEveryFrame()
        {
            LevelData source = ValidationFixtures.SingleBlockToTopGate();
            LevelEffectData barrier = new LevelEffectData("barrier", new LevelEffectParamData[0]);
            LevelData level = new LevelData(source.Id, source.Name, source.Width, source.Height, source.TimerSeconds,
                new Vector2Int[0], new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red") },
                new[] { source.GetBlockAt(0) },
                new[] { new LevelCellObjectData("o1", new[] { new Vector2Int(3, 0) }, barrier) });
            Assert.That(level.Validate(out string error), Is.True, error);
            LevelSolutionStep[] steps =
            {
                new LevelSolutionStep(0, 1, 2, 0, 2, false, BoardSide.Top),
                new LevelSolutionStep(0, 0, 2, 0, 3, false, BoardSide.Top),
                new LevelSolutionStep(0, 0, 3, 0, 3, true, BoardSide.Top)
            };

            IReadOnlyList<LevelData> frames = LevelSolutionFrames.Create(level, steps);

            AssertFramesFollowSteps(level, steps, frames, "objects");
            Assert.That(frames[2].BlockCount, Is.Zero);
            Assert.That(frames[2].DoorCount, Is.EqualTo(1));
            Assert.That(frames[2].GetDoorAt(0).Id, Is.EqualTo("d1"));
            Assert.That(frames[2].GetCellObjectAt(0).Effect.Type, Is.EqualTo("barrier"));
        }

        [Test]
        public void TheLevelNeverChanges()
        {
            LevelData[] levels =
            {
                ValidationFixtures.TwoOrdersOfDifferentLength(), ValidationFixtures.IceThawedByTwoExits(),
                ValidationFixtures.ArrowBlockingColumn("Horizontal")
            };
            for (int i = 0; i < levels.Length; i++)
            {
                LevelCheckResult listed = LevelSolvabilityCheck.RunAndList(levels[i], null, null, null);
                Assert.That(LevelDatabaseJson.TryWrite(new[] { levels[i] }, out string before, out string error), Is.True,
                    error);

                for (int e = 0; e < listed.Solutions.Count; e++)
                {
                    LevelSolutionFrames.Create(levels[i], listed.Solutions[e].Steps);
                }

                Assert.That(LevelDatabaseJson.TryWrite(new[] { levels[i] }, out string after, out error), Is.True, error);
                Assert.That(after, Is.EqualTo(before), "#" + i);
            }
        }

        [Test]
        public void NoStepsGiveNoFramesAndAStepForABlockOffTheBoardIsRefused()
        {
            LevelData level = ValidationFixtures.SingleBlockToTopGate();
            LevelSolutionStep exit = new LevelSolutionStep(0, 0, 3, 0, 3, true, BoardSide.Top);

            Assert.That(LevelSolutionFrames.Create(level, new LevelSolutionStep[0]), Is.Empty);
            Assert.That(() => LevelSolutionFrames.Create(level, new[] { exit, exit }), Throws.ArgumentException,
                "the block already left");
            Assert.That(() => LevelSolutionFrames.Create(level, new[] { new LevelSolutionStep(1, 0, 0, 1, 0, false,
                BoardSide.Top) }), Throws.ArgumentException, "the level has one block");
            Assert.That(() => LevelSolutionFrames.Create(null, new LevelSolutionStep[0]), Throws.ArgumentNullException);
            Assert.That(() => LevelSolutionFrames.Create(level, null), Throws.ArgumentNullException);
        }

        // Replays the steps on plain origins and checks every frame: the same board, the blocks still on the board in
        // authored order at their origins with colour, shape and cells, their effects with Ice lowered by the exits so
        // far, and a valid level whenever a block remains. The last frame of a full solution has no blocks.
        private static void AssertFramesFollowSteps(LevelData level, IReadOnlyList<LevelSolutionStep> steps,
            IReadOnlyList<LevelData> frames, string label)
        {
            Assert.That(frames.Count, Is.EqualTo(steps.Count), label);
            Vector2Int[] origins = new Vector2Int[level.BlockCount];
            bool[] exited = new bool[level.BlockCount];
            for (int block = 0; block < origins.Length; block++)
            {
                origins[block] = level.GetBlockAt(block).Origin;
            }

            int exitCount = 0;
            for (int i = 0; i < steps.Count; i++)
            {
                LevelSolutionStep step = steps[i];
                if (step.IsExit)
                {
                    exited[step.BlockIndex] = true;
                    exitCount++;
                }
                else
                {
                    origins[step.BlockIndex] = new Vector2Int(step.ToX, step.ToY);
                }

                LevelData frame = frames[i];
                string frameLabel = label + " frame " + i;
                AssertSameBoard(level, frame, frameLabel);
                int next = 0;
                for (int block = 0; block < level.BlockCount; block++)
                {
                    if (exited[block])
                    {
                        continue;
                    }

                    Assert.That(next, Is.LessThan(frame.BlockCount), frameLabel);
                    AssertBlock(level.GetBlockAt(block), origins[block], exitCount, frame.GetBlockAt(next++), frameLabel);
                }

                Assert.That(frame.BlockCount, Is.EqualTo(next), frameLabel);
                if (frame.BlockCount > 0)
                {
                    Assert.That(frame.Validate(out string error), Is.True, frameLabel + ": " + error);
                }
            }

            Assert.That(exitCount, Is.EqualTo(level.BlockCount), label + ": a full solution");
            Assert.That(frames[frames.Count - 1].BlockCount, Is.Zero, label + ": the cleared board");
        }

        private static void AssertSameBoard(LevelData expected, LevelData actual, string label)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id), label);
            Assert.That(actual.Name, Is.EqualTo(expected.Name), label);
            Assert.That(actual.Width, Is.EqualTo(expected.Width), label);
            Assert.That(actual.Height, Is.EqualTo(expected.Height), label);
            Assert.That(actual.TimerSeconds, Is.EqualTo(expected.TimerSeconds), label);
            Assert.That(actual.VoidCellCount, Is.EqualTo(expected.VoidCellCount), label);
            for (int i = 0; i < expected.VoidCellCount; i++)
            {
                Assert.That(actual.GetVoidCellAt(i), Is.EqualTo(expected.GetVoidCellAt(i)), label);
            }

            Assert.That(actual.DoorCount, Is.EqualTo(expected.DoorCount), label);
            for (int i = 0; i < expected.DoorCount; i++)
            {
                LevelDoorData want = expected.GetDoorAt(i);
                LevelDoorData got = actual.GetDoorAt(i);
                Assert.That(got, Is.Not.SameAs(want), label + ": frames hold copies");
                Assert.That(got.Id, Is.EqualTo(want.Id), label);
                Assert.That(got.Side, Is.EqualTo(want.Side), label);
                Assert.That(got.Start, Is.EqualTo(want.Start), label);
                Assert.That(got.Length, Is.EqualTo(want.Length), label);
                Assert.That(got.ColorId, Is.EqualTo(want.ColorId), label);
                Assert.That(got.EffectCount, Is.EqualTo(want.EffectCount), label);
                for (int e = 0; e < want.EffectCount; e++)
                {
                    AssertSameEffect(want.GetEffectAt(e), got.GetEffectAt(e), label);
                }
            }

            Assert.That(actual.CellObjectCount, Is.EqualTo(expected.CellObjectCount), label);
            for (int i = 0; i < expected.CellObjectCount; i++)
            {
                LevelCellObjectData want = expected.GetCellObjectAt(i);
                LevelCellObjectData got = actual.GetCellObjectAt(i);
                Assert.That(got.Id, Is.EqualTo(want.Id), label);
                Assert.That(got.CellCount, Is.EqualTo(want.CellCount), label);
                for (int c = 0; c < want.CellCount; c++)
                {
                    Assert.That(got.GetCellAt(c), Is.EqualTo(want.GetCellAt(c)), label);
                }

                AssertSameEffect(want.Effect, got.Effect, label);
            }
        }

        // A block keeps its id, colour, shape, cells and effects; only its origin moves and its Ice count follows exits.
        private static void AssertBlock(LevelBlockData source, Vector2Int origin, int exitCount, LevelBlockData actual,
            string label)
        {
            string blockLabel = label + " " + source.Id;
            Assert.That(actual.Id, Is.EqualTo(source.Id), blockLabel);
            Assert.That(actual.ColorId, Is.EqualTo(source.ColorId), blockLabel);
            Assert.That(actual.Key, Is.EqualTo(source.Key), blockLabel);
            Assert.That(actual.Origin, Is.EqualTo(origin), blockLabel);
            Assert.That(actual.CellCount, Is.EqualTo(source.CellCount), blockLabel);
            for (int c = 0; c < source.CellCount; c++)
            {
                Assert.That(actual.GetCellAt(c), Is.EqualTo(source.GetCellAt(c)), blockLabel);
            }

            int next = 0;
            for (int e = 0; e < source.EffectCount; e++)
            {
                LevelEffectData effect = source.GetEffectAt(e);
                if (!(effect.Parameters is IceEffect ice))
                {
                    AssertSameEffect(effect, actual.GetEffectAt(next++), blockLabel);
                    continue;
                }

                int remaining = ice.ClearsToThaw - exitCount;
                if (remaining <= 0)
                {
                    continue;
                }

                LevelEffectData lowered = actual.GetEffectAt(next++);
                Assert.That(lowered.Type, Is.EqualTo(effect.Type), blockLabel);
                Assert.That(lowered.Parameters, Is.InstanceOf<IceEffect>(), blockLabel);
                Assert.That(((IceEffect)lowered.Parameters).ClearsToThaw, Is.EqualTo(remaining), blockLabel);
                Assert.That(remaining, Is.InRange(1, 20), blockLabel);
            }

            Assert.That(actual.EffectCount, Is.EqualTo(next), blockLabel);
        }

        private static void AssertSameEffect(LevelEffectData expected, LevelEffectData actual, string label)
        {
            Assert.That(actual.Type, Is.EqualTo(expected.Type), label);
            Assert.That(actual.ParamCount, Is.EqualTo(expected.ParamCount), label);
            for (int p = 0; p < expected.ParamCount; p++)
            {
                Assert.That(actual.GetParamAt(p).Key, Is.EqualTo(expected.GetParamAt(p).Key), label);
                Assert.That(actual.GetParamAt(p).Value, Is.EqualTo(expected.GetParamAt(p).Value), label);
            }
        }

        // The frame's blocks in authored order: each id at its origin.
        private static void AssertBlocks(LevelData frame, params (string Id, Vector2Int Origin)[] blocks)
        {
            Assert.That(frame.BlockCount, Is.EqualTo(blocks.Length));
            for (int i = 0; i < blocks.Length; i++)
            {
                Assert.That(frame.GetBlockAt(i).Id, Is.EqualTo(blocks[i].Id));
                Assert.That(frame.GetBlockAt(i).Origin, Is.EqualTo(blocks[i].Origin), blocks[i].Id);
            }
        }

        private static LevelBlockData FindBlock(LevelData frame, string id)
        {
            for (int i = 0; i < frame.BlockCount; i++)
            {
                if (string.Equals(frame.GetBlockAt(i).Id, id, StringComparison.Ordinal))
                {
                    return frame.GetBlockAt(i);
                }
            }

            Assert.Fail("Block " + id + " is not in the frame.");
            return null;
        }
    }
}
