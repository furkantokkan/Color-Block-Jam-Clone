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
    /// Verifies the pure Pen template edit layer in plain C#: the pending footprint record with the blocks' places when
    /// the edit began, the mapping of a stroke into library orientation, the whole-level stroke and Discard commands,
    /// which move only the template's blocks (DEC-193), and the catalog overlay. Nothing here
    /// reads or writes the piece library or a level file.
    /// </summary>
    public sealed class LevelTemplateEditTests
    {
        private const string k_templateName = "LPiece";
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_domino = { Vector2Int.zero, Vector2Int.right };
        // The library L: a column of three with a foot to the right at the top; all 8 rotations and mirrors differ.
        private static readonly Vector2Int[] s_libraryL =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2)
        };
        // The L grown by (1,1) inside its own bounding box: a P pentomino, whose 8 rotations and mirrors differ too.
        private static readonly Vector2Int[] s_grownP =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(1, 1)
        };
        // The L grown by (0,3) on top of its column.
        private static readonly Vector2Int[] s_tallL =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(0, 3)
        };
        // Declared after the cells, so the static initializers read them.
        private static readonly ShapeKey s_libraryKey = LevelTestShapes.Key(s_libraryL);

        [Test]
        public void EditCopiesAndNormalizesBothFootprintsAndSurvivesJson()
        {
            Vector2Int[] shiftedBase = Shift(s_libraryL, new Vector2Int(2, 3));
            Vector2Int[] shiftedCells = Shift(s_grownP, new Vector2Int(-1, 4));

            LevelTemplateEdit edit = new LevelTemplateEdit(s_libraryKey, "L piece", shiftedBase, shiftedCells);
            shiftedBase[0] = new Vector2Int(9, 9);
            shiftedCells[0] = new Vector2Int(9, 9);

            Assert.That(edit.TemplateKey, Is.EqualTo(s_libraryKey));
            Assert.That(edit.DisplayName, Is.EqualTo("L piece"));
            Assert.That(edit.BaseCells, Is.EqualTo(s_libraryL), "Shifted to (0, 0) and copied.");
            Assert.That(edit.Cells, Is.EqualTo(s_grownP), "Shifted to (0, 0) and copied.");
            Assert.That(new LevelTemplateEdit(s_libraryKey, " ", s_libraryL, s_grownP).DisplayName,
                Is.EqualTo(s_libraryKey.ToString()), "A blank display name falls back to the key.");
            Assert.Throws<ArgumentException>(() => new LevelTemplateEdit(default, "L piece", s_libraryL, s_grownP));
            Assert.Throws<ArgumentException>(() =>
                new LevelTemplateEdit(s_libraryKey, "L piece", s_libraryL, Array.Empty<Vector2Int>()));

            LevelTemplateEdit copy = JsonUtility.FromJson<LevelTemplateEdit>(JsonUtility.ToJson(edit));

            Assert.That(copy.TemplateKey, Is.EqualTo(s_libraryKey));
            Assert.That(copy.DisplayName, Is.EqualTo("L piece"));
            Assert.That(copy.BaseCells, Is.EqualTo(s_libraryL));
            Assert.That(copy.Cells, Is.EqualTo(s_grownP));
        }

        [Test]
        public void EditThatReturnsToAnyRotationOrMirrorOfTheBaseIsNoChange()
        {
            LevelTemplateEdit edit = new LevelTemplateEdit(s_libraryKey, "L piece", s_libraryL, s_grownP);
            Assert.That(edit.IsNoChange, Is.False);

            for (int transform = 0; transform < LevelCustomPiece.k_transformCount; transform++)
            {
                LevelTemplateEdit back = edit.WithCells(LevelCustomPiece.Transform(s_libraryL, transform));
                Assert.That(back.IsNoChange, Is.True, $"Transform {transform} is the base again.");
                Assert.That(back.TemplateKey, Is.EqualTo(s_libraryKey));
                Assert.That(back.DisplayName, Is.EqualTo("L piece"));
                Assert.That(back.BaseCells, Is.EqualTo(s_libraryL), "WithCells keeps the base.");
            }

            // Grow the foot's column by (1,0), then take the old foot (1,2) off: the block is a mirrored L again.
            LevelData level = CreateLevel(8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(3, 3), s_libraryL));
            List<Vector2Int> cells = BoardCells(level.GetBlockAt(0));
            cells.Add(new Vector2Int(4, 3));
            Assert.That(LevelTemplateCommands.TryEdit(level, 0, cells, k_templateName, s_libraryL,
                Members(level, s_libraryL), out LevelData grown, out Vector2Int[] grownCells, out string issue), Is.True,
                issue);
            Assert.That(edit.WithCells(grownCells).IsNoChange, Is.False);

            cells.Remove(new Vector2Int(4, 5));
            Assert.That(LevelTemplateCommands.TryEdit(grown, 0, cells, k_templateName, grownCells,
                Members(grown, grownCells), out _, out Vector2Int[] backCells, out issue), Is.True, issue);

            Assert.That(edit.WithCells(backCells).IsNoChange, Is.True, "A turned or mirrored base drops the edit.");
            Assert.That(LevelCustomPiece.FindTransform(s_libraryL, backCells), Is.Not.EqualTo(0),
                "The footprint is not the base in its own orientation.");
        }

        [Test]
        public void StrokeMapsBackToLibraryOrientationForEveryRotationAndMirror()
        {
            Vector2Int origin = new Vector2Int(2, 2);
            for (int transform = 0; transform < LevelCustomPiece.k_transformCount; transform++)
            {
                // The P fills the L's bounding box, so both turn into one frame.
                Vector2Int[] before = LevelCustomPiece.Transform(s_libraryL, transform);
                Vector2Int[] after = Shift(LevelCustomPiece.Transform(s_grownP, transform), origin);
                LevelData level = CreateLevel(8, 8, new LevelBlockData("b1", "Red", origin, before));

                Assert.That(LevelTemplateCommands.TryEdit(level, 0, after, k_templateName, s_libraryL,
                    Members(level, s_libraryL), out LevelData result, out Vector2Int[] libraryCells, out string issue),
                    Is.True, $"Transform {transform}: {issue}");

                Assert.That(libraryCells, Is.EquivalentTo(s_grownP), $"Transform {transform}.");
                Assert.That(Cells(result.GetBlockAt(0)), Is.EquivalentTo(LevelCustomPiece.Normalize(after)));
                Assert.That(result.GetBlockAt(0).Origin, Is.EqualTo(origin));

                // A cell outside the old bounding box shifts the frame; the mapping still lands on the library L.
                Vector2Int[] tall = LevelCustomPiece.Transform(s_tallL, transform);
                Vector2Int offset = FindOffset(before, tall);
                Assert.That(LevelTemplateEdit.ToLibraryOrientation(s_libraryL, Shift(before, origin + offset),
                    Shift(tall, origin)), Is.EquivalentTo(s_tallL), $"Transform {transform}, tall.");
            }
        }

        [Test]
        public void LibraryOrientationIsNullWhenTheBlockDoesNotMatchOrIsEmpty()
        {
            Assert.That(LevelTemplateEdit.ToLibraryOrientation(s_libraryL, s_domino, s_grownP), Is.Null);
            Assert.That(LevelTemplateEdit.ToLibraryOrientation(s_libraryL, s_libraryL, Array.Empty<Vector2Int>()),
                Is.Null);
            Assert.That(LevelTemplateEdit.ToLibraryOrientation(null, s_libraryL, s_grownP), Is.Null);
        }

        [Test]
        public void StrokeRestampsEveryOtherTemplateBlockInItsOwnOrientationAndKeepsTheDrawnBlock()
        {
            LevelEffectData ice = LevelEffectTests.Effect("ice", ("clearsToThaw", "2"));
            LevelData level = CreateLevel(8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL, new[] { ice }),
                new LevelBlockData("b2", "Blue", new Vector2Int(5, 1), Turned(s_libraryL)),
                new LevelBlockData("b3", "Pink", new Vector2Int(1, 5), Mirrored(s_libraryL)),
                new LevelBlockData("b4", "Yellow", new Vector2Int(7, 7), s_single));
            List<Vector2Int> cells = BoardCells(level.GetBlockAt(0));
            cells.Add(new Vector2Int(2, 2));

            Assert.That(LevelTemplateCommands.TryEdit(level, 0, cells, k_templateName, s_libraryL,
                Members(level, s_libraryL), out LevelData result, out Vector2Int[] libraryCells, out string issue),
                Is.True, issue);

            Assert.That(issue, Is.Null);
            Assert.That(libraryCells, Is.EquivalentTo(s_grownP));
            LevelBlockData drawn = result.GetBlockAt(0);
            Assert.That(drawn.Id, Is.EqualTo("b1"));
            Assert.That(drawn.ColorId, Is.EqualTo("Red"));
            Assert.That(drawn.Origin, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(Cells(drawn), Is.EquivalentTo(s_grownP));
            Assert.That(drawn.EffectCount, Is.EqualTo(1));
            Assert.That(drawn.GetEffectAt(0).Type, Is.EqualTo("ice"), "Effects stay.");

            Assert.That(result.GetBlockAt(1).ColorId, Is.EqualTo("Blue"), "Colour is per block.");
            Assert.That(Cells(result.GetBlockAt(1)), Is.EqualTo(Turned(s_grownP)));
            Assert.That(result.GetBlockAt(1).Origin, Is.EqualTo(new Vector2Int(5, 1)));
            Assert.That(Cells(result.GetBlockAt(2)), Is.EqualTo(Mirrored(s_grownP)));
            Assert.That(Cells(result.GetBlockAt(3)), Is.EqualTo(s_single), "Other shapes are untouched.");
            Assert.That(LevelBlockOverlap.Describe(result), Is.Null);
            Assert.That(Cells(level.GetBlockAt(1)), Is.EqualTo(Turned(s_libraryL)),
                "The given level never changes.");
        }

        [Test]
        public void StrokeOnASymmetricTemplateGivesAnIdenticallyTurnedBlockTheIdenticalFootprint()
        {
            // Two upright blocks of a flat domino template: a half turn maps the domino onto itself, so the lowest
            // transform from a block to the template and the one Re-stamp uses back can differ by that half turn.
            LevelData level = CreateLevel(8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), Turned(s_domino)),
                new LevelBlockData("b2", "Blue", new Vector2Int(5, 1), Turned(s_domino)),
                new LevelBlockData("b3", "Pink", new Vector2Int(1, 5), s_domino));
            List<Vector2Int> cells = BoardCells(level.GetBlockAt(0));
            cells.Add(new Vector2Int(2, 1));

            Assert.That(LevelTemplateCommands.TryEdit(level, 0, cells, k_templateName, s_domino,
                Members(level, s_domino), out LevelData result, out Vector2Int[] libraryCells, out string issue),
                Is.True, issue);

            Vector2Int[] drawn = LevelCustomPiece.Normalize(cells);
            Assert.That(Cells(result.GetBlockAt(0)), Is.EquivalentTo(drawn));
            Assert.That(Cells(result.GetBlockAt(1)), Is.EquivalentTo(drawn),
                "A block turned like the drawn one gets its footprint, not the footprint turned half way round.");
            Assert.That(result.GetBlockAt(1).Origin, Is.EqualTo(new Vector2Int(5, 1)));
            Assert.That(Cells(result.GetBlockAt(2)), Is.EquivalentTo(libraryCells),
                "A block in the template's own orientation gets the library-orientation footprint.");
            Assert.That(LevelTemplateEdit.ToLibraryOrientation(s_domino, Shift(Turned(s_domino), Vector2Int.one),
                cells), Is.EquivalentTo(libraryCells));
        }

        [Test]
        public void StrokeIsRefusedWholeWhenAnyTemplateBlockWouldLeaveTheBoardOrCoverAVoidOrABoardObject()
        {
            // Each stroke adds (1,4) on top of b1's column; b2 in the same orientation then needs its (5, y + 3).
            LevelData offBoard = CreateLevel(8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL),
                new LevelBlockData("b2", "Blue", new Vector2Int(5, 5), s_libraryL));
            AssertRefused(offBoard, "b2", "off the 8 x 8 board");

            LevelData onVoid = new LevelData("template-fixture", "Template fixture", 8, 8, 60,
                new[] { new Vector2Int(5, 4) }, new LevelDoorData[0], TwoBlocks(), Array.Empty<LevelCellObjectData>());
            AssertRefused(onVoid, "b2", "Cell (5,4) is a void.");

            LevelData onObject = new LevelData("template-fixture", "Template fixture", 8, 8, 60,
                Array.Empty<Vector2Int>(), new LevelDoorData[0], TwoBlocks(),
                new[] { LevelEffectTests.Object("o1", LevelEffectTests.Effect("barrier"), new Vector2Int(5, 4)) });
            AssertRefused(onObject, "b2: Cell (5,4) is under board object o1.", "would not fit");

            LevelData drawnOnObject = new LevelData("template-fixture", "Template fixture", 8, 8, 60,
                Array.Empty<Vector2Int>(), new LevelDoorData[0], TwoBlocks(),
                new[] { LevelEffectTests.Object("o1", LevelEffectTests.Effect("barrier"), new Vector2Int(1, 4)) });
            AssertRefused(drawnOnObject, "b1: Cell (1,4) is under board object o1.", "b1");

            LevelData drawnOnBlock = CreateLevel(8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL),
                new LevelBlockData("b2", "Blue", new Vector2Int(1, 4), s_single));
            AssertRefused(drawnOnBlock, "b1", "Overlaps Blue block b2 at (1,4).");
        }

        [Test]
        public void StrokeThatOnlyMakesAnotherTemplateBlockOverlapIsAppliedAndTheOverlapIsNamed()
        {
            LevelData level = CreateLevel(8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL),
                new LevelBlockData("b2", "Blue", new Vector2Int(5, 1), s_libraryL),
                new LevelBlockData("b3", "Pink", new Vector2Int(5, 4), s_single));

            Assert.That(LevelTemplateCommands.TryEdit(level, 0, TallStroke(level), k_templateName, s_libraryL,
                Members(level, s_libraryL), out LevelData result, out Vector2Int[] libraryCells, out string issue),
                Is.True, issue);

            Assert.That(libraryCells, Is.EquivalentTo(s_tallL));
            Assert.That(Cells(result.GetBlockAt(1)), Is.EquivalentTo(s_tallL), "b2 is re-stamped, not refused.");
            Assert.That(LevelBlockOverlap.Describe(result), Is.EqualTo("b2 and b3"));
        }

        [Test]
        public void StrokeOnABlockThatIsNotTheTemplateIsRefused()
        {
            LevelData level = CreateLevel(8, 8,
                new LevelBlockData("b1", "Red", Vector2Int.zero, s_domino),
                new LevelBlockData("b2", "Blue", new Vector2Int(5, 5), s_single));
            Vector2Int[] line = { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) };

            Assert.That(LevelTemplateCommands.TryEdit(level, 1, new[] { new Vector2Int(5, 5), new Vector2Int(6, 5) },
                k_templateName, s_libraryL, Members(level, s_libraryL), out LevelData result,
                out Vector2Int[] libraryCells, out string issue), Is.False);
            Assert.That(issue, Is.EqualTo("b2 is not a block of LPiece."));
            Assert.That(result, Is.Null);
            Assert.That(libraryCells, Is.Null);

            Assert.That(LevelTemplateCommands.TryEdit(level, 0, new[] { Vector2Int.zero, new Vector2Int(2, 0) },
                k_templateName, s_domino, Members(level, s_domino), out result, out libraryCells, out issue), Is.False);
            Assert.That(issue, Does.StartWith("b1 changed nothing.").And.Contain("edge to edge"));

            Assert.That(LevelTemplateCommands.TryEdit(level, 5, line, k_templateName, s_domino,
                Members(level, s_domino), out result, out libraryCells, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Nothing to edit."));
        }

        [Test]
        public void DiscardRestampsEveryTemplateBlockBackToTheBaseFootprint()
        {
            LevelData level = CreateLevel(8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL),
                new LevelBlockData("b2", "Blue", new Vector2Int(5, 1), Turned(s_libraryL)),
                new LevelBlockData("b3", "Pink", new Vector2Int(1, 5), Mirrored(s_libraryL)));
            List<Vector2Int> cells = BoardCells(level.GetBlockAt(0));
            cells.Add(new Vector2Int(2, 2));
            List<string> members = Members(level, s_libraryL);
            Assert.That(LevelTemplateCommands.TryEdit(level, 0, cells, k_templateName, s_libraryL, members,
                out LevelData edited, out Vector2Int[] libraryCells, out string issue), Is.True, issue);
            LevelTemplateEdit edit = new LevelTemplateEdit(s_libraryKey, "L piece", s_libraryL, libraryCells,
                addedIds: members);

            Assert.That(LevelTemplateCommands.TryDiscard(edited, edit, out LevelData result, out issue), Is.True,
                issue);

            Assert.That(Cells(result.GetBlockAt(0)), Is.EquivalentTo(s_libraryL));
            Assert.That(result.GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(Cells(result.GetBlockAt(1)), Is.EqualTo(Turned(s_libraryL)));
            Assert.That(Cells(result.GetBlockAt(2)), Is.EqualTo(Mirrored(s_libraryL)));
            Assert.That(result.GetBlockAt(1).ColorId, Is.EqualTo("Blue"));

            LevelData other = CreateLevel(5, 5, new LevelBlockData("b1", "Red", Vector2Int.zero, s_single));
            Assert.That(LevelTemplateCommands.TryDiscard(other, edit, out result, out issue), Is.True, issue);
            Assert.That(result, Is.SameAs(other), "A level without template blocks is returned as given.");
            Assert.That(LevelTemplateCommands.TryDiscard(edited, null, out result, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Nothing to discard."));
        }

        [Test]
        public void DiscardPutsEveryBlockTheEditBeganWithBackWhereItWas()
        {
            // D-001: a cell left of b1 moves its bounding box and origin one cell left, and a re-stamp keeps that corner.
            LevelEffectData ice = LevelEffectTests.Effect("ice", ("clearsToThaw", "2"));
            LevelData level = CreateLevel(8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(3, 3), s_libraryL, new[] { ice }),
                new LevelBlockData("b2", "Blue", new Vector2Int(5, 0), Turned(s_libraryL)),
                new LevelBlockData("b3", "Pink", Vector2Int.zero, s_single));
            LevelTemplateBlockOrigin[] originals = LevelTemplateEdit.CaptureOriginals(level, s_libraryL);
            List<Vector2Int> cells = BoardCells(level.GetBlockAt(0));
            cells.Add(new Vector2Int(2, 3));
            Assert.That(LevelTemplateCommands.TryEdit(level, 0, cells, k_templateName, s_libraryL,
                Members(level, s_libraryL), out LevelData edited, out Vector2Int[] libraryCells, out string issue),
                Is.True, issue);
            Assume.That(edited.GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(2, 3)), "The grown box starts further left.");
            // A swatch recolours b1 during the edit.
            LevelBlockData[] blocks = LevelRecordCopy.GetBlocks(edited);
            blocks[0] = LevelRecordCopy.WithColor(blocks[0], "Green");
            edited = LevelRecordCopy.WithBlocks(edited, blocks);
            LevelTemplateEdit edit = new LevelTemplateEdit(s_libraryKey, "L piece", s_libraryL, libraryCells, originals);

            Assert.That(LevelTemplateCommands.TryDiscard(edited, edit, out LevelData result, out issue), Is.True, issue);

            for (int index = 0; index < 2; index++)
            {
                Assert.That(result.GetBlockAt(index).Origin, Is.EqualTo(level.GetBlockAt(index).Origin),
                    $"{level.GetBlockAt(index).Id} is back at its origin.");
                Assert.That(Cells(result.GetBlockAt(index)), Is.EqualTo(Cells(level.GetBlockAt(index))),
                    $"{level.GetBlockAt(index).Id} has its own cells again, in their order.");
            }

            Assert.That(result.GetBlockAt(0).ColorId, Is.EqualTo("Green"), "The colour stays as it is now.");
            Assert.That(result.GetBlockAt(0).EffectCount, Is.EqualTo(1), "Effects stay.");
            Assert.That(Cells(result.GetBlockAt(2)), Is.EqualTo(s_single), "Other pieces are untouched.");
            Assert.That(LevelBlockOverlap.Describe(result), Is.Null);

            LevelTemplateEdit unrecorded = new LevelTemplateEdit(s_libraryKey, "L piece", s_libraryL, libraryCells,
                addedIds: new[] { "b1", "b2" });
            Assert.That(LevelTemplateCommands.TryDiscard(edited, unrecorded, out LevelData restamped, out issue), Is.True,
                issue);
            Assert.That(restamped.GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(2, 3)),
                "Without the record a re-stamp keeps the grown corner, the defect's cause.");
        }

        [Test]
        public void DiscardReStampsABlockPlacedDuringTheEditAndRefusesOneThatCannotTakeTheBaseNamingIt()
        {
            LevelData level = CreateLevel(8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(3, 3), s_libraryL));
            LevelTemplateBlockOrigin[] originals = LevelTemplateEdit.CaptureOriginals(level, s_libraryL);
            List<Vector2Int> cells = BoardCells(level.GetBlockAt(0));
            cells.Add(new Vector2Int(2, 3));
            Assert.That(LevelTemplateCommands.TryEdit(level, 0, cells, k_templateName, s_libraryL,
                Members(level, s_libraryL), out LevelData edited, out Vector2Int[] libraryCells, out string issue),
                Is.True, issue);
            // b2 is placed with the pending footprint while the edit is open, so it joins the template's blocks.
            LevelBlockData[] blocks =
            {
                edited.GetBlockAt(0),
                new LevelBlockData("b2", "Blue", new Vector2Int(0, 5), libraryCells)
            };
            edited = LevelRecordCopy.WithBlocks(edited, blocks);
            LevelTemplateEdit edit = new LevelTemplateEdit(s_libraryKey, "L piece", s_libraryL, libraryCells, originals,
                new[] { "b2" });

            Assert.That(LevelTemplateCommands.TryDiscard(edited, edit, out LevelData result, out issue), Is.True, issue);

            Assert.That(result.GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(3, 3)));
            Assert.That(Cells(result.GetBlockAt(1)), Is.EquivalentTo(s_libraryL),
                "A block the edit did not begin with takes the base in its own orientation.");
            Assert.That(result.GetBlockAt(1).Origin, Is.EqualTo(new Vector2Int(0, 5)), "At its own corner.");

            // A column of three at the right edge cannot take the two-wide base back.
            Vector2Int[] column = { Vector2Int.zero, Vector2Int.up, new Vector2Int(0, 2) };
            LevelData shrunk = CreateLevel(8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), column),
                new LevelBlockData("b2", "Blue", new Vector2Int(7, 0), column));
            LevelTemplateBlockOrigin[] shrunkOriginals =
            {
                new LevelTemplateBlockOrigin("b1", new Vector2Int(1, 1), s_libraryL)
            };
            string before = JsonUtility.ToJson(shrunk);

            Assert.That(LevelTemplateCommands.TryDiscard(shrunk,
                new LevelTemplateEdit(s_libraryKey, "L piece", s_libraryL, column, shrunkOriginals, new[] { "b2" }),
                out result, out issue), Is.False);

            Assert.That(result, Is.Null);
            Assert.That(issue, Does.StartWith("1 block(s) of L piece would not fit: ").And.Contain("b2"));
            Assert.That(issue, Does.Not.Contain("b1"), "The block that goes back exactly is never refused.");
            Assert.That(JsonUtility.ToJson(shrunk), Is.EqualTo(before), "The given level never changes.");
        }

        [Test]
        public void CaptureOriginalsKeepsTheTemplatesBlocksShapedLikeTheBaseAndTheEditKeepsThem()
        {
            LevelData level = CreateLevel(8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(5, 5), Mirrored(s_libraryL)),
                new LevelBlockData("b2", "Blue", Vector2Int.zero, s_single),
                new LevelBlockData("b3", "Pink", new Vector2Int(1, 1), s_grownP),
                new LevelBlockData("b4", "Yellow", new Vector2Int(0, 5), s_libraryL));

            LevelTemplateBlockOrigin[] originals = LevelTemplateEdit.CaptureOriginals(level, s_libraryL);

            Assert.That(originals, Has.Length.EqualTo(2), "Blocks of other pieces are left out.");
            Assert.That(originals[0].BlockId, Is.EqualTo("b1"));
            Assert.That(originals[0].Origin, Is.EqualTo(new Vector2Int(5, 5)));
            Assert.That(originals[0].Cells, Is.EqualTo(Mirrored(s_libraryL)));
            Assert.That(originals[1].BlockId, Is.EqualTo("b4"));
            Assert.That(LevelTemplateEdit.CaptureOriginals(null, s_libraryL), Is.Empty);
            Assert.That(LevelTemplateEdit.CaptureOriginals(level, Array.Empty<Vector2Int>()), Is.Empty);

            LevelTemplateEdit edit = new LevelTemplateEdit(s_libraryKey, "L piece", s_libraryL, s_grownP, originals);
            originals[0] = null;
            Assert.That(edit.Originals[0].BlockId, Is.EqualTo("b1"), "The edit keeps its own list.");
            LevelTemplateEdit copy = JsonUtility.FromJson<LevelTemplateEdit>(JsonUtility.ToJson(edit.WithCells(s_tallL)));
            Assert.That(copy.Originals.Count, Is.EqualTo(2), "WithCells and the draft's JSON copy keep the record.");
            Assert.That(copy.Originals[1].BlockId, Is.EqualTo("b4"));
            Assert.That(copy.Originals[1].Origin, Is.EqualTo(new Vector2Int(0, 5)));
            Assert.That(copy.Originals[1].Cells, Is.EqualTo(s_libraryL));
            Assert.That(new LevelTemplateEdit(s_libraryKey, "L piece", s_libraryL, s_grownP).Originals, Is.Empty);
        }

        [Test]
        public void OverlayReplacesCellsByTemplateKeyAndKeepsIndexAndDisplayName()
        {
            Vector2Int[] line = { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) };
            LevelShapeChoice single = LevelTestShapes.Template("Single", s_single);
            LevelShapeCatalog catalog = LevelShapeCatalog.Create(new[]
            {
                single,
                LevelTestShapes.Template("L piece", s_libraryL),
                LevelTestShapes.Template("Hidden piece", s_domino)
            });
            LevelTemplateEdit[] edits =
            {
                new LevelTemplateEdit(s_libraryKey, "Renamed", s_libraryL, s_grownP),
                new LevelTemplateEdit(LevelTestShapes.Key(s_domino), "Hidden piece", s_domino, line),
                new LevelTemplateEdit(LevelTestShapes.Key(s_tallL), "Unknown", s_tallL, s_domino)
            };

            LevelShapeCatalog overlaid = LevelTemplateCommands.Overlay(catalog, edits);

            Assert.That(overlaid, Is.Not.SameAs(catalog));
            Assert.That(overlaid.Error, Is.Null);
            Assert.That(overlaid.Count, Is.EqualTo(3), "An edit the catalog lacks adds nothing.");
            Assert.That(overlaid.Get(0), Is.SameAs(single), "Records without an edit are kept.");
            Assert.That(overlaid.IndexOfTemplate(s_libraryKey, null), Is.EqualTo(1));
            Assert.That(overlaid.Get(1).DisplayName, Is.EqualTo("L piece"), "The library's display name stays.");
            Assert.That(overlaid.Get(1).Cells, Is.EqualTo(s_grownP));
            Assert.That(overlaid.Get(2).Cells, Is.EqualTo(line));
            Assert.That(catalog.Get(1).Cells, Is.EqualTo(s_libraryL), "The given catalog never changes.");

            LevelShapeCatalog later = LevelTemplateCommands.Overlay(catalog,
                new[] { edits[0], edits[0].WithCells(s_tallL) });
            Assert.That(later.Get(1).Cells, Is.EqualTo(s_tallL), "A later edit of the same template wins.");

            Assert.That(LevelTemplateCommands.Overlay(catalog, Array.Empty<LevelTemplateEdit>()), Is.SameAs(catalog));
            Assert.That(LevelTemplateCommands.Overlay(catalog, null), Is.SameAs(catalog));
            Assert.That(LevelTemplateCommands.Overlay(catalog, new[] { edits[2] }), Is.SameAs(catalog));
            Assert.That(LevelTemplateCommands.Overlay(LevelShapeCatalog.Missing, edits),
                Is.SameAs(LevelShapeCatalog.Missing), "A catalog that failed to load is returned unchanged.");
        }

        private static void AssertRefused(LevelData level, string expected, string alsoExpected)
        {
            string before = JsonUtility.ToJson(level);

            Assert.That(LevelTemplateCommands.TryEdit(level, 0, TallStroke(level), k_templateName, s_libraryL,
                Members(level, s_libraryL), out LevelData result, out Vector2Int[] libraryCells, out string issue),
                Is.False);

            Assert.That(result, Is.Null);
            Assert.That(libraryCells, Is.Null);
            Assert.That(issue, Does.Contain(expected).And.Contain(alsoExpected));
            Assert.That(JsonUtility.ToJson(level), Is.EqualTo(before), "The given level never changes.");
        }

        // The template's blocks at the edit's first stroke: the level's blocks keyed like its footprint (DEC-193).
        private static List<string> Members(LevelData level, IReadOnlyList<Vector2Int> footprint)
        {
            return LevelBlockQueries.GetBlockIds(level, LevelTestShapes.Key(footprint));
        }

        // b1's board cells plus (1,4) on top of its column: the library L becomes the tall L.
        private static List<Vector2Int> TallStroke(LevelData level)
        {
            List<Vector2Int> cells = BoardCells(level.GetBlockAt(0));
            cells.Add(new Vector2Int(1, 4));
            return cells;
        }

        private static LevelBlockData[] TwoBlocks()
        {
            return new[]
            {
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL),
                new LevelBlockData("b2", "Blue", new Vector2Int(5, 1), s_libraryL)
            };
        }

        private static LevelData CreateLevel(int width, int height, params LevelBlockData[] blocks)
        {
            return new LevelData("template-fixture", "Template fixture", width, height, 60, new LevelDoorData[0],
                blocks);
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

        private static List<Vector2Int> BoardCells(LevelBlockData block)
        {
            List<Vector2Int> cells = new List<Vector2Int>(block.CellCount + 1);
            for (int index = 0; index < block.CellCount; index++)
            {
                cells.Add(block.Origin + block.GetCellAt(index));
            }

            return cells;
        }

        // Transform 1: the footprint turned clockwise once.
        private static Vector2Int[] Turned(IReadOnlyList<Vector2Int> cells)
        {
            return LevelCustomPiece.Transform(cells, 1);
        }

        // Transform 4: the footprint mirrored horizontally.
        private static Vector2Int[] Mirrored(IReadOnlyList<Vector2Int> cells)
        {
            return LevelCustomPiece.Transform(cells, 4);
        }

        private static Vector2Int[] Shift(IReadOnlyList<Vector2Int> cells, Vector2Int offset)
        {
            Vector2Int[] shifted = new Vector2Int[cells.Count];
            for (int index = 0; index < shifted.Length; index++)
            {
                shifted[index] = cells[index] + offset;
            }

            return shifted;
        }

        // The offset that places every inner cell on an outer cell.
        private static Vector2Int FindOffset(Vector2Int[] inner, Vector2Int[] outer)
        {
            for (int x = -LevelCustomPiece.k_maxExtent; x <= LevelCustomPiece.k_maxExtent; x++)
            {
                for (int y = -LevelCustomPiece.k_maxExtent; y <= LevelCustomPiece.k_maxExtent; y++)
                {
                    Vector2Int offset = new Vector2Int(x, y);
                    bool inside = true;
                    for (int index = 0; inside && index < inner.Length; index++)
                    {
                        inside = Array.IndexOf(outer, inner[index] + offset) >= 0;
                    }

                    if (inside)
                    {
                        return offset;
                    }
                }
            }

            throw new InvalidOperationException("The inner footprint does not fit the outer one.");
        }
    }
}
