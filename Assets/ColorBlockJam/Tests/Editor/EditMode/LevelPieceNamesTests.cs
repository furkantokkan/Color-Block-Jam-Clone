using System;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies LevelPieceNames (DEC-193): Resolve writes one name per non-template key a level's blocks use, taking
    /// the level's own name, else another level's, else the session's, else a new "&lt;Level Name&gt; Piece n" that no
    /// taken name repeats; and CarryName moves a piece's name to its new key once no block uses the old one.
    /// </summary>
    public sealed class LevelPieceNamesTests
    {
        private static readonly Vector2Int[] s_square = { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.one };
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_domino = { Vector2Int.zero, Vector2Int.right };
        private static readonly Vector2Int[] s_line = { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) };
        private static readonly Vector2Int[] s_tee =
        {
            Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0), new Vector2Int(1, 1)
        };

        private static ShapeKey SingleKey => LevelTestShapes.Key(s_single);
        private static ShapeKey DominoKey => LevelTestShapes.Key(s_domino);
        private static ShapeKey LineKey => LevelTestShapes.Key(s_line);
        private static ShapeKey TeeKey => LevelTestShapes.Key(s_tee);

        // The piece library in these tests holds only the square, so every other footprint is a level piece.
        private static LevelShapeCatalog Library() =>
            LevelShapeCatalog.Create(new[] { LevelTestShapes.Template("Square", s_square) });

        [Test]
        public void ResolveNamesEachLevelPieceKeyOnceInBlockOrderAndSkipsTemplates()
        {
            // Block order: the square template, a line with its own name, a tee another level names, a domino only
            // the session names, a second line, and a single nobody names. The table's stale entry is dropped.
            LevelData level = WithNames(Level("Forest",
                    Block("b1", s_square), Block("b2", s_line), Block("b3", s_tee), Block("b4", s_domino),
                    Block("b5", s_line, new Vector2Int(0, 4)), Block("b6", s_single, new Vector2Int(5, 5))),
                new LevelPieceName(LineKey, "Bridge"), new LevelPieceName(LevelTestShapes.Key(s_square), "Stale square"));
            LevelData other = WithNames(Level("Other", Block("b1", s_tee)), new LevelPieceName(TeeKey, "Hat"));

            LevelPieceName[] names = LevelPieceNames.Resolve(level, new[] { other }, Library(),
                key => key == DominoKey ? "Session domino" : null, null);

            Assert.That(Describe(names), Is.EqualTo(new[]
            {
                LineKey + "=Bridge", TeeKey + "=Hat", DominoKey + "=Session domino", SingleKey + "=Forest Piece 1"
            }));
        }

        [Test]
        public void ANewDefaultNameRepeatsNoTemplateTableOrReservedNameIgnoringCase()
        {
            LevelShapeCatalog library = LevelShapeCatalog.Create(new[]
            {
                LevelTestShapes.Template("Square", s_square), LevelTestShapes.Template("forest piece 1", s_tee)
            });
            LevelData level = Level("Forest", Block("b1", s_single), Block("b2", s_domino, new Vector2Int(3, 3)));
            LevelData other = WithNames(Level("Other", Block("b1", s_line)), new LevelPieceName(LineKey, "FOREST PIECE 2"));

            LevelPieceName[] names = LevelPieceNames.Resolve(level, new[] { other }, library, null,
                new[] { "Forest Piece 3", " " });

            Assert.That(Describe(names), Is.EqualTo(new[]
            {
                SingleKey + "=Forest Piece 4", DominoKey + "=Forest Piece 5"
            }), "Library, other-level and reserved names are taken; each new name is taken for the next.");
        }

        [Test]
        public void AnUnusableNameFallsThroughToTheNextSource()
        {
            LevelData level = Level("Forest", Block("b1", s_single));
            string tooLong = new string('x', LevelPieceName.MaxNameLength + 1);

            LevelPieceName[] names = LevelPieceNames.Resolve(level, Array.Empty<LevelData>(), Library(),
                key => tooLong, null);

            Assert.That(Describe(names), Is.EqualTo(new[] { SingleKey + "=Forest Piece 1" }));
        }

        [Test]
        public void AnUnreadableLibraryKeepsTheLevelsOwnTableAndANullLevelHasNone()
        {
            LevelData level = WithNames(Level("Forest", Block("b1", s_single)),
                new LevelPieceName(DominoKey, "Kept though unused"));

            Assert.That(Describe(LevelPieceNames.Resolve(level, null, LevelShapeCatalog.Missing, null, null)),
                Is.EqualTo(new[] { DominoKey + "=Kept though unused" }), "No key can be told from a template.");
            Assert.That(Describe(LevelPieceNames.Resolve(level, null, null, null, null)),
                Is.EqualTo(new[] { DominoKey + "=Kept though unused" }));
            Assert.That(LevelPieceNames.Resolve(null, null, Library(), null, null), Is.Empty);
        }

        [Test]
        public void CarryNameMovesTheNameToTheNewKeyOnceNoBlockUsesTheOldOne()
        {
            // The piece's only block grew from a domino to a line.
            LevelData moved = WithNames(Level("Forest", Block("b1", s_line)), new LevelPieceName(DominoKey, "Bridge"));

            LevelData carried = LevelPieceNames.CarryName(moved, DominoKey, LineKey);

            Assert.That(Describe(LevelRecordCopy.GetPieceNames(carried)), Is.EqualTo(new[] { LineKey + "=Bridge" }));
            Assert.That(Describe(LevelRecordCopy.GetPieceNames(moved)), Is.EqualTo(new[] { DominoKey + "=Bridge" }),
                "The given level is not changed.");
        }

        [Test]
        public void CarryNameKeepsTheTableWhileABlockStillUsesTheOldKey()
        {
            LevelData level = WithNames(Level("Forest", Block("b1", s_line), Block("b2", s_domino, new Vector2Int(4, 4))),
                new LevelPieceName(DominoKey, "Bridge"));

            Assert.That(LevelPieceNames.CarryName(level, DominoKey, LineKey), Is.SameAs(level));
            Assert.That(LevelPieceNames.CarryName(level, DominoKey, DominoKey), Is.SameAs(level));
            Assert.That(LevelPieceNames.CarryName(level, TeeKey, LineKey), Is.SameAs(level), "An unnamed key moves nothing.");
        }

        [Test]
        public void CarryNameLetsTheNewKeyKeepItsOwnNameAndATemplateTakeNone()
        {
            LevelData named = WithNames(Level("Forest", Block("b1", s_line)),
                new LevelPieceName(DominoKey, "Bridge"), new LevelPieceName(LineKey, "Long bridge"));
            Assert.That(Describe(LevelRecordCopy.GetPieceNames(LevelPieceNames.CarryName(named, DominoKey, LineKey))),
                Is.EqualTo(new[] { LineKey + "=Long bridge" }), "Use the existing name of the new key.");

            // The default key stands for a new footprint that is a template: the old name is dropped.
            LevelData toTemplate = WithNames(Level("Forest", Block("b1", s_square)), new LevelPieceName(DominoKey, "Bridge"));
            Assert.That(LevelRecordCopy.GetPieceNames(LevelPieceNames.CarryName(toTemplate, DominoKey, default)), Is.Empty);
        }

        private static LevelData Level(string name, params LevelBlockData[] blocks)
        {
            return new LevelData(name.ToLowerInvariant(), name, 8, 8, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red") }, blocks);
        }

        private static LevelData WithNames(LevelData level, params LevelPieceName[] names)
        {
            return LevelRecordCopy.WithPieceNames(level, names);
        }

        private static LevelBlockData Block(string id, Vector2Int[] cells, Vector2Int origin = default)
        {
            return new LevelBlockData(id, "Red", origin, cells);
        }

        private static string[] Describe(LevelPieceName[] names)
        {
            string[] text = new string[names.Length];
            for (int index = 0; index < names.Length; index++)
            {
                text[index] = names[index].Key + "=" + names[index].Name;
            }

            return text;
        }
    }
}
