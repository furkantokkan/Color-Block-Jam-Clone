using System;
using System.Collections.Generic;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Runtime.Board;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies BlockPieceComposer's placement math: oriented corner output, exact non-overlapping occupancy across
    /// every library shape and rotation, invalid-input safety and zero-allocation recomposition (DEC-120).
    /// </summary>
    public sealed class BoardKitComposerTests
    {
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_l = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };

        [Test]
        public void SingleHasFourOrientedOriginalCorners()
        {
            var output = new List<BlockPiecePlacement>(9);
            new BlockPieceComposer(1).Compose(s_single, output);
            Assert.That(output.Count, Is.EqualTo(4));
            AssertPlacement(output[0], BlockPieceKind.OuterCorner, -1, -1, 1);
            AssertPlacement(output[1], BlockPieceKind.OuterCorner, 1, -1, 2);
            AssertPlacement(output[2], BlockPieceKind.OuterCorner, -1, 1, 0);
            AssertPlacement(output[3], BlockPieceKind.OuterCorner, 1, 1, 3);
        }

        [Test]
        public void EveryLibraryShapeInEveryRotationHasExactOccupancyWithoutOverlappingTiles()
        {
            var library = BlockShapeLibrary.LoadProject();
            Assert.That(library.JsonPath, Is.EqualTo(BoardKitAssets.ShapesJsonPath));
            Assert.That(library.Shapes.Count, Is.EqualTo(11));
            var composer = new BlockPieceComposer(PieceGridDraft.MaxSize * PieceGridDraft.MaxSize);
            var output = new List<BlockPiecePlacement>(PieceGridDraft.MaxSize * PieceGridDraft.MaxSize * 9);
            for (int i = 0; i < library.Shapes.Count; i++) for (int rotation = 0; rotation < 4; rotation++)
            {
                var cells = new List<Vector2Int>();
                for (int c = 0; c < library.Shapes[i].Cells.Count; c++) cells.Add(Rotate(library.Shapes[i].Cells[c], rotation));
                composer.Compose(cells, output);
                CheckOccupancy(cells, output);
            }
        }

        [Test]
        public void ActivePieceDatabaseHasOneRecordPerRotationAndMirrorFamily()
        {
            var library = BlockShapeLibrary.LoadProject();
            Assert.That(library.Shapes.Count, Is.EqualTo(11));
            var footprints = new HashSet<string>();
            for (int index = 0; index < library.Shapes.Count; index++)
            {
                string family = null;
                for (int mirror = 0; mirror < 2; mirror++)
                {
                    for (int turn = 0; turn < 4; turn++)
                    {
                        var cells = new List<Vector2Int>();
                        foreach (Vector2Int cell in library.Shapes[index].Cells)
                            cells.Add(Rotate(mirror == 0 ? cell : new Vector2Int(-cell.x, cell.y), turn));
                        int minX = int.MaxValue;
                        int minY = int.MaxValue;
                        foreach (Vector2Int cell in cells)
                        {
                            minX = Math.Min(minX, cell.x);
                            minY = Math.Min(minY, cell.y);
                        }
                        var values = new List<string>();
                        foreach (Vector2Int cell in cells)
                            values.Add($"{cell.x - minX}:{cell.y - minY}");
                        values.Sort(StringComparer.Ordinal);
                        string key = string.Join(",", values);
                        if (family == null || string.CompareOrdinal(key, family) < 0)
                            family = key;
                    }
                }
                Assert.That(footprints.Add(family), Is.True, library.Shapes[index].DisplayName);
            }
        }

        [Test]
        public void InvalidInputsPreserveThePreviousOutput()
        {
            var composer = new BlockPieceComposer(8);
            var output = new List<BlockPiecePlacement>(72);
            composer.Compose(s_l, output);
            int count = output.Count;
            Assert.Throws<ArgumentException>(() => composer.Compose(Array.Empty<Vector2Int>(), output));
            Assert.Throws<ArgumentException>(() => composer.Compose(new[] { Vector2Int.zero, Vector2Int.zero }, output));
            Assert.Throws<ArgumentException>(() => composer.Compose(new[] { Vector2Int.zero, new Vector2Int(2, 0) }, output));
            Assert.That(output.Count, Is.EqualTo(count));
            Assert.Throws<ArgumentException>(() => composer.Compose(s_l, new List<BlockPiecePlacement>(1)));
        }

        [Test]
        public void RecompositionAfterEitherLRemovalUsesTheSameRules()
        {
            var composer = new BlockPieceComposer(4);
            var output = new List<BlockPiecePlacement>(36);
            var original = new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up, new Vector2Int(0, 2) };
            composer.Compose(original, output);
            var first = new[] { Vector2Int.zero, Vector2Int.up, new Vector2Int(0, 2) };
            composer.Compose(first, output); CheckOccupancy(first, output);
            composer.Compose(s_l, output); CheckOccupancy(s_l, output);
        }

        [Test]
        public void PreparedComposerAllocatesZeroBytes()
        {
            var composer = new BlockPieceComposer(64);
            var output = new List<BlockPiecePlacement>(576);
            var cells = new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up, new Vector2Int(0, 2) };
            for (int i = 0; i < 16; i++) composer.Compose(cells, output);
            // AD-27: GC.GetAllocatedBytesForCurrentThread reads 0 in the 2022.3 Editor; the warmed Recorder
            // constraint gives a real allocation check.
            Assert.That(() => composer.Compose(cells, output), Is.Not.AllocatingGCMemory());
        }

        private static void AssertPlacement(BlockPiecePlacement p, BlockPieceKind kind, int x, int y, int rotation)
        {
            Assert.That(p.Kind, Is.EqualTo(kind));
            Assert.That(p.PositionTwice, Is.EqualTo(new Vector2Int(x, y)));
            Assert.That(p.QuarterTurns, Is.EqualTo(rotation));
        }

        private static Vector2Int Rotate(Vector2Int value, int turns)
        {
            for (int i = 0; i < turns; i++) value = new Vector2Int(-value.y, value.x);
            return value;
        }

        private static void CheckOccupancy(IReadOnlyList<Vector2Int> cells, List<BlockPiecePlacement> placements)
        {
            var occupied = new HashSet<Vector2Int>(cells);
            var coverage = new Dictionary<Vector2Int, int>();
            foreach (var p in placements)
            {
                int mask = p.Kind == BlockPieceKind.Center ? 15 : p.Kind == BlockPieceKind.Edge ? 3 : p.Kind == BlockPieceKind.OuterCorner ? 2 : 7;
                mask = ((mask << p.QuarterTurns) | (mask >> (4 - p.QuarterTurns))) & 15;
                coverage.TryGetValue(p.PositionTwice, out int existing);
                Assert.That(existing & mask, Is.Zero, "Overlapping body tile quadrants");
                coverage[p.PositionTwice] = existing | mask;
            }
            foreach (var pair in coverage)
            {
                Vector2Int sw = new Vector2Int((pair.Key.x - 1) / 2, (pair.Key.y - 1) / 2);
                int expected = (occupied.Contains(sw) ? 1 : 0) | (occupied.Contains(sw + Vector2Int.right) ? 2 : 0) |
                    (occupied.Contains(sw + Vector2Int.one) ? 4 : 0) | (occupied.Contains(sw + Vector2Int.up) ? 8 : 0);
                Assert.That(pair.Value, Is.EqualTo(expected));
            }
            foreach (var cell in cells) for (int y = -1; y <= 1; y += 2) for (int x = -1; x <= 1; x += 2)
                Assert.That(coverage.ContainsKey(cell * 2 + new Vector2Int(x, y)), Is.True);
        }
    }
}
