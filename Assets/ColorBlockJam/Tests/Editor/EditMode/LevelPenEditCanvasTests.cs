using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the canvas and Blocks palette side of Pen editing (task level-editor-pen-edit-templates): the free
    /// cells the edited block can grow into, a Pen press on another block picking it while its own cells and free cells
    /// still stroke, the Barrier brush and gate painting unchanged, the click on empty space that ends a Pen edit or
    /// clears the selection while every other click keeps its rule, and the palette's green edit outline, which never
    /// arms a tile and gives way to the armed outline.
    /// </summary>
    public sealed class LevelPenEditCanvasTests
    {
        private const int k_boardSize = 8;
        private static readonly ShapeKey s_singleKey = LevelTestShapes.Key(new[] { Vector2Int.zero });
        private static readonly Vector2Int[] s_levelPieceCells = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };
        private EditorWindow m_host;
        private LevelCanvasElement m_canvas;
        private readonly List<Vector2Int> m_cells = new List<Vector2Int>();

        [SetUp]
        public void OpenHost()
        {
            // A shown utility window lays the canvas out, so it has a board geometry and receives pointer events.
            m_host = ScriptableObject.CreateInstance<EditorWindow>();
            m_host.ShowUtility();
            m_host.position = new Rect(40f, 40f, 600f, 700f);
        }

        [TearDown]
        public void CloseHost()
        {
            m_host.Close();
            m_canvas = null;
        }

        [Test]
        public void LoneInteriorCellGrowsIntoItsFourNeighbours()
        {
            OpenCanvas(Level(Block("b1", new Vector2Int(4, 4), Vector2Int.zero)));
            EditBlock("b1");

            m_canvas.CollectGrowCells(m_cells);

            Assert.That(m_cells, Is.EquivalentTo(new[]
            {
                new Vector2Int(3, 4), new Vector2Int(5, 4), new Vector2Int(4, 3), new Vector2Int(4, 5)
            }));
        }

        [Test]
        public void GrowCellsLeaveOutBoardObjectsClosedBarriersVoidsAndOtherBlocks()
        {
            LevelData level = Level(
                Block("b1", new Vector2Int(4, 4), Vector2Int.zero),
                Block("b2", new Vector2Int(4, 5), Vector2Int.zero));
            level = LevelRecordCopy.WithVoidCells(level, new[] { new Vector2Int(3, 4) });
            level = LevelRecordCopy.WithCellObjects(level, new[]
            {
                new LevelCellObjectData("barrier-1", new[] { new Vector2Int(5, 4) }, LevelBarrierCommands.CreateEffect()),
                new LevelCellObjectData("barrier-2", new[] { new Vector2Int(4, 3) },
                    LevelEffectTests.Effect("barrier", ("startsOpen", "true")))
            });
            OpenCanvas(level);
            EditBlock("b1");

            m_canvas.CollectGrowCells(m_cells);

            Assert.That(m_cells, Is.Empty, "A void, a closed barrier, an open barrier (a board object) and b2.");
        }

        [Test]
        public void GrowCellsStayInsideTheFiveByFiveBoxAndStopAtTwentyFiveCells()
        {
            // A five-cell row may grow up or down but not longer.
            OpenCanvas(Level(Block("b1", new Vector2Int(1, 4), Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0),
                new Vector2Int(3, 0), new Vector2Int(4, 0))));
            EditBlock("b1");

            m_canvas.CollectGrowCells(m_cells);

            Assert.That(m_cells, Has.Count.EqualTo(10));
            Assert.That(m_cells, Has.No.Member(new Vector2Int(0, 4)));
            Assert.That(m_cells, Has.No.Member(new Vector2Int(6, 4)));

            // A full 5 x 5 block has 25 cells: nothing more can join it.
            List<Vector2Int> square = new List<Vector2Int>();
            for (int y = 0; y < LevelCustomPiece.k_maxExtent; y++)
            {
                for (int x = 0; x < LevelCustomPiece.k_maxExtent; x++)
                {
                    square.Add(new Vector2Int(x, y));
                }
            }

            m_canvas.SetModel(LevelCanvasModel.Create(Level(Block("b1", new Vector2Int(1, 1), square.ToArray()))));

            m_canvas.CollectGrowCells(m_cells);

            Assert.That(m_cells, Is.Empty);
        }

        [Test]
        public void GrowCellsNeedTheFreehandPenAGrowingBlockAndNoStroke()
        {
            OpenCanvas(Level(Block("b1", new Vector2Int(4, 4), Vector2Int.zero)));
            EditBlock("b1");

            m_canvas.SetFreehand(false);
            m_canvas.CollectGrowCells(m_cells);
            Assert.That(m_cells, Is.Empty, "The plain Pen without an edited block.");

            m_canvas.SetFreehand(true);
            m_canvas.SetFreehandBlock(null);
            m_canvas.CollectGrowCells(m_cells);
            Assert.That(m_cells, Is.Empty, "No growing block yet.");

            m_canvas.SetFreehandBlock("b1");
            m_canvas.SetObstacleBrush(LevelObstacleBrush.Barrier);
            m_canvas.CollectGrowCells(m_cells);
            Assert.That(m_cells, Is.Empty, "The Barrier brush paints instead.");

            m_canvas.SetObstacleBrush(LevelObstacleBrush.None);
            Press(new Vector2Int(4, 4));
            Assert.That(m_canvas.IsPieceStrokeActive, Is.True, "A press on the block's own cell starts a stroke.");
            m_canvas.CollectGrowCells(m_cells);
            Assert.That(m_cells, Is.Empty, "None during a stroke.");

            m_canvas.CancelDrag();
            m_canvas.CollectGrowCells(m_cells);
            Assert.That(m_cells, Has.Count.EqualTo(4));
        }

        [Test]
        public void PenPressOnAnotherBlockPicksItAndOwnOrFreeCellsStillStroke()
        {
            OpenCanvas(Level(
                Block("b1", new Vector2Int(1, 1), Vector2Int.zero),
                Block("b2", new Vector2Int(5, 5), Vector2Int.zero, Vector2Int.right)));
            EditBlock("b1");
            List<int> picked = new List<int>();
            m_canvas.PenBlockPicked += picked.Add;

            Press(new Vector2Int(6, 5));

            Assert.That(picked, Is.EqualTo(new[] { 1 }), "b2 is picked by its index.");
            Assert.That(m_canvas.IsPieceStrokeActive, Is.False, "Picking starts no stroke.");

            Press(new Vector2Int(1, 1));
            Assert.That(m_canvas.IsPieceStrokeActive, Is.True, "The edited block's own cell starts a stroke.");
            m_canvas.CancelDrag();

            Press(new Vector2Int(2, 1));
            Assert.That(m_canvas.IsPieceStrokeActive, Is.True, "A free cell starts a stroke.");
            m_canvas.CancelDrag();
            Assert.That(picked, Has.Count.EqualTo(1));
        }

        [Test]
        public void PenPressOnABlockPicksItWithoutAnEditedBlockAndAFreeCellClearsTheSelection()
        {
            OpenCanvas(Level(Block("b1", new Vector2Int(1, 1), Vector2Int.zero)));
            m_canvas.SetTool(LevelEditorTool.Pen, null);
            List<int> picked = new List<int>();
            List<Vector2Int> placed = new List<Vector2Int>();
            int emptyClicks = 0;
            m_canvas.PenBlockPicked += picked.Add;
            m_canvas.PlaceRequested += placed.Add;
            m_canvas.EmptySpaceClicked += () => emptyClicks++;

            Press(new Vector2Int(1, 1));
            Press(new Vector2Int(3, 3));

            Assert.That(picked, Is.EqualTo(new[] { 0 }), "The plain Pen picks the block.");
            Assert.That(emptyClicks, Is.EqualTo(1), "A free cell is empty space (user order of 2026-09-26).");
            Assert.That(placed, Is.Empty, "So it no longer asks the window to place.");

            // "+" armed before its first cell: a block is picked too, and a free cell starts the new piece.
            m_canvas.SetFreehand(true);
            Press(new Vector2Int(1, 1));
            Assert.That(picked, Is.EqualTo(new[] { 0, 0 }));
            Press(new Vector2Int(3, 3));
            Assert.That(m_canvas.IsPieceStrokeActive, Is.True);
            m_canvas.CancelDrag();
        }

        [Test]
        public void EmptySpaceEndsAPenEditOrClearsTheSelectionAndEveryOtherClickKeepsItsRule()
        {
            LevelData level = Level(
                Block("b1", new Vector2Int(1, 1), Vector2Int.zero),
                Block("b2", new Vector2Int(5, 5), Vector2Int.zero));
            OpenCanvas(LevelRecordCopy.WithVoidCells(level, new[] { new Vector2Int(7, 7) }));
            int emptyClicks = 0;
            List<LevelCanvasSelection> selected = new List<LevelCanvasSelection>();
            List<Vector2Int> placed = new List<Vector2Int>();
            List<bool> obstacleStrokes = new List<bool>();
            m_canvas.EmptySpaceClicked += () => emptyClicks++;
            m_canvas.SelectionRequested += selected.Add;
            m_canvas.PlaceRequested += placed.Add;
            m_canvas.ObstacleStrokeRequested += (_, paint) => obstacleStrokes.Add(paint);
            Vector2 outside = GetOutsidePoint();

            // Select: a free cell and the area outside the board clear, with either button; a block is still selected.
            m_canvas.SetTool(LevelEditorTool.Select, null);
            Press(new Vector2Int(3, 3));
            SendPointerDown(outside);
            Press(new Vector2Int(3, 3), 1);
            Assert.That(emptyClicks, Is.EqualTo(3));
            emptyClicks = 0;
            Press(new Vector2Int(1, 1));
            m_canvas.CancelDrag();
            Assert.That(selected, Has.Count.EqualTo(1), "A block is selected as before.");
            Press(new Vector2Int(7, 7), 1);
            Assert.That(obstacleStrokes, Is.EqualTo(new[] { false }), "A right click still erases a void.");
            Assert.That(emptyClicks, Is.Zero, "A block and a void are no empty space.");

            // A Pen edit: a green cell still grows the block; any other free cell or the outside ends the edit, and a
            // right click takes no cell, so a green cell is empty space for it.
            EditBlock("b1");
            m_canvas.SetPenEditing(true);
            Press(new Vector2Int(2, 1));
            Assert.That(m_canvas.IsPieceStrokeActive, Is.True, "A green cell adds itself.");
            m_canvas.CancelDrag();
            Assert.That(emptyClicks, Is.Zero, "A green cell is not empty space.");
            Press(new Vector2Int(3, 3));
            Assert.That(m_canvas.IsPieceStrokeActive, Is.False, "A cell that is not green starts no stroke.");
            SendPointerDown(outside);
            Press(new Vector2Int(2, 1), 1);
            Assert.That(emptyClicks, Is.EqualTo(3),
                "A non-green free cell, the outside, and a right click on the green cell each count.");
            emptyClicks = 0;

            // A "+" New piece drawing keeps its rule: a free cell starts the piece, and other empty space does nothing.
            m_canvas.SetPenEditing(false);
            m_canvas.SetFreehandBlock(null);
            SendPointerDown(outside);
            Press(new Vector2Int(3, 3), 1);
            Press(new Vector2Int(3, 3));
            Assert.That(m_canvas.IsPieceStrokeActive, Is.True, "\"+\" starts its piece on a free cell.");
            m_canvas.CancelDrag();

            // So does the Barrier brush, whose right press on a board cell erases.
            m_canvas.SetFreehand(false);
            m_canvas.SetObstacleBrush(LevelObstacleBrush.Barrier);
            SendPointerDown(outside);
            Press(new Vector2Int(3, 3), 1);
            Assert.That(m_canvas.IsObstacleStrokeActive, Is.True);
            m_canvas.CancelDrag();
            m_canvas.SetObstacleBrush(LevelObstacleBrush.None);
            Assert.That(emptyClicks, Is.Zero, "Neither \"+\" nor the Barrier brush ends anything on empty space.");

            // Select with an armed piece still places it on a free cell; the outside still clears.
            m_canvas.SetTool(LevelEditorTool.Select, new[] { Vector2Int.zero });
            Press(new Vector2Int(3, 3));
            Assert.That(placed, Is.EqualTo(new[] { new Vector2Int(3, 3) }));
            SendPointerDown(outside);
            Assert.That(emptyClicks, Is.EqualTo(1));
        }

        [Test]
        public void APressOnAnEdgeOrCornerButtonIsNoEmptySpace()
        {
            OpenCanvas(Level(Block("b1", new Vector2Int(1, 1), Vector2Int.zero)));
            m_canvas.SetTool(LevelEditorTool.Select, null);
            int emptyClicks = 0;
            m_canvas.EmptySpaceClicked += () => emptyClicks++;

            // The buttons take their places from the board geometry, which a first layout pass sets.
            ValidateLayout();
            Button[] buttons =
            {
                m_canvas.GetEdgeButton(BoardSide.Top, true), m_canvas.GetEdgeButton(BoardSide.Left, false),
                m_canvas.Q<Button>(LevelCanvasElement.GetCornerButtonName(0, true))
            };
            foreach (Button button in buttons)
            {
                Assume.That(button, Is.Not.Null);
                Vector2 centre = button.worldBound.center;
                Assume.That(button.panel.Pick(centre), Is.SameAs(button), $"{button.name} takes the press.");
                SendPointerDown(centre);
                SendPointerUp(centre);
            }

            Assert.That(emptyClicks, Is.Zero);
        }

        [Test]
        public void BarrierBrushAndGatePaintingNeverPickABlock()
        {
            OpenCanvas(Level(Block("b1", new Vector2Int(1, 1), Vector2Int.zero)));
            EditBlock(null);
            List<int> picked = new List<int>();
            m_canvas.PenBlockPicked += picked.Add;

            m_canvas.SetObstacleBrush(LevelObstacleBrush.Barrier);
            Press(new Vector2Int(1, 1));
            Assert.That(m_canvas.IsObstacleStrokeActive, Is.True, "The brush paints from a block cell.");
            m_canvas.CancelDrag();
            m_canvas.SetObstacleBrush(LevelObstacleBrush.None);

            Vector2 ring = m_canvas.Geometry.GetEdgeRect(BoardSide.Top, 3, LevelCanvasGeometry.k_ringDepth).center;
            Assert.That(m_canvas.HitTest(ring).Kind, Is.EqualTo(LevelCanvasHitKind.RingSegment));
            SendPointerDown(m_canvas.LocalToWorld(ring));
            Assert.That(m_canvas.IsPaintingGate, Is.True, "The ring still paints a gate.");
            m_canvas.CancelDrag();

            Assert.That(picked, Is.Empty);
        }

        [Test]
        public void EditingOutlinesTheTileInGreenWithoutArmingIt()
        {
            LevelShapePalette palette = CreatePalette();
            List<LevelShapeChoice> picked = new List<LevelShapeChoice>();
            palette.ShapePicked += picked.Add;

            palette.SetEditing(s_singleKey);

            Assert.That(GetBorder(palette.GetTile(0)), Is.EqualTo(LevelShapePalette.s_editBorder));
            Assert.That(palette.GetTile(0).style.borderTopWidth.value, Is.EqualTo(LevelShapePalette.k_editBorderWidth));
            Assert.That(GetBorder(palette.GetTile(1)), Is.EqualTo(LevelShapePalette.s_idleBorder));
            Assert.That(picked, Is.Empty, "Editing never arms or picks the tile.");

            palette.SetEditing(LevelTestShapes.Key(s_levelPieceCells));
            Assert.That(GetBorder(palette.GetCustomTile(0)), Is.EqualTo(LevelShapePalette.s_editBorder),
                "A level piece tile is outlined the same way.");
            Assert.That(GetBorder(palette.GetTile(0)), Is.EqualTo(LevelShapePalette.s_idleBorder));

            palette.SetEditing(default);
            Assert.That(GetBorder(palette.GetCustomTile(0)), Is.EqualTo(LevelShapePalette.s_idleBorder));
            Assert.That(palette.GetCustomTile(0).style.borderTopWidth.value, Is.EqualTo(1f));
        }

        [Test]
        public void ArmedOutlineWinsOverEditingAndEditingOverHover()
        {
            LevelShapePalette palette = CreatePalette();
            VisualElement single = palette.GetTile(0);
            palette.SetEditing(s_singleKey);

            Send<PointerEnterEvent>(single);
            Assert.That(GetBorder(single), Is.EqualTo(LevelShapePalette.s_editBorder), "Editing wins over hover.");

            palette.SetArmed(s_singleKey);
            Assert.That(GetBorder(single), Is.EqualTo(LevelShapePalette.s_armedBorder), "Armed wins over editing.");
            Assert.That(single.style.borderTopWidth.value, Is.EqualTo(1f));

            palette.SetArmed(default);
            Assert.That(GetBorder(single), Is.EqualTo(LevelShapePalette.s_editBorder));

            palette.SetEditing(default);
            Assert.That(GetBorder(single), Is.EqualTo(LevelShapePalette.s_hoverBorder));
            Send<PointerLeaveEvent>(single);
            Assert.That(GetBorder(single), Is.EqualTo(LevelShapePalette.s_idleBorder));
        }

        private void OpenCanvas(LevelData level)
        {
            m_canvas = new LevelCanvasElement();
            m_canvas.style.width = 560f;
            m_canvas.style.height = 660f;
            m_host.rootVisualElement.Add(m_canvas);
            m_canvas.SetModel(LevelCanvasModel.Create(level));
            ValidateLayout();
            Assume.That(m_canvas.Geometry.IsValid, "The canvas has a laid-out board.");
        }

        private void ValidateLayout()
        {
            UiTestInput.ValidateLayout(m_host.rootVisualElement);
        }

        // The freehand growth a Pen edit and a "+" piece share: the Pen in freehand mode growing this block (null before
        // its first cell). A Pen edit also sets SetPenEditing, which only the empty-space click reads.
        private void EditBlock(string blockId)
        {
            m_canvas.SetTool(LevelEditorTool.Pen, null);
            m_canvas.SetFreehand(true);
            m_canvas.SetFreehandBlock(blockId);
        }

        // A world point outside the board, its gate ring and its edge and corner buttons: near the canvas's top-left.
        private Vector2 GetOutsidePoint()
        {
            Vector2 local = new Vector2(4f, 4f);
            Rect board = m_canvas.Geometry.BoardRect;
            float ring = LevelCanvasGeometry.k_ringDepth * m_canvas.Geometry.CellSize;
            Assume.That(m_canvas.HitTest(local).Kind, Is.EqualTo(LevelCanvasHitKind.Outside));
            Assume.That(local.x < board.xMin - ring && local.y < board.yMin - ring, Is.True, "Clear of the ring corners.");
            Vector2 world = m_canvas.LocalToWorld(local);
            Assume.That(m_canvas.panel.Pick(world), Is.SameAs(m_canvas), "No button there.");
            return world;
        }

        private static LevelData Level(params LevelBlockData[] blocks)
        {
            return new LevelData("pen-edit", "Pen edit", k_boardSize, k_boardSize, 60, new LevelDoorData[0], blocks);
        }

        private static LevelBlockData Block(string id, Vector2Int origin, params Vector2Int[] cells)
        {
            return new LevelBlockData(id, "Red", origin, cells);
        }

        // Button 0 is a left press and 1 a right press.
        private void Press(Vector2Int cell, int button = 0)
        {
            SendPointerDown(m_canvas.LocalToWorld(m_canvas.Geometry.GetCellRect(cell).center), button);
        }

        private void SendPointerDown(Vector2 position, int button = 0)
        {
            UiTestInput.SendPointer(m_host.rootVisualElement, EventType.MouseDown, position, button);
        }

        private void SendPointerUp(Vector2 position)
        {
            UiTestInput.SendPointer(m_host.rootVisualElement, EventType.MouseUp, position);
        }

        private LevelShapePalette CreatePalette()
        {
            LevelShapeChoice single = LevelTestShapes.Template("One Cell", new[] { Vector2Int.zero });
            LevelShapeChoice pair = LevelTestShapes.Template("Two Cells", new[] { Vector2Int.zero, Vector2Int.right });
            LevelShapePalette palette = new LevelShapePalette();
            palette.SetCatalog(LevelShapeCatalog.Create(new[] { single, pair }));
            // A level piece: a key no template holds (DEC-193).
            palette.SetCustomPieces(new[] { new LevelShapeChoice("Pen Edit Piece 1", s_levelPieceCells) });
            m_host.rootVisualElement.Add(palette);
            return palette;
        }

        private static Color GetBorder(VisualElement element)
        {
            return element.style.borderTopColor.value;
        }

        private static void Send<T>(VisualElement target) where T : EventBase<T>, new()
        {
            using (T pointerEvent = EventBase<T>.GetPooled())
            {
                pointerEvent.target = target;
                target.SendEvent(pointerEvent);
            }
        }
    }
}
