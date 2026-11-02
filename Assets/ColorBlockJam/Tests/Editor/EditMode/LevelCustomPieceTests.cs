using System;
using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the level-piece footprint rules in plain C#: the piece limits, the rotate and mirror transforms, and the
    /// whole-level Re-stamp of the blocks with one key (DEC-193).
    /// </summary>
    public sealed class LevelCustomPieceTests
    {
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_domino = { Vector2Int.zero, Vector2Int.right };
        // The library L: a column of three with a foot to the right at the top.
        private static readonly Vector2Int[] s_libraryL =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2)
        };
        // An F pentomino: no library piece has it, and all 8 of its rotations and mirrors differ.
        private static readonly Vector2Int[] s_fPentomino =
        {
            new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1), new Vector2Int(1, 2), new Vector2Int(2, 2)
        };

        [Test]
        public void PieceLimitsRefuseEmptyRepeatedOversizedWideAndSplitFootprints()
        {
            Assert.That(LevelCustomPiece.TryValidate(null, out _), Is.False);
            Assert.That(LevelCustomPiece.TryValidate(new[] { Vector2Int.zero, Vector2Int.zero }, out _), Is.False);
            Assert.That(LevelCustomPiece.TryValidate(Line(6), out _), Is.False);
            Assert.That(LevelCustomPiece.TryValidate(new[] { Vector2Int.zero, new Vector2Int(2, 0) }, out _), Is.False);
            Assert.That(LevelCustomPiece.TryValidate(new[] { Vector2Int.zero, Vector2Int.one }, out _), Is.False,
                "Cells that touch only at a corner are split.");

            Vector2Int[] full = Box(5, 5);
            Assert.That(LevelCustomPiece.TryValidate(full, out _), Is.True, "25 cells in 5 x 5 fit.");
            Vector2Int[] tooMany = new Vector2Int[26];
            Array.Copy(full, tooMany, 25);
            tooMany[25] = new Vector2Int(5, 0);

            Assert.That(LevelCustomPiece.TryValidate(Line(6), out string issue), Is.False);
            Assert.That(issue, Does.Contain("6 x 1").And.Contain("5 x 5"));
            Assert.That(LevelCustomPiece.TryValidate(tooMany, out issue), Is.False);
            Assert.That(issue, Does.Contain("26 cells").And.Contain("25"));
        }

        [Test]
        public void TransformIndicesAreTheCanvasRotateAndMirrorHelpers()
        {
            Assert.That(LevelCustomPiece.Transform(s_libraryL, 0), Is.EqualTo(s_libraryL));
            Assert.That(LevelCustomPiece.Transform(s_libraryL, 1), Is.EqualTo(LevelPieceTools.RotateClockwise(s_libraryL)));
            Assert.That(LevelCustomPiece.Transform(s_libraryL, 4), Is.EqualTo(LevelPieceTools.FlipHorizontal(s_libraryL)));
            Vector2Int[] mirroredHalfTurn =
                LevelPieceTools.RotateClockwise(LevelPieceTools.RotateClockwise(LevelPieceTools.FlipHorizontal(s_libraryL)));
            Assert.That(LevelCustomPiece.Transform(s_libraryL, 6), Is.EqualTo(mirroredHalfTurn));

            for (int transform = 0; transform < LevelCustomPiece.k_transformCount; transform++)
            {
                Vector2Int[] turned = LevelCustomPiece.Transform(s_fPentomino, transform);
                Assert.That(LevelCustomPiece.FindTransform(s_fPentomino, turned), Is.EqualTo(transform),
                    "Each orientation of an asymmetric piece has one transform.");
            }
        }

        [Test]
        public void RestampKeepsEachBlocksRotationOrMirrorAndItsCorner()
        {
            // b1 is the old L turned clockwise, b2 the old L mirrored; the new footprint adds a cell under the column.
            Vector2Int[] newL = { new Vector2Int(0, -1), new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2),
                new Vector2Int(1, 2) };
            Vector2Int[] turned = LevelPieceTools.RotateClockwise(s_libraryL);
            Vector2Int[] mirrored = LevelPieceTools.FlipHorizontal(s_libraryL);
            LevelData level = CreateLevel(8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), turned),
                new LevelBlockData("b2", "Blue", new Vector2Int(5, 2), mirrored),
                new LevelBlockData("b3", "Pink", new Vector2Int(0, 7), s_single));

            Assert.That(LevelShapeRestamp.TryRestamp(level, LevelTestShapes.Key(s_libraryL), s_libraryL, newL, "L piece",
                out LevelData result, out int count, out string issue), Is.True, issue);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(result.GetBlockAt(0).Key, Is.EqualTo(LevelTestShapes.Key(newL)));
            Assert.That(Cells(result.GetBlockAt(0)), Is.EqualTo(LevelCustomPiece.Transform(newL, 1)));
            Assert.That(Cells(result.GetBlockAt(1)), Is.EqualTo(LevelCustomPiece.Transform(newL, 4)));
            Assert.That(result.GetBlockAt(1).Origin, Is.EqualTo(new Vector2Int(5, 2)), "Origin and corner stay.");
            Assert.That(result.GetBlockAt(2).Key, Is.EqualTo(LevelTestShapes.Key(s_single)), "Other pieces are untouched.");
            Assert.That(level.GetBlockAt(0).Key, Is.EqualTo(LevelTestShapes.Key(s_libraryL)),
                "The given level never changes.");
        }

        [Test]
        public void RestampIsRefusedWholeAndListsEveryBlockThatWouldNotFit()
        {
            LevelData level = CreateLevel(4, 4,
                new LevelBlockData("b1", "Red", new Vector2Int(0, 0), s_domino),
                new LevelBlockData("b2", "Blue", new Vector2Int(2, 3), s_domino),
                new LevelBlockData("b3", "Pink", new Vector2Int(1, 2), s_domino));
            Vector2Int[] triple = Line(3);

            Assert.That(LevelShapeRestamp.TryRestamp(level, LevelTestShapes.Key(s_domino), s_domino, triple, "Domino",
                out LevelData result, out int count, out string issue), Is.False);

            Assert.That(result, Is.Null);
            Assert.That(count, Is.Zero);
            Assert.That(issue, Does.Contain("b2").And.Contain("off the 4 x 4 board"));
            Assert.That(issue, Does.Not.Contain("b1:"), "b1 fits, so it is not listed.");
        }

        [Test]
        public void RestampOntoABoardObjectIsRefusedWholeAndNamesIt()
        {
            LevelCellObjectData barrier = LevelEffectTests.Object("o1",
                LevelEffectTests.Effect("barrier"), new Vector2Int(2, 0));
            LevelData level = new LevelData("custom-fixture", "Custom fixture", 5, 5, 60, Array.Empty<Vector2Int>(),
                new LevelDoorData[0],
                new[]
                {
                    new LevelBlockData("b1", "Red", Vector2Int.zero, s_domino),
                    new LevelBlockData("b2", "Blue", new Vector2Int(0, 2), s_domino)
                },
                new[] { barrier });

            Assert.That(LevelShapeRestamp.TryRestamp(level, LevelTestShapes.Key(s_domino), s_domino, Line(3), "Domino",
                out LevelData result, out int count, out string issue), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(count, Is.Zero);
            Assert.That(issue, Does.Contain("b1: Cell (2,0) is under board object o1."));
            Assert.That(issue, Does.Not.Contain("b2:"), "b2 fits.");

            Assert.That(LevelShapeRestamp.TryRestampBlock(level, 0, s_domino, Line(3), out result,
                out issue), Is.False);
            Assert.That(issue, Is.EqualTo("b1: Cell (2,0) is under board object o1."));
            Assert.That(LevelShapeRestamp.TryRestampBlock(level, 1, s_domino, Line(3), out result,
                out issue), Is.True, issue);
            Assert.That(result.GetBlockAt(1).Key, Is.EqualTo(LevelTestShapes.Key(Line(3))));
            Assert.That(result.GetBlockAt(0).Key, Is.EqualTo(LevelTestShapes.Key(s_domino)), "Only that block changes.");
        }

        [Test]
        public void RestampThatOnlyOverlapsAnotherBlockIsAppliedAndLeavesTheOverlapToResolve()
        {
            // The user's case of 2026-09-26 (contract D3-B): two squares of one piece, b1 above b2. The new footprint
            // adds a cell on top of the left column, so b2's new cell lands on b1's bottom-left cell (0,2).
            Vector2Int[] square = { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.one };
            Vector2Int[] grown = { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.one, new Vector2Int(0, 2) };
            LevelData level = CreateLevel(6, 6,
                new LevelBlockData("b1", "Red", new Vector2Int(0, 2), square),
                new LevelBlockData("b2", "Red", Vector2Int.zero, square));

            Assert.That(LevelShapeRestamp.TryRestamp(level, LevelTestShapes.Key(square), square, grown, "Square",
                out LevelData result, out int count, out string issue), Is.True, issue);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(Cells(result.GetBlockAt(0)), Is.EquivalentTo(grown));
            Assert.That(Cells(result.GetBlockAt(1)), Is.EquivalentTo(grown), "b2 is re-stamped, not refused.");
            Assert.That(LevelBlockOverlap.Describe(result), Is.EqualTo("b1 and b2"));
            Assert.That(LevelBlockOverlap.Describe(level), Is.Null, "The given level never changes.");

            Assert.That(LevelShapeRestamp.TryRestampBlock(level, 1, square, grown, out result,
                out issue), Is.True, issue);
            Assert.That(LevelBlockOverlap.Describe(result), Is.EqualTo("b1 and b2"), "One block may overlap too.");
        }

        [Test]
        public void RestampWithoutTheOldFootprintKeepsTheOrientationThatOverlapsMost()
        {
            Vector2Int[] vertical = { Vector2Int.zero, Vector2Int.up };
            LevelData level = CreateLevel(5, 5, new LevelBlockData("b1", "Red", new Vector2Int(2, 0), vertical));

            Assert.That(LevelShapeRestamp.TryRestamp(level, LevelTestShapes.Key(vertical), null, Line(3), "Double",
                out LevelData result, out _, out string issue), Is.True, issue);

            Assert.That(Cells(result.GetBlockAt(0)),
                Is.EquivalentTo(new[] { Vector2Int.zero, Vector2Int.up, new Vector2Int(0, 2) }), "It stays vertical.");
        }

        private static LevelData CreateLevel(int width, int height, params LevelBlockData[] blocks)
        {
            return new LevelData("custom-fixture", "Custom fixture", width, height, 60, new LevelDoorData[0], blocks);
        }

        private static Vector2Int[] Cells(LevelBlockData block)
        {
            Vector2Int[] cells = new Vector2Int[block.CellCount];
            for (int index = 0; index < cells.Length; index++)
            {
                cells[index] = block.GetCellAt(index);
            }

            return cells;
        }

        private static Vector2Int[] Line(int length)
        {
            Vector2Int[] cells = new Vector2Int[length];
            for (int index = 0; index < length; index++)
            {
                cells[index] = new Vector2Int(index, 0);
            }

            return cells;
        }

        private static Vector2Int[] Box(int width, int height)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }

            return cells.ToArray();
        }
    }
}
