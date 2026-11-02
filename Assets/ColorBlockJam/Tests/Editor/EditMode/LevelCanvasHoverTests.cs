using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>Verifies that canvas hover asks for a repaint only when the hovered cell or segment changes.</summary>
    public sealed class LevelCanvasHoverTests
    {
        [Test]
        public void MovingInsideOneCellRequestsNoRepaint()
        {
            LevelCanvasModel model = LevelCanvasModel.Create(LevelCanvasModelTests.CreateLevelOne());
            LevelCanvasGeometry geometry = LevelCanvasGeometry.Create(new Vector2(680f, 518f), 4, 5);
            Rect cell = geometry.GetCellRect(new Vector2Int(1, 0));
            LevelCanvasHover hover = new LevelCanvasHover();

            Assert.That(hover.Update(LevelCanvasHitTest.Test(model, geometry, cell.min + Vector2.one)), Is.True);
            Assert.That(hover.Update(LevelCanvasHitTest.Test(model, geometry, cell.center)), Is.False);
            Assert.That(hover.Update(LevelCanvasHitTest.Test(model, geometry, cell.max - Vector2.one)), Is.False);
            Assert.That(hover.Current, Is.EqualTo(LevelCanvasHit.ForCell(new Vector2Int(1, 0))));
        }

        [Test]
        public void EnteringAnotherCellSegmentOrLeavingRequestsOneRepaint()
        {
            LevelCanvasHover hover = new LevelCanvasHover();

            Assert.That(hover.Update(LevelCanvasHit.Outside), Is.False, "Starts outside.");
            Assert.That(hover.Update(LevelCanvasHit.ForCell(new Vector2Int(0, 0))), Is.True);
            Assert.That(hover.Update(LevelCanvasHit.ForCell(new Vector2Int(1, 0))), Is.True);
            Assert.That(hover.Update(LevelCanvasHit.ForBlock(new Vector2Int(1, 1), 0, "b1")), Is.True);
            Assert.That(hover.Update(LevelCanvasHit.ForRingSegment(BoardSide.Top, 0)), Is.True);
            Assert.That(hover.Update(LevelCanvasHit.ForRingSegment(BoardSide.Top, 0)), Is.False);
            Assert.That(hover.Update(LevelCanvasHit.ForDoor(BoardSide.Top, 1, 0, "d1")), Is.True);
            Assert.That(hover.Update(LevelCanvasHit.Outside), Is.True);
            Assert.That(hover.Update(LevelCanvasHit.Outside), Is.False);
        }
    }
}
