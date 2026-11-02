using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// Proves where exit fragments start (DEC-131): on the exit clip plane's door line, centred across
    /// the lanes the block covers along the door side, facing out of the board, on all four sides.
    /// </summary>
    public sealed class ExitFragmentPlacementTests
    {
        private const float k_tolerance = 1e-5f;

        // 6 x 4. A (block 0) is a 2 x 2 square at the top, B (1) a vertical pair on the left edge,
        // C (2) an L along the bottom and D (3) a vertical pair on the right edge.
        private static readonly string[] s_map =
        {
            "B..AA.",
            "B..AA.",
            "C....D",
            "CCC..D"
        };

        [Test]
        public void TheDoorLineIsTheFloorEdgePlusTheClipOffsetOutward()
        {
            LevelModel model = BoardFixture.Create(s_map);
            float offset = ExitMotionSettings.k_clipOffsetCells;

            Assert.That(ExitFragmentPlacement.GetDoorLine(model, Side.Top), Is.EqualTo(3.5f + offset).Within(k_tolerance));
            Assert.That(ExitFragmentPlacement.GetDoorLine(model, Side.Bottom), Is.EqualTo(-0.5f - offset).Within(k_tolerance));
            Assert.That(ExitFragmentPlacement.GetDoorLine(model, Side.Left), Is.EqualTo(-0.5f - offset).Within(k_tolerance));
            Assert.That(ExitFragmentPlacement.GetDoorLine(model, Side.Right), Is.EqualTo(5.5f + offset).Within(k_tolerance));
        }

        [Test]
        public void ATopExitStartsOnTheTopDoorLineAcrossTheBlocksColumns()
        {
            LevelModel model = BoardFixture.Create(s_map);

            ExitFragmentPlacement placement = ExitFragmentPlacement.Create(model, 0, Side.Top, model.GetBlock(0).Origin);

            AssertPlacement(placement, 3.5f, ExitFragmentPlacement.GetDoorLine(model, Side.Top), Vector3.forward, 2);
        }

        [Test]
        public void ALeftExitStartsOnTheLeftDoorLineAcrossTheBlocksRows()
        {
            LevelModel model = BoardFixture.Create(s_map);

            ExitFragmentPlacement placement = ExitFragmentPlacement.Create(model, 1, Side.Left, model.GetBlock(1).Origin);

            AssertPlacement(placement, ExitFragmentPlacement.GetDoorLine(model, Side.Left), 2.5f, Vector3.left, 2);
        }

        [Test]
        public void AnLShapedBottomExitCoversTheBlocksWholeWidth()
        {
            LevelModel model = BoardFixture.Create(s_map);

            ExitFragmentPlacement placement = ExitFragmentPlacement.Create(model, 2, Side.Bottom, model.GetBlock(2).Origin);

            AssertPlacement(placement, 1f, ExitFragmentPlacement.GetDoorLine(model, Side.Bottom), Vector3.back, 3);
        }

        [Test]
        public void ARightExitStartsOnTheRightDoorLineAcrossTheBlocksRows()
        {
            LevelModel model = BoardFixture.Create(s_map);

            ExitFragmentPlacement placement = ExitFragmentPlacement.Create(model, 3, Side.Right, model.GetBlock(3).Origin);

            AssertPlacement(placement, ExitFragmentPlacement.GetDoorLine(model, Side.Right), 0.5f, Vector3.right, 2);
        }

        [Test]
        public void TheLanesFollowTheExitCellNotTheAuthoredOrigin()
        {
            LevelModel model = BoardFixture.Create(s_map);

            // The square moved two columns left before it left through the top.
            ExitFragmentPlacement placement = ExitFragmentPlacement.Create(model, 0, Side.Top, new Cell(1, 2));

            AssertPlacement(placement, 1.5f, ExitFragmentPlacement.GetDoorLine(model, Side.Top), Vector3.forward, 2);
        }

        [Test]
        public void TheLanesCountTheFootprintAroundAnOriginInsideIt()
        {
            // A T whose origin is its middle cell, so its cells run from one below and one left of the origin.
            BlockDefinition block = new BlockDefinition(0, new BlockColor(0), new Cell(2, 1),
                new[] { new Cell(-1, 0), new Cell(0, 0), new Cell(1, 0), new Cell(0, -1) });
            bool[] floor = new bool[5 * 3];
            for (int i = 0; i < floor.Length; i++)
            {
                floor[i] = true;
            }

            Assert.That(LevelModel.TryCreate(5, 3, floor, new DoorDefinition[0], new[] { block }, 60,
                out LevelModel model, out string error), Is.True, error);

            AssertPlacement(ExitFragmentPlacement.Create(model, 0, Side.Top, block.Origin),
                2f, ExitFragmentPlacement.GetDoorLine(model, Side.Top), Vector3.forward, 3);
            AssertPlacement(ExitFragmentPlacement.Create(model, 0, Side.Right, block.Origin),
                ExitFragmentPlacement.GetDoorLine(model, Side.Right), 0.5f, Vector3.right, 2);
        }

        [Test]
        public void ABlockThatLostAnEndCellEmitsAcrossItsRemainingLanes()
        {
            // A horizontal line of three on the bottom row; Rocket removes its right end (B-19).
            BoardSimulation simulation = CreateLineSimulation();
            Assert.That(simulation.Commands.Execute(GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(2, 0))),
                Is.EqualTo(CommandOutcome.Applied));
            IBoardReader board = simulation.Preview;

            ExitFragmentPlacement placement = ExitFragmentPlacement.Create(board, 0, Side.Bottom, board.GetOrigin(0));

            AssertPlacement(placement, 0.5f, ExitFragmentPlacement.GetDoorLine(board.Model, Side.Bottom), Vector3.back, 2);
            Assert.That(ExitFragmentPlacement.Create(board.Model, 0, Side.Bottom, board.GetOrigin(0)).Lanes, Is.EqualTo(3),
                "the authored overload still reads the authored footprint");
        }

        [Test]
        public void ARocketPartEmitsAcrossItsOwnLanesFromTheSharedOrigin()
        {
            // Removing the middle leaves the parent's left cell and a new part (ID 1) on the right end.
            BoardSimulation simulation = CreateLineSimulation();
            Assert.That(simulation.Commands.Execute(GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(1, 0))),
                Is.EqualTo(CommandOutcome.Applied));
            IBoardReader board = simulation.Preview;
            Assert.That(board.BlockCount, Is.EqualTo(2));
            Assert.That(board.GetOrigin(1), Is.EqualTo(new Cell(0, 0)), "a part keeps its parent's origin");

            AssertPlacement(ExitFragmentPlacement.Create(board, 1, Side.Bottom, board.GetOrigin(1)),
                2f, ExitFragmentPlacement.GetDoorLine(board.Model, Side.Bottom), Vector3.back, 1);
            AssertPlacement(ExitFragmentPlacement.Create(board, 1, Side.Right, board.GetOrigin(1)),
                ExitFragmentPlacement.GetDoorLine(board.Model, Side.Right), 0f, Vector3.right, 1);
            AssertPlacement(ExitFragmentPlacement.Create(board, 0, Side.Bottom, board.GetOrigin(0)),
                0f, ExitFragmentPlacement.GetDoorLine(board.Model, Side.Bottom), Vector3.back, 1);
        }

        // 3 x 2: A (block 0) is a horizontal line of three on the bottom row.
        private static BoardSimulation CreateLineSimulation()
        {
            LevelModel model = BoardFixture.Create(new[] { "...", "AAA" });
            return new BoardSimulation(model, AttemptHooks.CreateEmpty(), DragSettings.Default);
        }

        private static void AssertPlacement(ExitFragmentPlacement placement, float x, float z, Vector3 outward, int lanes)
        {
            Assert.That(placement.LocalPosition.x, Is.EqualTo(x).Within(k_tolerance), "x");
            Assert.That(placement.LocalPosition.y, Is.EqualTo(ExitFragmentPlacement.k_centerHeight).Within(k_tolerance), "y");
            Assert.That(placement.LocalPosition.z, Is.EqualTo(z).Within(k_tolerance), "z");
            Vector3 forward = placement.LocalRotation * Vector3.forward;
            Assert.That(Vector3.Distance(forward, outward), Is.LessThan(k_tolerance), "the emitter's +Z points out of the board");
            Assert.That(placement.Lanes, Is.EqualTo(lanes), "lanes");
        }
    }
}
