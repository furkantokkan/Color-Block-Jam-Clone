using System;
using ColorBlockJam.Editor.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies LevelPieceTools' rotate and flip geometry: cells stay normalized, four quarter turns restore the
    /// footprint and origin, and a horizontal or vertical flip mirrors the piece and undoes itself.
    /// </summary>
    public sealed class LevelPieceToolsTests
    {
        [Test]
        public void RotatingLevelPieceKeepsItsCellsNormalizedAndReturnsAfterFourTurns()
        {
            var original = new[]
            {
                Vector2Int.zero, new Vector2Int(0, 1), new Vector2Int(1, 1)
            };

            Vector2Int[] rotated = LevelPieceTools.RotateCounterClockwise(original);
            Assert.That(rotated, Is.EquivalentTo(new[]
            {
                new Vector2Int(1, 0), Vector2Int.zero, new Vector2Int(0, 1)
            }));

            for (int turn = 1; turn < 4; turn++)
                rotated = LevelPieceTools.RotateCounterClockwise(rotated);
            Assert.That(rotated, Is.EquivalentTo(original));
            Assert.Throws<ArgumentException>(() => LevelPieceTools.RotateCounterClockwise(Array.Empty<Vector2Int>()));
        }

        [Test]
        public void ClockwiseRotationUndoesCounterClockwiseAndReturnsAfterFourTurns()
        {
            // An L: the column (0,0), (0,1) with a foot to the right at (1,0); y points up on the board.
            var original = new[] { Vector2Int.zero, new Vector2Int(0, 1), new Vector2Int(1, 0) };

            Vector2Int[] clockwise = LevelPieceTools.RotateClockwise(original);
            Assert.That(clockwise, Is.EquivalentTo(new[]
            {
                new Vector2Int(0, 1), new Vector2Int(1, 1), Vector2Int.zero
            }), "Clockwise on the y-up board turns the upward column to the right and the foot downward.");
            Assert.That(LevelPieceTools.RotateCounterClockwise(clockwise), Is.EquivalentTo(original));

            Vector2Int[] turned = clockwise;
            for (int turn = 1; turn < 4; turn++)
                turned = LevelPieceTools.RotateClockwise(turned);
            Assert.That(turned, Is.EquivalentTo(original));
            Assert.Throws<ArgumentException>(() => LevelPieceTools.RotateClockwise(Array.Empty<Vector2Int>()));
        }

        [Test]
        public void QuarterTurnsStayInPlaceAndFourRestoreTheFootprint()
        {
            Vector2Int[][] footprints =
            {
                new[] { Vector2Int.zero, Vector2Int.right },
                new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up },
                new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) },
                new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0), new Vector2Int(1, 1) }
            };
            foreach (Vector2Int[] footprint in footprints)
            {
                foreach (bool clockwise in new[] { true, false })
                {
                    Vector2Int[] cells = footprint;
                    Vector2Int origin = new Vector2Int(5, 5);
                    for (int turn = 0; turn < 4; turn++)
                    {
                        origin += LevelPieceTools.GetTurnOriginShift(cells, clockwise);
                        cells = clockwise ? LevelPieceTools.RotateClockwise(cells) : LevelPieceTools.RotateCounterClockwise(cells);
                    }

                    Assert.That(cells, Is.EquivalentTo(footprint), "Four turns restore the cells.");
                    Assert.That(origin, Is.EqualTo(new Vector2Int(5, 5)), "Four turns restore the origin.");
                }
            }

            Vector2Int[] corner = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };
            Assert.That(LevelPieceTools.GetTurnOriginShift(corner, true), Is.EqualTo(Vector2Int.zero),
                "A piece in a square box turns around the box centre and keeps its origin.");
            Assert.That(LevelPieceTools.GetTurnOriginShift(new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) }, true),
                Is.EqualTo(new Vector2Int(1, -1)), "A 1 x 3 turns around its middle cell.");
        }

        [Test]
        public void ATurnThatCrossesTheBoardEdgeSlidesBackInside()
        {
            // A vertical 1 x 3 against the left edge of a 6 x 5 board turns around its middle cell to x -1..1.
            Vector2Int[] column = { Vector2Int.zero, Vector2Int.up, new Vector2Int(0, 2) };
            Vector2Int origin = new Vector2Int(0, 1);
            Vector2Int[] turned = LevelPieceTools.RotateClockwise(column);
            Vector2Int placed = origin + LevelPieceTools.GetTurnOriginShift(column, true);
            Assert.That(placed.x, Is.EqualTo(-1), "The turn in place crosses the left edge.");

            Assert.That(LevelPieceTools.GetBoardKick(turned, placed, 6, 5), Is.EqualTo(Vector2Int.right),
                "It slides one cell right onto the board.");
            Assert.That(LevelPieceTools.GetBoardKick(turned, new Vector2Int(4, 2), 6, 5), Is.EqualTo(Vector2Int.left),
                "Across the right edge it slides left.");
            Assert.That(LevelPieceTools.GetBoardKick(turned, new Vector2Int(1, 2), 6, 5), Is.EqualTo(Vector2Int.zero),
                "A turn that fits does not move.");
            Assert.That(LevelPieceTools.GetBoardKick(column, new Vector2Int(2, 3), 6, 5), Is.EqualTo(Vector2Int.down),
                "Across the top edge it slides down.");
        }

        [Test]
        public void FlipHorizontalMirrorsTheLAndZPiecesAndUndoesItself()
        {
            var lBase = new[]
            {
                new Vector2Int(0, 0), new Vector2Int(0, 1),
                new Vector2Int(0, 2), new Vector2Int(1, 2)
            };
            var lMirror = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(1, 1),
                new Vector2Int(0, 2), new Vector2Int(1, 2)
            };
            var zBase = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(2, 0),
                new Vector2Int(0, 1), new Vector2Int(1, 1)
            };
            var zMirror = new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                new Vector2Int(1, 1), new Vector2Int(2, 1)
            };

            Assert.That(LevelPieceTools.FlipHorizontal(lBase), Is.EquivalentTo(lMirror));
            Assert.That(LevelPieceTools.FlipHorizontal(zBase), Is.EquivalentTo(zMirror));
            Assert.That(LevelPieceTools.FlipHorizontal(LevelPieceTools.FlipHorizontal(lBase)),
                Is.EquivalentTo(lBase));
            Assert.Throws<ArgumentException>(() => LevelPieceTools.FlipHorizontal(Array.Empty<Vector2Int>()));
        }

        [Test]
        public void FlipVerticalMirrorsRowsAndStartsAtZero()
        {
            Vector2Int[] corner = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };

            Vector2Int[] flipped = LevelPieceTools.FlipVertical(corner);

            Assert.That(flipped, Is.EquivalentTo(new[] { Vector2Int.up, new Vector2Int(1, 1), Vector2Int.zero }));
            Assert.That(LevelPieceTools.FlipVertical(flipped), Is.EquivalentTo(corner));
            Assert.Throws<ArgumentException>(() => LevelPieceTools.FlipVertical(new Vector2Int[0]));
        }
    }
}
