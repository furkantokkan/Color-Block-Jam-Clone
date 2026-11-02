using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the BLK-01 Arrow marker rule (TASK-20260926-212320271-98b25efa-level-editor-arrow-markers AC-1,
    /// revision 1): the marker is centred on the footprint's bounding box, its extent is the box's extent along the
    /// axis, the variant clamps that extent to 1-3, and a block without a valid Arrow effect has no marker.
    /// </summary>
    public sealed class ArrowMarkerRunTests
    {
        private static readonly Vector2Int[] s_single = { new Vector2Int(0, 0) };
        private static readonly Vector2Int[] s_square =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1)
        };
        private static readonly Vector2Int[] s_horizontalI3 = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) };
        private static readonly Vector2Int[] s_verticalI3 = { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2) };
        private static readonly Vector2Int[] s_horizontalI4 =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0)
        };
        private static readonly Vector2Int[] s_verticalI4 =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(0, 3)
        };
        // The library L Shape: a column of three with a foot to the right at the top.
        private static readonly Vector2Int[] s_l =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2)
        };
        // The library T Shape: a row of three over a one-cell stem at (1, 0).
        private static readonly Vector2Int[] s_t =
        {
            new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1)
        };
        private static readonly Vector2Int[] s_plus =
        {
            new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(1, 2)
        };

        private static ArrowMarker s_sink;

        [Test]
        public void OneCellUsesArrowOneOnItsCellOnEitherAxis()
        {
            AssertMarker(ArrowMarkerRun.Create(s_single, LevelEffectAxis.Horizontal), LevelEffectAxis.Horizontal,
                new Vector2(0f, 0f), 1, 1);
            AssertMarker(ArrowMarkerRun.Create(s_single, LevelEffectAxis.Vertical), LevelEffectAxis.Vertical,
                new Vector2(0f, 0f), 1, 1);
        }

        [Test]
        public void TwoByTwoCentresBetweenItsColumnsAndRowsOnEitherAxis()
        {
            // The centre lies on the line between the two columns and the two rows, not on one of the columns.
            AssertMarker(ArrowMarkerRun.Create(s_square, LevelEffectAxis.Vertical), LevelEffectAxis.Vertical,
                new Vector2(0.5f, 0.5f), 2, 2);
            AssertMarker(ArrowMarkerRun.Create(s_square, LevelEffectAxis.Horizontal), LevelEffectAxis.Horizontal,
                new Vector2(0.5f, 0.5f), 2, 2);
        }

        [Test]
        public void StraightBlocksAlongTheirAxisUseTheirLengthUpToThreeWithoutStretching()
        {
            AssertMarker(ArrowMarkerRun.Create(s_horizontalI3, LevelEffectAxis.Horizontal), LevelEffectAxis.Horizontal,
                new Vector2(1f, 0f), 3, 3);
            AssertMarker(ArrowMarkerRun.Create(s_verticalI3, LevelEffectAxis.Vertical), LevelEffectAxis.Vertical,
                new Vector2(0f, 1f), 3, 3);
            AssertMarker(ArrowMarkerRun.Create(s_horizontalI4, LevelEffectAxis.Horizontal), LevelEffectAxis.Horizontal,
                new Vector2(1.5f, 0f), 4, 3);
            AssertMarker(ArrowMarkerRun.Create(s_verticalI4, LevelEffectAxis.Vertical), LevelEffectAxis.Vertical,
                new Vector2(0f, 1.5f), 4, 3);
        }

        [Test]
        public void AnAxisAcrossAStraightBlockUsesOneCellAtItsCentre()
        {
            AssertMarker(ArrowMarkerRun.Create(s_horizontalI3, LevelEffectAxis.Vertical), LevelEffectAxis.Vertical,
                new Vector2(1f, 0f), 1, 1);
            AssertMarker(ArrowMarkerRun.Create(s_verticalI3, LevelEffectAxis.Horizontal), LevelEffectAxis.Horizontal,
                new Vector2(0f, 1f), 1, 1);
            AssertMarker(ArrowMarkerRun.Create(s_horizontalI4, LevelEffectAxis.Vertical), LevelEffectAxis.Vertical,
                new Vector2(1.5f, 0f), 1, 1);
            AssertMarker(ArrowMarkerRun.Create(s_verticalI4, LevelEffectAxis.Horizontal), LevelEffectAxis.Horizontal,
                new Vector2(0f, 1.5f), 1, 1);
        }

        [Test]
        public void LShapeIsCentredOnItsBoundingBox()
        {
            // Box x 0-1, y 0-2: the centre (0.5, 1) lies between the stem and the empty column beside it.
            AssertMarker(ArrowMarkerRun.Create(s_l, LevelEffectAxis.Vertical), LevelEffectAxis.Vertical,
                new Vector2(0.5f, 1f), 3, 3);
            AssertMarker(ArrowMarkerRun.Create(s_l, LevelEffectAxis.Horizontal), LevelEffectAxis.Horizontal,
                new Vector2(0.5f, 1f), 2, 2);
        }

        [Test]
        public void TShapeIsCentredOnItsBoundingBox()
        {
            // Box x 0-2, y 0-1: the centre (1, 0.5) lies between the bar and the stem.
            AssertMarker(ArrowMarkerRun.Create(s_t, LevelEffectAxis.Horizontal), LevelEffectAxis.Horizontal,
                new Vector2(1f, 0.5f), 3, 3);
            AssertMarker(ArrowMarkerRun.Create(s_t, LevelEffectAxis.Vertical), LevelEffectAxis.Vertical,
                new Vector2(1f, 0.5f), 2, 2);
        }

        [Test]
        public void PlusShapeIsCentredOnItsMiddleCellOnBothAxes()
        {
            AssertMarker(ArrowMarkerRun.Create(s_plus, LevelEffectAxis.Horizontal), LevelEffectAxis.Horizontal,
                new Vector2(1f, 1f), 3, 3);
            AssertMarker(ArrowMarkerRun.Create(s_plus, LevelEffectAxis.Vertical), LevelEffectAxis.Vertical,
                new Vector2(1f, 1f), 3, 3);
        }

        [Test]
        public void CellsKeepTheirOwnSpaceSoAbsoluteCellsGiveAnAbsoluteCentre()
        {
            Vector2Int origin = new Vector2Int(3, -2);
            Vector2Int[] moved = new Vector2Int[s_square.Length];
            for (int index = 0; index < moved.Length; index++)
            {
                moved[index] = s_square[index] + origin;
            }

            AssertMarker(ArrowMarkerRun.Create(moved, LevelEffectAxis.Vertical), LevelEffectAxis.Vertical,
                new Vector2(3.5f, -1.5f), 2, 2);
        }

        [Test]
        public void NoCellsOrAnUnknownAxisGiveNoMarker()
        {
            Assert.That(ArrowMarkerRun.Create(null, LevelEffectAxis.Horizontal).HasMarker, Is.False);
            Assert.That(ArrowMarkerRun.Create(new Vector2Int[0], LevelEffectAxis.Vertical).HasMarker, Is.False);
            Assert.That(ArrowMarkerRun.Create(s_horizontalI3, (LevelEffectAxis)7).HasMarker, Is.False);
            Assert.That(default(ArrowMarker).Variant, Is.Zero);
        }

        [Test]
        public void CreatingAMarkerAllocatesNothing()
        {
            ArrowMarkerRun.Create(s_plus, LevelEffectAxis.Vertical);

            Assert.That(() => { s_sink = ArrowMarkerRun.Create(s_plus, LevelEffectAxis.Vertical); },
                Is.Not.AllocatingGCMemory());
            Assert.That(s_sink.HasMarker, Is.True);
        }

        [TestCase("Horizontal", LevelEffectAxis.Horizontal)]
        [TestCase("Vertical", LevelEffectAxis.Vertical)]
        public void ABlocksArrowEffectGivesItsAxisAndMarker(string axisValue, LevelEffectAxis expected)
        {
            LevelBlockData block = Block(new LevelEffectData("arrow", new[] { new LevelEffectParamData("axis", axisValue) }));

            Assert.That(ArrowMarkerRun.TryGetAxis(block, out LevelEffectAxis axis), Is.True);
            Assert.That(axis, Is.EqualTo(expected));
            ArrowMarker marker = ArrowMarkerRun.Create(block, s_square);
            Assert.That(marker.HasMarker, Is.True);
            Assert.That(marker.Axis, Is.EqualTo(expected));
            Assert.That(marker.Center, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        }

        [Test]
        public void MissingInvalidOrOtherEffectsGiveNoMarker()
        {
            LevelBlockData[] blocks =
            {
                null,
                Block(),
                Block(new LevelEffectData("arrow", new LevelEffectParamData[0])),
                Block(new LevelEffectData("arrow", new[] { new LevelEffectParamData("axis", "Diagonal") })),
                Block(new LevelEffectData("arrow", new[] { new LevelEffectParamData("direction", "Vertical") })),
                Block(new LevelEffectData("ice", new[] { new LevelEffectParamData("clearsToThaw", "3") }))
            };

            for (int index = 0; index < blocks.Length; index++)
            {
                Assert.That(ArrowMarkerRun.TryGetAxis(blocks[index], out _), Is.False, $"Block {index}");
                Assert.That(ArrowMarkerRun.Create(blocks[index], s_square).HasMarker, Is.False, $"Block {index}");
            }
        }

        [Test]
        public void AnArrowNextToAnotherEffectStillGivesItsMarker()
        {
            LevelBlockData block = Block(
                new LevelEffectData("ice", new[] { new LevelEffectParamData("clearsToThaw", "3") }),
                new LevelEffectData("arrow", new[] { new LevelEffectParamData("axis", "Vertical") }));

            ArrowMarker marker = ArrowMarkerRun.Create(block, s_verticalI4);

            AssertMarker(marker, LevelEffectAxis.Vertical, new Vector2(0f, 1.5f), 4, 3);
        }

        private static LevelBlockData Block(params LevelEffectData[] effects)
        {
            return new LevelBlockData("b1", "Pink", Vector2Int.zero, s_square, effects);
        }

        private static void AssertMarker(ArrowMarker marker, LevelEffectAxis axis, Vector2 centre, int extent, int variant)
        {
            Assert.That(marker.HasMarker, Is.True);
            Assert.That(marker.Axis, Is.EqualTo(axis));
            Assert.That(marker.Center, Is.EqualTo(centre), "bounding-box centre");
            Assert.That(marker.Extent, Is.EqualTo(extent), "extent along the axis");
            Assert.That(marker.Variant, Is.EqualTo(variant), "Arrow_N");
        }
    }
}
