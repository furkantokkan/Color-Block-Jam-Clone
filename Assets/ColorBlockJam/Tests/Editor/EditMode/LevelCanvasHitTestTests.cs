using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>Verifies the Level Editor canvas geometry and the point-to-cell, ring, door and block hit test.</summary>
    public sealed class LevelCanvasHitTestTests
    {
        [Test]
        public void GeometryCentresTheBoardAndFitsItsRing()
        {
            LevelCanvasGeometry geometry = LevelCanvasGeometry.Create(new Vector2(680f, 518f), 4, 5);

            // min((680 - 140) / 5, (518 - 140) / 6) = min(108, 63) -> 63; 140 = 2 x (12 margin + 58 edge band) and the
            // 0.5 cell gates add one cell per axis.
            Assert.That(geometry.CellSize, Is.EqualTo(63f));
            Assert.That(LevelCanvasGeometry.k_doorDepth, Is.EqualTo(LevelCanvasGeometry.k_ringDepth),
                "Gates are as deep as the wall ring, not past it (D-004).");
            Rect board = geometry.BoardRect;
            Assert.That(board.center.x, Is.EqualTo(340f).Within(1f));
            Assert.That(board.center.y, Is.EqualTo(259f).Within(1f));
            float doorDepth = LevelCanvasGeometry.k_doorDepth * geometry.CellSize;
            Assert.That(board.yMin - doorDepth, Is.GreaterThanOrEqualTo(0f), "Door bars stay inside the canvas.");
            Assert.That(board.yMax + doorDepth, Is.LessThanOrEqualTo(518f));
            float band = LevelCanvasGeometry.k_edgeButtonBand;
            Assert.That(board.yMin - doorDepth - band, Is.GreaterThanOrEqualTo(0f), "The edge button band fits above.");
            Assert.That(board.yMax + doorDepth + band, Is.LessThanOrEqualTo(518f), "The edge button band fits below.");
        }

        [Test]
        public void GeometryZoomsRelativeToTheFitWithinLimitsFromTheGridSize()
        {
            Vector2 size = new Vector2(680f, 518f);
            float fit = LevelCanvasGeometry.Create(size, 4, 5).CellSize;
            LevelCanvasGeometry first = LevelCanvasGeometry.Create(size, 4, 5, LevelCanvasGeometry.k_defaultZoom);
            Assert.That(first.CellSize, Is.EqualTo(Mathf.Floor(fit * LevelCanvasGeometry.k_defaultZoom)).Within(1f),
                "The first view leaves a margin around the fit.");

            LevelCanvasGeometry panned = LevelCanvasGeometry.Create(size, 4, 5, 1f, new Vector2(30f, -20f));
            Assert.That(panned.BoardRect.center.x, Is.EqualTo(370f).Within(1f), "Pan moves the board.");
            Assert.That(panned.BoardRect.center.y, Is.EqualTo(239f).Within(1f));

            LevelCanvasGeometry far = LevelCanvasGeometry.Create(size, 4, 5, 0.01f);
            Assert.That(far.CellSize, Is.EqualTo(Mathf.Floor(fit * LevelCanvasGeometry.k_minZoom)).Within(1f),
                "The farthest view is half the fit.");

            float maxZoom = LevelCanvasGeometry.GetMaxZoom(size, 4, 5);
            LevelCanvasGeometry close = LevelCanvasGeometry.Create(size, 4, 5, 100f);
            Assert.That(close.CellSize, Is.EqualTo(Mathf.Floor(size.x / LevelCanvasGeometry.k_closestCellsAcross)),
                "The closest view is capped at three cells across the canvas.");
            Assert.That(LevelCanvasGeometry.GetMaxZoom(size, 8, 10), Is.GreaterThan(maxZoom),
                "A bigger grid can zoom in further, relative to its smaller fit.");
        }

        [Test]
        public void GeometryKeepsCellsAtLeastEighteenPixels()
        {
            LevelCanvasGeometry geometry = LevelCanvasGeometry.Create(new Vector2(100f, 80f), 8, 10);

            Assert.That(geometry.CellSize, Is.EqualTo(18f));
            Assert.That(geometry.BoardRect.center.x, Is.EqualTo(50f).Within(1f), "Still centred when it overflows.");
        }

        [Test]
        public void TopRowIsTheHighestBoardRow()
        {
            LevelCanvasGeometry geometry = LevelCanvasGeometry.Create(new Vector2(680f, 518f), 4, 5);

            Assert.That(geometry.GetCellRect(new Vector2Int(0, 4)).y, Is.EqualTo(geometry.Origin.y));
            Assert.That(geometry.GetCellRect(new Vector2Int(0, 0)).yMax, Is.EqualTo(geometry.BoardRect.yMax));
            Assert.That(geometry.TryGetCell(geometry.Origin + new Vector2(1f, 1f), out Vector2Int cell), Is.True);
            Assert.That(cell, Is.EqualTo(new Vector2Int(0, 4)));
        }

        [Test]
        public void CellsAndBlocksHitWithTheirIds()
        {
            LevelCanvasModel model = LevelCanvasModel.Create(LevelCanvasModelTests.CreateLevelOne());
            LevelCanvasGeometry geometry = LevelCanvasGeometry.Create(new Vector2(680f, 518f), 4, 5);

            LevelCanvasHit empty = Test(model, geometry, CellCentre(geometry, 0, 0));
            Assert.That(empty, Is.EqualTo(LevelCanvasHit.ForCell(new Vector2Int(0, 0))));

            LevelCanvasHit block = Test(model, geometry, CellCentre(geometry, 3, 3));
            Assert.That(block.Kind, Is.EqualTo(LevelCanvasHitKind.Block));
            Assert.That(block.Cell, Is.EqualTo(new Vector2Int(3, 3)));
            Assert.That(block.EntityIndex, Is.Zero);
            Assert.That(block.EntityId, Is.EqualTo("b1"));
        }

        [Test]
        public void DoorsAndFreeRingSegmentsHitOnTheirSide()
        {
            LevelCanvasModel model = LevelCanvasModel.Create(LevelCanvasModelTests.CreateLevelOne());
            LevelCanvasGeometry geometry = LevelCanvasGeometry.Create(new Vector2(680f, 518f), 4, 5);
            float cell = geometry.CellSize;

            // Top segment 2 belongs to d1; its bar fills the 0.5 cell ring.
            Vector2 topDoor = new Vector2(geometry.BoardRect.xMin + 2.5f * cell, geometry.BoardRect.yMin - 0.4f * cell);
            LevelCanvasHit door = Test(model, geometry, topDoor);
            Assert.That(door, Is.EqualTo(LevelCanvasHit.ForDoor(BoardSide.Top, 2, 0, "d1")));
            Assert.That(door.EntityId, Is.EqualTo("d1"));

            // Top segment 0 has no door: the ring answers within 0.5 cell, nothing beyond it.
            Vector2 topRing = new Vector2(geometry.BoardRect.xMin + 0.5f * cell, geometry.BoardRect.yMin - 0.25f * cell);
            Assert.That(Test(model, geometry, topRing), Is.EqualTo(LevelCanvasHit.ForRingSegment(BoardSide.Top, 0)));
            Vector2 pastRing = new Vector2(topRing.x, geometry.BoardRect.yMin - 0.6f * cell);
            Assert.That(Test(model, geometry, pastRing).Kind, Is.EqualTo(LevelCanvasHitKind.Outside));
            Vector2 pastDoor = new Vector2(topDoor.x, geometry.BoardRect.yMin - 0.6f * cell);
            Assert.That(Test(model, geometry, pastDoor).Kind, Is.EqualTo(LevelCanvasHitKind.Outside),
                "A gate ends with the ring.");

            // Left segment index counts rows from the bottom.
            Vector2 leftRing = new Vector2(geometry.BoardRect.xMin - 0.25f * cell, geometry.BoardRect.yMax - 0.5f * cell);
            Assert.That(Test(model, geometry, leftRing), Is.EqualTo(LevelCanvasHit.ForRingSegment(BoardSide.Left, 0)));
            Vector2 rightRing = new Vector2(geometry.BoardRect.xMax + 0.25f * cell, geometry.BoardRect.yMin + 0.5f * cell);
            Assert.That(Test(model, geometry, rightRing), Is.EqualTo(LevelCanvasHit.ForRingSegment(BoardSide.Right, 4)));

            Vector2 bottomDoor = new Vector2(geometry.BoardRect.xMin + 0.5f * cell, geometry.BoardRect.yMax + 0.25f * cell);
            Assert.That(Test(model, geometry, bottomDoor), Is.EqualTo(LevelCanvasHit.ForDoor(BoardSide.Bottom, 0, 1, "d2")));
        }

        [Test]
        public void RingCornersAndFarPointsAreOutside()
        {
            LevelCanvasModel model = LevelCanvasModel.Create(LevelCanvasModelTests.CreateLevelOne());
            LevelCanvasGeometry geometry = LevelCanvasGeometry.Create(new Vector2(680f, 518f), 4, 5);
            Rect board = geometry.BoardRect;
            float quarter = 0.25f * geometry.CellSize;

            Assert.That(Test(model, geometry, new Vector2(board.xMin - quarter, board.yMin - quarter)).Kind,
                Is.EqualTo(LevelCanvasHitKind.Outside), "Top-left corner.");
            Assert.That(Test(model, geometry, new Vector2(board.xMax + quarter, board.yMin - quarter)).Kind,
                Is.EqualTo(LevelCanvasHitKind.Outside), "Top-right corner, next to door d1.");
            Assert.That(Test(model, geometry, new Vector2(board.xMax + quarter, board.yMax + quarter)).Kind,
                Is.EqualTo(LevelCanvasHitKind.Outside), "Bottom-right corner.");
            Assert.That(Test(model, geometry, new Vector2(board.xMin - quarter, board.yMax + quarter)).Kind,
                Is.EqualTo(LevelCanvasHitKind.Outside), "Bottom-left corner, next to door d2.");
            Assert.That(Test(model, geometry, Vector2.zero).Kind, Is.EqualTo(LevelCanvasHitKind.Outside));
        }

        [Test]
        public void EmptyModelHitsNothing()
        {
            LevelCanvasGeometry geometry = LevelCanvasGeometry.Create(new Vector2(680f, 518f), 0, 0);

            Assert.That(geometry.IsValid, Is.False);
            Assert.That(Test(LevelCanvasModel.Empty, geometry, new Vector2(340f, 259f)), Is.EqualTo(LevelCanvasHit.Outside));
        }

        private static LevelCanvasHit Test(LevelCanvasModel model, LevelCanvasGeometry geometry, Vector2 point)
        {
            return LevelCanvasHitTest.Test(model, geometry, point);
        }

        private static Vector2 CellCentre(LevelCanvasGeometry geometry, int x, int y)
        {
            return geometry.GetCellRect(new Vector2Int(x, y)).center;
        }
    }
}
