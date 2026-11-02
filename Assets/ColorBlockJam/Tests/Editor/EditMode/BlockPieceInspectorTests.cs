using System;
using System.Collections.Generic;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the BlockPiece Inspector (DEC-193): its summary line names the size, the piece library template or
    /// "level piece" and the key; the root prefab asset shows one line instead of a shape; and a scene root shows the
    /// cells it holds now, including when it has none.
    /// </summary>
    public sealed class BlockPieceInspectorTests
    {
        private static readonly Vector2Int[] s_square = { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.one };
        private static readonly Vector2Int[] s_corner = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };

        [Test]
        public void TheSummaryNamesTheTemplateForItsKey()
        {
            ShapeKey asked = default;
            string summary = BlockPieceShapeSummary.Describe(s_square, key =>
            {
                asked = key;
                return "Square";
            });

            Assert.That(asked, Is.EqualTo(LevelTestShapes.Key(s_square)));
            Assert.That(summary, Is.EqualTo("4 cells, 2 x 2 · template Square · key " + LevelTestShapes.Key(s_square)));
        }

        [Test]
        public void ATurnedTemplateReadsAsThatTemplate()
        {
            // The key ignores turns and mirrors, so the corner turned a quarter asks for the same key.
            Vector2Int[] turned = { Vector2Int.zero, Vector2Int.right, Vector2Int.one };
            ShapeKey cornerKey = LevelTestShapes.Key(s_corner);

            string summary = BlockPieceShapeSummary.Describe(turned, key => key == cornerKey ? "Corner" : null);

            Assert.That(summary, Is.EqualTo("3 cells, 2 x 2 · template Corner · key " + cornerKey));
        }

        [Test]
        public void AFootprintTheLibraryLacksIsALevelPiece()
        {
            Vector2Int[] single = { Vector2Int.zero };
            Assert.That(BlockPieceShapeSummary.Describe(single, key => null),
                Is.EqualTo("1 cell, 1 x 1 · level piece · key " + LevelTestShapes.Key(single)));
            Assert.That(BlockPieceShapeSummary.Describe(single, null),
                Is.EqualTo("1 cell, 1 x 1 · level piece · key " + LevelTestShapes.Key(single)),
                "No library lookup reads as a level piece.");
        }

        [Test]
        public void CellsWithoutAKeyNameTheirIssue()
        {
            Vector2Int[] apart = { Vector2Int.zero, new Vector2Int(2, 0) };
            bool asked = false;

            string summary = BlockPieceShapeSummary.Describe(apart, key =>
            {
                asked = true;
                return "Anything";
            });

            Assert.That(summary, Is.EqualTo("2 cells, 3 x 1 · no key: its cells do not all touch edge to edge"));
            Assert.That(asked, Is.False, "A footprint without a key asks the library nothing.");
        }

        [Test]
        public void TheRootPrefabAssetShowsOneLineAndNoShape()
        {
            BlockPiece root = AssetDatabase.LoadAssetAtPath<GameObject>(BoardKitAssets.PrefabPath).GetComponent<BlockPiece>();
            Assert.That(root, Is.Not.Null, BoardKitAssets.PrefabPath);
            UnityEditor.Editor inspector = UnityEditor.Editor.CreateEditor(root);
            try
            {
                Assert.That(inspector, Is.TypeOf<BlockPieceEditor>());
                VisualElement gui = inspector.CreateInspectorGUI();

                Assert.That(gui.Q<Label>(BlockPieceEditor.ShapeSummaryName).text, Is.EqualTo(BlockPieceEditor.RootAssetText));
                Assert.That(gui.Q(BlockPieceEditor.ShapeGridName), Is.Null, "No prefab carries a shape.");
            }
            finally
            {
                Object.DestroyImmediate(inspector);
            }
        }

        [Test]
        public void ASceneRootShowsTheCellsItHoldsNow()
        {
            var art = new BoardArt(AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath),
                AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(BoardKitAssets.CatalogPath));
            GameObject instance = Object.Instantiate(art.Catalog.BlockRoot.gameObject);
            var tilesParent = new GameObject("Inspector test tiles");
            var tiles = new BoardTilePool(art.Catalog, tilesParent.transform);
            UnityEditor.Editor inspector = null;
            try
            {
                BlockPiece piece = instance.GetComponent<BlockPiece>();
                inspector = UnityEditor.Editor.CreateEditor(piece);
                VisualElement gui = inspector.CreateInspectorGUI();
                Label summary = gui.Q<Label>(BlockPieceEditor.ShapeSummaryName);
                Assert.That(gui.Q(BlockPieceEditor.ShapeGridName), Is.Not.Null);
                Assert.That(summary.text, Is.EqualTo("No cells: this root is not prepared."));

                piece.Prepare(tiles, 4);
                ((BlockPieceEditor)inspector).Refresh();
                Assert.That(summary.text, Is.EqualTo("No cells: this root is waiting in its pool."));

                piece.SetCells(s_square);
                ((BlockPieceEditor)inspector).Refresh();
                Assert.That(summary.text, Does.StartWith("4 cells, 2 x 2 · ").And.EndWith("key " + LevelTestShapes.Key(s_square)));
                Assert.That(summary.text, Does.Contain("template Square"), "The shipped library holds the square.");
                IReadOnlyList<Vector2Int> shown = gui.Q<BlockPieceShapeGrid>(BlockPieceEditor.ShapeGridName).Cells;
                Assert.That(shown, Is.EquivalentTo(s_square));

                piece.Clear();
                piece.Release();
            }
            finally
            {
                if (inspector != null) Object.DestroyImmediate(inspector);
                tiles.Dispose();
                Object.DestroyImmediate(tilesParent);
                Object.DestroyImmediate(instance);
            }
        }
    }
}
