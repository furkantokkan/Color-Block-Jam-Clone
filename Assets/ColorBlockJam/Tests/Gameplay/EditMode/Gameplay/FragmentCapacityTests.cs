using System;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// Proves the Rocket part bounds Home warms roots, Ice shells and Arrow markers with (B-19, AC-16): the most parts
    /// a footprint can show at once is the maximum independent set of its 4-neighbour graph, exact up to 12 cells and
    /// the cell count minus one above, and Arrow markers are counted per variant over every reachable part set.
    /// </summary>
    public sealed class FragmentCapacityTests
    {
        private static readonly Vector2Int[] s_single = { new Vector2Int(0, 0) };
        private static readonly Vector2Int[] s_line3 = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) };

        // The library's LTypeB1: a vertical line of three with a foot at the top right.
        private static readonly Vector2Int[] s_l4 =
            { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2) };

        private static readonly Vector2Int[] s_square =
            { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) };

        [Test]
        public void ASingleCellIsItsOnlyPart()
        {
            Assert.That(FragmentCapacity.GetMaxFragments(s_single), Is.EqualTo(1));
        }

        [Test]
        public void ALineOfThreeSplitsIntoTwoEnds()
        {
            Assert.That(FragmentCapacity.GetMaxFragments(s_line3), Is.EqualTo(2));
        }

        [Test]
        public void AnLOfFourShowsAtMostTwoParts()
        {
            Assert.That(FragmentCapacity.GetMaxFragments(s_l4), Is.EqualTo(2));
        }

        [Test]
        public void ASquareOfFourShowsAtMostItsTwoDiagonalCells()
        {
            Assert.That(FragmentCapacity.GetMaxFragments(s_square), Is.EqualTo(2));
        }

        [Test]
        public void AThreeByThreeShowsItsFiveCornerAndCentreCells()
        {
            Vector2Int[] cells = new Vector2Int[9];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = new Vector2Int(i % 3, i / 3);
            }

            Assert.That(FragmentCapacity.GetMaxFragments(cells), Is.EqualTo(5));
        }

        [Test]
        public void AboveTwelveCellsTheBoundIsTheCellCountMinusOne()
        {
            // A 13-cell line: its exact maximum is 7, but the bound is not enumerated above 12 cells.
            Vector2Int[] cells = new Vector2Int[FragmentCapacity.MaxEnumeratedCells + 1];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = new Vector2Int(i, 0);
            }

            Assert.That(FragmentCapacity.GetMaxFragments(cells), Is.EqualTo(cells.Length - 1));
        }

        [Test]
        public void AnEmptyFootprintIsRefused()
        {
            Assert.Throws<ArgumentException>(() => FragmentCapacity.GetMaxFragments(Array.Empty<Vector2Int>()));
        }

        [Test]
        public void AVerticalSquaresPartsShowTwoArrowOnesOrOneArrowTwoAtOnce()
        {
            int[] counts = new int[3];

            FragmentCapacity.AddArrowMarkerCapacity(s_square, LevelEffectAxis.Vertical, counts);

            // Two diagonal cells are two Arrow_1 parts; a vertical pair, an L or the whole square is one Arrow_2.
            Assert.That(counts, Is.EqualTo(new[] { 2, 1, 0 }));
        }

        [Test]
        public void MarkerCountsAddUpAcrossBlocksAndNeverExceedTheBlocksOwnVariant()
        {
            int[] counts = new int[3];

            FragmentCapacity.AddArrowMarkerCapacity(s_line3, LevelEffectAxis.Horizontal, counts);
            FragmentCapacity.AddArrowMarkerCapacity(s_single, LevelEffectAxis.Horizontal, counts);

            // The line shows Arrow_3 whole, Arrow_2 after losing an end and two Arrow_1 ends after losing its middle.
            Assert.That(counts, Is.EqualTo(new[] { 3, 1, 1 }));

            int[] across = new int[3];
            FragmentCapacity.AddArrowMarkerCapacity(s_line3, LevelEffectAxis.Vertical, across);
            Assert.That(across, Is.EqualTo(new[] { 2, 0, 0 }), "a horizontal line is one cell long vertically");
        }
    }
}
