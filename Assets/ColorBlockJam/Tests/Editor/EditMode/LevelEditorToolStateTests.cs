using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor tool state (colour cycling, arming, ghost rotation) and the shape catalog's
    /// tolerance of an unreadable piece library, without opening the window.
    /// </summary>
    public sealed class LevelEditorToolStateTests
    {
        [Test]
        public void ColourCyclesThroughTheElevenGateColoursAndWraps()
        {
            LevelEditorToolState state = new LevelEditorToolState();
            Assert.That(state.Color, Is.EqualTo(PaletteColor.Red));
            Assert.That(LevelEditorToolState.k_colorCount, Is.EqualTo(11));

            state.CycleColor(-1);
            Assert.That(state.Color, Is.EqualTo(PaletteColor.Pink), "[ from the first colour wraps to the last.");
            state.CycleColor(1);
            Assert.That(state.Color, Is.EqualTo(PaletteColor.Red), "] from the last colour wraps to the first.");
            state.SetColor(PaletteColor.LightBlue);
            Assert.That(state.ColorId, Is.EqualTo("LightBlue"), "The colour id is the name the level JSON uses.");
        }

        [Test]
        public void ArmingSelectsSelectAndRotatesTheGhostUntilAnotherToolDisarms()
        {
            LevelEditorToolState state = new LevelEditorToolState();
            Assert.That(state.RotateArmed(true), Is.False, "Nothing is armed at first.");

            state.Arm(LevelTestShapes.Template("Double", new[] { Vector2Int.zero, Vector2Int.right }));

            // User order of 2026-09-26: pieces are placed with Select.
            Assert.That(state.Tool, Is.EqualTo(LevelEditorTool.Select));
            Assert.That(state.ArmedKey, Is.EqualTo(LevelTestShapes.Key(new[] { Vector2Int.zero, Vector2Int.right })));
            Assert.That(state.HasArmedShape, Is.True);
            Assert.That(state.RotateArmed(true), Is.True);
            Assert.That(state.ArmedCells, Is.EquivalentTo(new[] { Vector2Int.zero, Vector2Int.up }));
            Assert.That(state.FlipArmed(true), Is.True);
            Assert.That(state.ArmedCells, Is.EquivalentTo(new[] { Vector2Int.zero, Vector2Int.up }));

            state.SelectEraser();
            Assert.That(state.Tool, Is.EqualTo(LevelEditorTool.Eraser));
            Assert.That(state.ArmedKey.IsValid, Is.False);
            Assert.That(state.ArmedCells, Is.Empty);

            state.Arm(LevelTestShapes.Template("Single", new[] { Vector2Int.zero }));
            state.Select();
            Assert.That(state.Tool, Is.EqualTo(LevelEditorTool.Select));
            Assert.That(state.ArmedCells, Is.Empty);
        }

        [Test]
        public void ThePenNeverHoldsAPieceAndATransformToolArmsNothing()
        {
            LevelEditorToolState state = new LevelEditorToolState();
            state.SelectPen();
            Assert.That(state.Tool, Is.EqualTo(LevelEditorTool.Pen), "The Pen is active with nothing armed.");
            Assert.That(state.HasArmedShape, Is.False);
            Assert.That(state.FlipArmed(false), Is.False, "An empty Pen has no ghost to mirror.");

            LevelShapeChoice corner = LevelTestShapes.Template("Corner",
                new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up });
            state.Arm(corner);
            state.SelectTransform(LevelEditorTool.RotateClockwise);
            Assert.That(state.Tool, Is.EqualTo(LevelEditorTool.RotateClockwise));
            Assert.That(state.HasArmedShape, Is.False, "Picking up a transform tool disarms the piece.");
            Assert.That(state.RotateArmed(true), Is.False, "With nothing armed, R has no ghost to turn.");

            state.Arm(corner);
            state.SelectPen();
            Assert.That(state.Tool, Is.EqualTo(LevelEditorTool.Pen));
            Assert.That(state.HasArmedShape, Is.False, "The Pen disarms the piece (user order of 2026-09-26).");

            state.Arm(corner);
            Assert.That(state.Tool, Is.EqualTo(LevelEditorTool.Select));
            Assert.That(state.ArmedCells, Is.EqualTo(corner.Cells), "The piece starts in its own orientation again.");

            for (int turn = 0; turn < 4; turn++)
            {
                state.RotateArmed(true);
            }

            Assert.That(state.ArmedCells, Is.EquivalentTo(corner.Cells), "Four clockwise turns restore the ghost.");
            state.RotateArmed(true);
            state.RotateArmed(false);
            Assert.That(state.ArmedCells, Is.EquivalentTo(corner.Cells), "Counter-clockwise undoes clockwise.");
        }

        [Test]
        public void SelectTransformPicksUpOnlyTheFourRotateAndMirrorTools()
        {
            LevelEditorToolState state = new LevelEditorToolState();
            LevelEditorTool[] transforms =
            {
                LevelEditorTool.RotateCounterClockwise, LevelEditorTool.RotateClockwise,
                LevelEditorTool.MirrorHorizontal, LevelEditorTool.MirrorVertical
            };
            foreach (LevelEditorTool tool in transforms)
            {
                state.SelectTransform(tool);
                Assert.That(state.Tool, Is.EqualTo(tool));
            }

            state.SelectTransform(LevelEditorTool.Pen);
            Assert.That(state.Tool, Is.EqualTo(LevelEditorTool.Select), "Any other tool falls back to Select.");
        }

        [Test]
        public void AnArmedDominoGhostTurnsAQuarterTurnAroundThePointerCellEachTime()
        {
            LevelEditorToolState state = new LevelEditorToolState();
            state.Arm(LevelTestShapes.Template("Double", new[] { Vector2Int.zero, Vector2Int.right }));
            Assert.That(state.ArmedAnchor, Is.EqualTo(Vector2Int.zero));
            Assert.That(state.GhostCells, Is.EqualTo(state.ArmedCells), "An armed piece starts on the hovered cell.");

            // D-001: each clockwise turn is a visible quarter turn, not a flip between two orientations.
            Vector2Int[][] ghosts =
            {
                new[] { Vector2Int.zero, Vector2Int.down },
                new[] { Vector2Int.zero, Vector2Int.left },
                new[] { Vector2Int.zero, Vector2Int.up },
                new[] { Vector2Int.zero, Vector2Int.right }
            };
            foreach (Vector2Int[] ghost in ghosts)
            {
                Assert.That(state.RotateArmed(true), Is.True);
                Assert.That(state.GhostCells, Is.EquivalentTo(ghost), "The ghost turns around the hovered cell.");
                Assert.That(state.ArmedCells, Does.Contain(Vector2Int.zero), "Armed cells stay normalized.");
            }

            Assert.That(state.ArmedAnchor, Is.EqualTo(Vector2Int.zero), "Four turns restore the anchor.");
            state.RotateArmed(false);
            Assert.That(state.GhostCells, Is.EquivalentTo(ghosts[2]), "Counter-clockwise undoes clockwise.");
            state.FlipArmed(true);
            Assert.That(state.GhostCells, Is.EquivalentTo(ghosts[2]), "A mirror keeps the ghost in its box.");

            state.Select();
            Assert.That(state.ArmedAnchor, Is.EqualTo(Vector2Int.zero));
            Assert.That(state.GhostCells, Is.Empty);
        }

        [Test]
        public void AnArmedBarIsHeldByItsMiddleCellAndTurnsUnderThePointer()
        {
            LevelEditorToolState state = new LevelEditorToolState();
            Vector2Int[] bar = { Vector2Int.zero, Vector2Int.up, new Vector2Int(0, 2) };
            state.Arm(LevelTestShapes.Template("Bar", bar));
            Assert.That(state.ArmedAnchor, Is.EqualTo(Vector2Int.up), "The pointer holds the middle cell.");
            Assert.That(state.GhostCells, Is.EquivalentTo(new[] { Vector2Int.down, Vector2Int.zero, Vector2Int.up }));

            Assert.That(state.RotateArmed(true), Is.True);
            Assert.That(state.GhostCells, Is.EquivalentTo(new[] { Vector2Int.left, Vector2Int.zero, Vector2Int.right }),
                "The bar turns around the hovered cell and still covers it.");
            Assert.That(state.ArmedCells, Is.EqualTo(LevelPieceTools.RotateClockwise(bar)), "Armed cells stay normalized.");
            Assert.That(state.ArmedAnchor, Is.EqualTo(Vector2Int.right));

            state.RotateArmed(false);
            Assert.That(state.GhostCells, Is.EquivalentTo(new[] { Vector2Int.down, Vector2Int.zero, Vector2Int.up }),
                "Counter-clockwise turns it back under the same cell.");
        }

        [Test]
        public void UnreadableLibraryGivesAnEmptyCatalogWithItsMessage()
        {
            // A library without its JSON source throws from Shapes; the catalog turns that into a message.
            BlockShapeLibrary library = ScriptableObject.CreateInstance<BlockShapeLibrary>();
            try
            {
                LevelShapeCatalog catalog = LevelShapeCatalog.Read(library);

                Assert.That(catalog.Count, Is.Zero);
                Assert.That(catalog.Error, Does.StartWith("The piece library could not be read:"));
                Assert.That(LevelShapeCatalog.Read(null).Error, Is.EqualTo("The piece library asset is missing."));

                LevelShapePalette palette = new LevelShapePalette();
                palette.SetCatalog(catalog);
                Assert.That(palette.TileCount, Is.Zero);
                Assert.That(palette.Q<Label>(LevelShapePalette.k_messageName).text,
                    Is.EqualTo(catalog.Error));
            }
            finally
            {
                Object.DestroyImmediate(library);
            }
        }
    }
}
