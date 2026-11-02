using System;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.Environment
{
    public sealed class BoardLayoutTests
    {
        [Test]
        public void RectangleExposesFloorCellMappingAndBounds()
        {
            BoardLayout layout = Create(4, 3);

            Assert.That(layout.Width, Is.EqualTo(4));
            Assert.That(layout.Height, Is.EqualTo(3));
            Assert.That(layout.IsFloor(0, 0) && layout.IsFloor(3, 2), Is.True);
            Assert.That(layout.IsFloor(-1, 0) || layout.IsFloor(4, 0) || layout.IsFloor(0, 3), Is.False);
            Assert.That(layout.CellToLocal(new Vector2Int(3, 2)), Is.EqualTo(new Vector3(3f, 0f, 2f)));
            Assert.That(layout.LocalBounds.min.x, Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(layout.LocalBounds.max.x, Is.EqualTo(4f).Within(1e-5f));
            Assert.That(layout.LocalBounds.min.z, Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(layout.LocalBounds.max.z, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(layout.LocalBounds.min.y, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(layout.LocalBounds.max.y, Is.EqualTo(0.72f).Within(1e-5f));
        }

        [Test]
        public void EveryFloorCellRoundTripsThroughLocalSpace()
        {
            BoardLayout layout = Create(6, 6, Cells((2, 2), (3, 3)));

            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    Vector3 local = layout.CellToLocal(new Vector2Int(x, y)) + new Vector3(0.3f, 0f, -0.3f);
                    bool found = layout.TryLocalToCell(local, out Vector2Int cell);
                    Assert.That(found, Is.EqualTo(layout.IsFloor(x, y)), $"cell {x},{y}");
                    Assert.That(cell, Is.EqualTo(new Vector2Int(x, y)));
                }
            }

            Assert.That(layout.IsFloor(2, 2) || layout.IsFloor(3, 3), Is.False);
            Assert.That(layout.TryLocalToCell(new Vector3(-0.8f, 0f, 0f), out _), Is.False);
        }

        [Test]
        public void DoorSlotsFollowTheJsonSpanAndStandInTheWallRing()
        {
            BoardLayout layout = Create(5, 4, Array.Empty<Vector2Int>(),
                Door("top", BoardSide.Top, 1, 2, "Red"),
                Door("right", BoardSide.Right, 0, 1, "Blue"),
                Door("bottom", BoardSide.Bottom, 4, 1, "Yellow"));

            Assert.That(layout.DoorSlotCount, Is.EqualTo(4));
            AssertSlot(layout.GetDoorSlotAt(0), 0, 0, new Vector2Int(1, 3), new Vector3(1f, 0f, 3.75f), 0, PaletteColor.Red);
            AssertSlot(layout.GetDoorSlotAt(1), 0, 1, new Vector2Int(2, 3), new Vector3(2f, 0f, 3.75f), 0, PaletteColor.Red);
            AssertSlot(layout.GetDoorSlotAt(2), 1, 0, new Vector2Int(4, 0), new Vector3(4.75f, 0f, 0f), 90, PaletteColor.Blue);
            AssertSlot(layout.GetDoorSlotAt(3), 2, 0, new Vector2Int(4, 0), new Vector3(4f, 0f, -0.75f), 0, PaletteColor.Yellow);
        }

        [Test]
        public void LevelDataBuildsARectangularLayoutWithItsDoors()
        {
            LevelData level = new LevelData("level-1", "Level 1", 5, 4, 60,
                new[] { Door("left", BoardSide.Left, 1, 2, "Purple") },
                new[] { new LevelBlockData("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero }) });

            Assert.That(BoardLayout.TryCreate(level, out BoardLayout layout, out string error), Is.True, error);
            Assert.That(layout.Width, Is.EqualTo(5));
            Assert.That(layout.Height, Is.EqualTo(4));
            Assert.That(layout.DoorSlotCount, Is.EqualTo(2));
            AssertSlot(layout.GetDoorSlotAt(1), 0, 1, new Vector2Int(0, 2), new Vector3(-0.75f, 0f, 2f), 90, PaletteColor.Purple);
        }

        [Test]
        public void LevelDataVoidCellsBecomeHolesInTheLayout()
        {
            LevelData level = new LevelData("level-1", "Level 1", 5, 4, 60,
                new[] { new Vector2Int(0, 0), new Vector2Int(4, 3) },
                new[] { Door("left", BoardSide.Left, 1, 2, "Purple") },
                new[] { new LevelBlockData("b1", "Red", new Vector2Int(1, 0), new[] { Vector2Int.zero }) },
                new LevelCellObjectData[0]);
            Assert.That(level.Validate(out string error), Is.True, error);

            Assert.That(BoardLayout.TryCreate(level, out BoardLayout layout, out error), Is.True, error);
            Assert.That(layout.IsFloor(0, 0), Is.False);
            Assert.That(layout.IsFloor(4, 3), Is.False);
            Assert.That(layout.IsFloor(1, 0), Is.True);
            Assert.That(layout.IsFloor(3, 3), Is.True);
            Assert.That(layout.DoorSlotCount, Is.EqualTo(2));
        }

        [TestCase(-1, 0)]
        [TestCase(4, 0)]
        [TestCase(0, 3)]
        public void RejectsAVoidCellOutsideTheBoard(int x, int y)
        {
            AssertRejected(4, 3, Cells((x, y)));
        }

        [Test]
        public void RejectsDuplicateVoidCellsAndABoardWithoutFloor()
        {
            AssertRejected(4, 3, Cells((1, 1), (1, 1)));
            AssertRejected(2, 1, Cells((0, 0), (1, 0)));
        }

        [Test]
        public void RejectsDoorsOntoVoidOverlappingOrOutsideTheirEdge()
        {
            AssertRejected(4, 3, Cells((1, 2)), Door("top", BoardSide.Top, 1, 1, "Red"));
            AssertRejected(4, 3, Array.Empty<Vector2Int>(),
                Door("a", BoardSide.Top, 0, 2, "Red"), Door("b", BoardSide.Top, 1, 1, "Blue"));
            AssertRejected(4, 3, Array.Empty<Vector2Int>(), Door("wide", BoardSide.Left, 2, 2, "Red"));
            AssertRejected(4, 3, Array.Empty<Vector2Int>(), Door("empty", BoardSide.Bottom, 0, 0, "Red"));
        }

        [TestCase("Red", true)]
        [TestCase("Pink", true)]
        [TestCase("LightTurquoise", true)]
        [TestCase("Wall", false)]
        [TestCase("Ground", false)]
        [TestCase("red", false)]
        [TestCase("3", false)]
        [TestCase("Red,Blue", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void DoorColoursAreExactGatePaletteNames(string colorId, bool expected)
        {
            Assert.That(BoardLayout.TryParseDoorColor(colorId, out _), Is.EqualTo(expected));
            AssertCreation(4, 3, Array.Empty<Vector2Int>(), new[] { Door("door", BoardSide.Top, 0, 1, colorId) }, expected);
        }

        internal static BoardLayout Create(int width, int height, Vector2Int[] voidCells = null, params LevelDoorData[] doors)
        {
            bool created = BoardLayout.TryCreate(
                width, height, voidCells ?? Array.Empty<Vector2Int>(), doors, out BoardLayout layout, out string error);
            Assert.That(created, Is.True, error);
            return layout;
        }

        /// <summary>
        /// A shared 6x6 layout: a 2x2 interior hole at cells (2..3, 2..3) and a two-cell notch open to
        /// the outer ring at (0, 5)-(1, 5). Used by the Environment composer and wall-run tests.
        /// </summary>
        internal static BoardLayout CreateHoleAndNotchLayout(params LevelDoorData[] doors)
        {
            return Create(6, 6, Cells((2, 2), (3, 2), (2, 3), (3, 3), (0, 5), (1, 5)), doors);
        }

        internal static Vector2Int[] Cells(params (int x, int y)[] cells)
        {
            return Array.ConvertAll(cells, cell => new Vector2Int(cell.x, cell.y));
        }

        internal static LevelDoorData Door(string id, BoardSide side, int start, int length, string color)
        {
            return new LevelDoorData(id, side, start, length, color);
        }

        private static void AssertRejected(int width, int height, Vector2Int[] voidCells, params LevelDoorData[] doors)
        {
            AssertCreation(width, height, voidCells, doors, false);
        }

        private static void AssertCreation(
            int width, int height, Vector2Int[] voidCells, LevelDoorData[] doors, bool expected)
        {
            bool created = BoardLayout.TryCreate(width, height, voidCells, doors, out BoardLayout layout, out string error);
            Assert.That(created, Is.EqualTo(expected), error);
            Assert.That(layout == null, Is.EqualTo(!expected));
            if (!expected)
            {
                Assert.That(error, Is.Not.Empty);
            }
        }

        private static void AssertSlot(
            DoorSlot slot, int doorIndex, int segmentIndex, Vector2Int cell, Vector3 position, int yaw, PaletteColor color)
        {
            Assert.That(slot.DoorIndex, Is.EqualTo(doorIndex));
            Assert.That(slot.SegmentIndex, Is.EqualTo(segmentIndex));
            Assert.That(slot.Cell, Is.EqualTo(cell));
            Assert.That(slot.LocalPosition, Is.EqualTo(position));
            Assert.That(slot.Yaw, Is.EqualTo(yaw));
            Assert.That(slot.Color, Is.EqualTo(color));
        }
    }
}
