using System.IO;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.Controls;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Board;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Blocks palette thumbnails (only the piece's cells, centred, one shared cell size) and the tile ×
    /// that asks to delete a piece, without opening the window.
    /// </summary>
    public sealed class LevelShapePaletteTests
    {
        [Test]
        public void ThumbnailsCentreEveryLibraryPieceAtOneSharedCellSize()
        {
            BlockShapeLibrary library = ScriptableObject.CreateInstance<BlockShapeLibrary>();
            try
            {
                library.Load(File.ReadAllText(BoardKitAssets.ShapesJsonPath));
                LevelShapeCatalog catalog = LevelShapeCatalog.Read(library);
                Assume.That(catalog.Error, Is.Null, "The base piece library is readable.");
                Assume.That(catalog.Count, Is.GreaterThan(0));

                int largest = 1;
                for (int index = 0; index < catalog.Count; index++)
                {
                    RectInt bounds = LevelShapeThumbnail.GetBounds(catalog.Get(index).Cells);
                    largest = Mathf.Max(largest, Mathf.Max(bounds.width, bounds.height));
                }

                Assert.That(LevelShapeThumbnail.GetExtent(catalog), Is.EqualTo(largest));
                float grid = LevelShapePalette.k_gridSize;
                float cellSize = LevelShapeThumbnail.GetCellSize(grid, largest);
                Assert.That(cellSize, Is.EqualTo(Mathf.Floor(grid / largest)));

                for (int index = 0; index < catalog.Count; index++)
                {
                    LevelShapeChoice shape = catalog.Get(index);
                    RectInt bounds = LevelShapeThumbnail.GetBounds(shape.Cells);
                    Rect union = LevelShapeThumbnail.GetCellRect(bounds, shape.Cells[0], grid, cellSize);
                    for (int cell = 0; cell < shape.Cells.Count; cell++)
                    {
                        Rect rect = LevelShapeThumbnail.GetCellRect(bounds, shape.Cells[cell], grid, cellSize);
                        Assert.That(rect.width, Is.EqualTo(cellSize), shape.DisplayName);
                        Assert.That(rect.xMin >= 0f && rect.yMin >= 0f && rect.xMax <= grid && rect.yMax <= grid,
                            Is.True, $"{shape.DisplayName} stays inside its tile.");
                        union = Rect.MinMaxRect(Mathf.Min(union.xMin, rect.xMin), Mathf.Min(union.yMin, rect.yMin),
                            Mathf.Max(union.xMax, rect.xMax), Mathf.Max(union.yMax, rect.yMax));
                    }

                    Assert.That(Vector2.Distance(union.center, new Vector2(0.5f * grid, 0.5f * grid)),
                        Is.LessThanOrEqualTo(1f), $"{shape.DisplayName} is centred in its tile.");
                }
            }
            finally
            {
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void ThumbnailRowZeroIsAtTheBottomAndAPieceAwayFromTheOriginIsStillCentred()
        {
            // An L drawn at an offset: the cells' position, not (0, 0), decides the layout.
            Vector2Int[] cells = { new Vector2Int(3, 5), new Vector2Int(3, 6), new Vector2Int(4, 5) };
            RectInt bounds = LevelShapeThumbnail.GetBounds(cells);
            Assert.That(bounds, Is.EqualTo(new RectInt(3, 5, 2, 2)));

            Rect bottom = LevelShapeThumbnail.GetCellRect(bounds, cells[0], 40f, 10f);
            Rect top = LevelShapeThumbnail.GetCellRect(bounds, cells[1], 40f, 10f);
            Rect right = LevelShapeThumbnail.GetCellRect(bounds, cells[2], 40f, 10f);
            Assert.That(bottom, Is.EqualTo(new Rect(10f, 20f, 10f, 10f)), "Row 5 is the lower row, drawn below row 6.");
            Assert.That(top, Is.EqualTo(new Rect(10f, 10f, 10f, 10f)));
            Assert.That(right, Is.EqualTo(new Rect(20f, 20f, 10f, 10f)));
        }

        [Test]
        public void EveryTileHasADeleteButtonBesideIt()
        {
            LevelShapePalette palette = CreatePalette();

            Assert.That(palette.TileCount, Is.EqualTo(2));
            for (int index = 0; index < palette.TileCount; index++)
            {
                Button delete = palette.GetDeleteButton(index);
                VisualElement tile = palette.GetTile(index);
                Assert.That(delete.parent, Is.SameAs(tile.parent), "The Delete badge is the tile's sibling, not its child.");
                Assert.That(tile.Contains(delete), Is.False);
                Assert.That(delete.enabledSelf, Is.True);
                UiTestInput.AssertShowsIcon(delete, ActionIcons.k_delete);
            }

            Assert.That(palette.GetDeleteButton(0).tooltip, Is.EqualTo("Delete One Cell from the piece library"));
        }

        [Test]
        public void PlusTileIsTheLastItemAndStaysOutOfTheLibraryTiles()
        {
            LevelShapePalette palette = CreatePalette();
            VisualElement row = palette.Q("shape-tiles");

            Assert.That(palette.TileCount, Is.EqualTo(2), "The library tiles keep their indices.");
            Assert.That(palette.PlusTile, Is.Not.Null);
            Assert.That(palette.PlusTile.name, Is.EqualTo(LevelShapePalette.k_createTileName));
            Assert.That(row.IndexOf(palette.PlusTile.parent), Is.EqualTo(row.childCount - 1), "\"+\" is the last item.");
            Assert.That(palette.PlusTile.ClassListContains(LevelShapePalette.k_tileClass), Is.False);
            Assert.That(palette.PlusTile.parent.Q<Button>(), Is.Null, "\"+\" has no Delete badge.");
            Assert.That(palette.Q<Label>(LevelShapePalette.k_customHeaderName), Is.Null,
                "No Level pieces header without pieces.");

            palette.SetCatalog(LevelShapeCatalog.Missing);
            Assert.That(palette.PlusTile, Is.Null, "An unreadable library shows only its message.");
            Assert.That(palette.CustomTileCount, Is.Zero);
        }

        [Test]
        public void CustomSectionListsLevelPiecesAfterTheLibraryWithDeleteBadges()
        {
            LevelShapePalette palette = CreatePalette();
            LevelShapeChoice first = new LevelShapeChoice("Level 1 Piece 1", new[]
            {
                Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0)
            });
            LevelShapeChoice second = new LevelShapeChoice("Level 1 Piece 2", new[]
            {
                Vector2Int.zero, Vector2Int.right, Vector2Int.up
            });

            palette.SetCustomPieces(new[] { first, second });

            VisualElement row = palette.Q("shape-tiles");
            Label header = palette.Q<Label>(LevelShapePalette.k_customHeaderName);
            Assert.That(palette.TileCount, Is.EqualTo(2), "Level piece tiles are not library tiles.");
            Assert.That(palette.CustomTileCount, Is.EqualTo(2));
            Assert.That(header, Is.Not.Null);
            Assert.That(row.IndexOf(header), Is.GreaterThan(row.IndexOf(palette.GetTile(1).parent)), "After the library.");
            Assert.That(palette.GetCustomChoice(0), Is.SameAs(first));
            Assert.That(palette.GetCustomTile(1).name, Is.EqualTo("custom-" + second.Key), "Tiles are found by key.");
            for (int index = 0; index < palette.CustomTileCount; index++)
            {
                VisualElement tile = palette.GetCustomTile(index);
                // User request of 2026-09-26: level piece tiles have a × too, a sibling of the tile like a library tile's.
                Assert.That(tile.parent.Q<Button>(), Is.SameAs(palette.GetCustomDeleteButton(index)),
                    "Level piece tiles have a ×.");
                Assert.That(palette.GetCustomDeleteButton(index).parent, Is.SameAs(tile.parent));
                Assert.That(tile.enabledSelf, Is.True, "Level piece tiles are stampable.");
                Assert.That(row.IndexOf(tile.parent), Is.GreaterThan(row.IndexOf(header)));
            }

            Assert.That(row.IndexOf(palette.PlusTile.parent), Is.EqualTo(row.childCount - 1), "\"+\" stays last.");
            Assert.That(palette.GetCustomTile(0).Q<Label>().text, Is.EqualTo("Level 1 Piece 1"),
                "The label is the piece's name; the key shows only in the tooltip (DEC-193).");
            Assert.That(palette.GetCustomDeleteButton(1).tooltip,
                Is.EqualTo("Delete Level 1 Piece 2 and its blocks from this level"));
            UiTestInput.AssertShowsIcon(palette.GetCustomDeleteButton(1), ActionIcons.k_delete);

            palette.SetArmed(second.Key);
            Assert.That(palette.GetCustomTile(1).style.borderTopColor.value, Is.EqualTo(LevelShapePalette.s_armedBorder));
            Assert.That(palette.GetCustomTile(0).style.borderTopColor.value, Is.EqualTo(LevelShapePalette.s_idleBorder));

            palette.SetCatalog(LevelShapeCatalog.Create(new[]
            {
                LevelTestShapes.Template("One Cell", new[] { Vector2Int.zero })
            }));
            Assert.That(palette.CustomTileCount, Is.EqualTo(2), "A library change keeps the Level pieces section.");

            palette.SetCustomPieces(new LevelShapeChoice[0]);
            Assert.That(palette.CustomTileCount, Is.Zero);
            Assert.That(palette.Q<Label>(LevelShapePalette.k_customHeaderName), Is.Null);
            Assert.That(palette.PlusTile, Is.Not.Null);
        }

        private static LevelShapePalette CreatePalette()
        {
            LevelShapeChoice single = LevelTestShapes.Template("One Cell", new[] { Vector2Int.zero });
            LevelShapeChoice domino = LevelTestShapes.Template("Two Cells", new[] { Vector2Int.zero, Vector2Int.right });
            LevelShapePalette palette = new LevelShapePalette();
            palette.SetCatalog(LevelShapeCatalog.Create(new[] { single, domino }));
            return palette;
        }
    }
}
