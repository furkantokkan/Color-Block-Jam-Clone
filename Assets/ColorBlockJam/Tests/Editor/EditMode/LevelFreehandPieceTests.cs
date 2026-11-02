using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies freehand piece drawing (AC-1): the "+" New piece tile makes a named piece and arms the Pen in freehand
    /// mode, a click or a drag over free cells walks cell by cell and draws that piece's block as one undo step, later
    /// clicks and drags next to it grow it and a right click takes a cell off (click-to-grow, user feedback of
    /// 2026-09-25), its Level pieces tile follows every change, refused strokes change nothing, Esc cancels, and the Pen
    /// without an armed piece still only asks for one. Nothing is saved.
    /// </summary>
    public sealed class LevelFreehandPieceTests
    {
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_domino = { Vector2Int.zero, Vector2Int.right };
        private static readonly Vector2Int[] s_square = { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.one };

        private LevelEditorWindowFixture m_fixture;
        private LevelDatabaseWindow m_window;
        private BlockShapeLibrary m_library;
        // Pieces the window mirrored into the Piece Editor; the stub keeps the real window closed.
        private readonly List<LevelPieceRequest> m_mirrored = new List<LevelPieceRequest>();
        private bool m_pieceEditorOpen;

        [SetUp]
        public void ClearRequests()
        {
            m_mirrored.Clear();
            m_pieceEditorOpen = false;
        }

        [TearDown]
        public void CloseWindow()
        {
            m_fixture?.Dispose();
            m_fixture = null;
            m_window = null;

            if (m_library != null)
            {
                Object.DestroyImmediate(m_library);
                m_library = null;
            }
        }

        [Test]
        public void TheWalkCrossesEveryCellOneSideAtATime()
        {
            List<Vector2Int> crossed = new List<Vector2Int>();
            LevelCanvasWalk.AddCrossedCells(new Vector2Int(0, 0), new Vector2Int(3, -2), crossed);

            Assert.That(crossed, Has.Count.EqualTo(5));
            Assert.That(crossed[crossed.Count - 1], Is.EqualTo(new Vector2Int(3, -2)));
            Vector2Int previous = Vector2Int.zero;
            foreach (Vector2Int cell in crossed)
            {
                Assert.That(Mathf.Abs(cell.x - previous.x) + Mathf.Abs(cell.y - previous.y), Is.EqualTo(1), "Side steps only.");
                previous = cell;
            }

            crossed.Clear();
            LevelCanvasWalk.AddCrossedCells(Vector2Int.one, Vector2Int.one, crossed);
            Assert.That(crossed, Is.Empty);
        }

        [Test]
        public void FreehandIsItsOwnPenModeThatAnyOtherPickClears()
        {
            LevelEditorToolState state = new LevelEditorToolState();
            state.ArmBarrier();

            state.ArmFreehand();
            Assert.That(state.Tool, Is.EqualTo(LevelEditorTool.Pen));
            Assert.That(state.IsFreehandArmed, Is.True);
            Assert.That(state.IsBarrierArmed, Is.False, "It clears the Barrier brush.");
            Assert.That(state.HasArmedShape, Is.False);

            state.Arm(LevelTestShapes.Template("Single", s_single));
            Assert.That(state.IsFreehandArmed, Is.False, "A piece clears it.");
            state.ArmFreehand();
            state.ArmBarrier();
            Assert.That(state.IsFreehandArmed, Is.False, "A brush clears it.");
            state.ArmFreehand();
            state.SelectEraser();
            Assert.That(state.IsFreehandArmed, Is.False, "Another tool clears it.");
        }

        [Test]
        public void ThePlusTileIsTheOnlyNewPieceTileAndOutlinesWhileArmed()
        {
            LevelShapePalette palette = new LevelShapePalette();
            palette.SetCatalog(LevelShapeCatalog.Create(new[] { LevelTestShapes.Template("Single", s_single) }));
            VisualElement row = palette.Q("shape-tiles");

            Assert.That(palette.PlusTile, Is.Not.Null);
            Assert.That(palette.PlusTile.Query<Label>().ToList().ConvertAll(label => label.text),
                Is.EqualTo(new[] { "+", "New piece" }), "The plus glyph and its label, with no pencil icon.");
            Assert.That(palette.PlusTile.Q<Image>(), Is.Null);
            Assert.That(palette.PlusTile.tooltip, Does.Contain("freehand").And.Contain("Piece Editor"));
            Assert.That(row.childCount, Is.EqualTo(2), "One library slot and the \"+\" slot, nothing else.");
            Assert.That(palette.TileCount, Is.EqualTo(1), "It is not a library tile.");

            palette.SetFreehandArmed(true);
            Assert.That(palette.PlusTile.style.borderTopColor.value, Is.EqualTo(LevelShapePalette.s_armedBorder));
            palette.SetFreehandArmed(false);
            Assert.That(palette.PlusTile.style.borderTopColor.value, Is.EqualTo(LevelShapePalette.s_idleBorder));
        }

        [UnityTest]
        public IEnumerator AStrokeOverFreeCellsBecomesOneBlockAsOneUndoStepAndTheTileStaysArmed()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = RequireCanvas(root);
            UseLibrary(Shape("Single", s_single), Shape("DoubleHorizontal", s_domino));
            LoadFixture(root, CreateFixture(new LevelBlockData("b1", "Red", new Vector2Int(5, 5), s_single)));
            LevelShapePalette palette = root.Q<LevelShapePalette>();
            LevelEditorToolState state = GetToolState();
            LevelData before = GetDraft();

            GetToolState().ArmBarrier();
            yield return ScrollTo(root, palette.PlusTile);
            Click(root, palette.PlusTile.worldBound.center);
            Assert.That(state.IsFreehandArmed, Is.True, "The \"+\" tile arms freehand drawing.");
            Assert.That(state.IsBarrierArmed, Is.False, "It clears the Barrier brush.");
            Assert.That(canvas.IsFreehand, Is.True);
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Pen));
            Assert.That(palette.PlusTile.style.borderTopColor.value, Is.EqualTo(LevelShapePalette.s_armedBorder));
            string scope = (string)LevelEditorTestAccess.Invoke(m_window, "GetOpenLevelScope", null);
            Assert.That(scope, Does.EndWith("/freehand-fixture"), "The database GUID and the level id.");
            Assert.That(m_mirrored, Has.Count.EqualTo(1), "The new piece opens in the Piece Editor.");
            Assert.That(m_mirrored[0].LevelId, Is.EqualTo(scope));
            // The piece's identity is its session token (DEC-193); its name is "Freehand fixture Piece 1".
            string token = m_mirrored[0].PieceId;
            Assert.That(token, Does.StartWith("piece:" + scope + ":"));
            Assert.That(m_mirrored[0].Name, Is.EqualTo("Freehand fixture Piece 1"));

            // An L: down the left column, then one step right at the top.
            Stroke(root, canvas, new Vector2Int(0, 0), new Vector2Int(0, 2), new Vector2Int(1, 2));

            LevelData after = GetDraft();
            Assert.That(after.BlockCount, Is.EqualTo(before.BlockCount + 1));
            LevelBlockData block = after.GetBlockAt(after.BlockCount - 1);
            Vector2Int[] drawn = { Vector2Int.zero, Vector2Int.up, new Vector2Int(0, 2), new Vector2Int(1, 2) };
            Assert.That(block.Origin, Is.EqualTo(Vector2Int.zero));
            Assert.That(Cells(block), Is.EquivalentTo(drawn));
            Assert.That(block.ColorId, Is.EqualTo(state.ColorId));
            Assert.That(state.IsFreehandArmed, Is.True, "The tile stays armed for the next stroke.");
            Assert.That(m_mirrored, Has.Count.EqualTo(2), "Each released stroke is mirrored once.");
            Assert.That(m_mirrored[1].PieceId, Is.EqualTo(token));
            Assert.That(m_mirrored[1].Cells, Is.EquivalentTo(drawn), "The normalized footprint.");

            // A later stroke next to the piece adds its cells to the same block (click-to-grow, user feedback of
            // 2026-09-25); one away from it is refused.
            Stroke(root, canvas, new Vector2Int(3, 0), new Vector2Int(3, 1));
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(JsonUtility.ToJson(after)));
            Assert.That(root.Q<HelpBox>(LevelDatabaseWindow.k_hintBoxName).text,
                Is.EqualTo("Click next to Freehand fixture Piece 1."));
            Stroke(root, canvas, new Vector2Int(1, 1), new Vector2Int(2, 1));
            LevelData grown = GetDraft();
            Assert.That(grown.BlockCount, Is.EqualTo(before.BlockCount + 1));
            Assert.That(grown.GetBlockAt(grown.BlockCount - 1).Id, Is.EqualTo(block.Id), "The same block.");
            Assert.That(grown.GetBlockAt(grown.BlockCount - 1).Origin, Is.EqualTo(Vector2Int.zero));
            Assert.That(Cells(grown.GetBlockAt(grown.BlockCount - 1)), Is.EquivalentTo(new[]
            {
                Vector2Int.zero, Vector2Int.up, new Vector2Int(0, 2), new Vector2Int(1, 2), Vector2Int.one,
                new Vector2Int(2, 1)
            }));
            Assert.That(m_mirrored, Has.Count.EqualTo(3));

            Undo.PerformUndo();
            yield return null;
            Assert.That(Cells(GetDraft().GetBlockAt(GetDraft().BlockCount - 1)), Is.EquivalentTo(drawn),
                "One undo restores the first stroke.");
            Undo.PerformUndo();
            yield return null;
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(JsonUtility.ToJson(before)));
        }

        [UnityTest]
        public IEnumerator ClicksGrowThePieceNextToItsBlockAndARightClickTakesACellOff()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = RequireCanvas(root);
            UseLevelPieceLibrary();
            LoadFixture(root, CreateFixture(new LevelBlockData("b1", "Red", new Vector2Int(4, 4), s_square)));
            Invoke("CreateNamedPiece");
            m_mirrored.Clear();
            LevelShapePalette palette = root.Q<LevelShapePalette>();
            Label status = root.Q<Label>("status");
            Assert.That(canvas.CanGrowPieceAt(new Vector2Int(3, 3)), Is.True, "Before the first cell any free cell is green.");

            // User feedback of 2026-09-25: single clicks build the piece, not only a press-and-drag.
            Click(root, CellCenter(canvas, new Vector2Int(1, 1)));
            LevelBlockData block = GetPieceBlock();
            Assert.That(block.Origin, Is.EqualTo(Vector2Int.one));
            Assert.That(Cells(block), Is.EquivalentTo(new[] { Vector2Int.zero }));
            Assert.That(canvas.FreehandBlockId, Is.EqualTo(block.Id));
            Assert.That(canvas.CanGrowPieceAt(new Vector2Int(2, 1)), Is.True, "A free cell next to the piece is green.");
            Assert.That(canvas.CanGrowPieceAt(new Vector2Int(3, 3)), Is.False, "One away from it is red.");
            Assert.That(canvas.CanGrowPieceAt(new Vector2Int(5, 5)), Is.False, "Another block's cell is red.");

            Click(root, CellCenter(canvas, new Vector2Int(2, 1)));
            Click(root, CellCenter(canvas, new Vector2Int(3, 1)));
            Assert.That(GetPieceBlock().Id, Is.EqualTo(block.Id), "Each click grows the same block.");
            Vector2Int[] line = { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) };
            Assert.That(Cells(GetPieceBlock()), Is.EquivalentTo(line));
            Assert.That(GetTileCells(palette), Is.EquivalentTo(line), "The piece's tile shows the current footprint.");
            Assert.That(GetPieceTypeLabel(), Is.EqualTo("Freehand fixture Piece 1 (level piece)"));
            Assert.That(m_mirrored, Has.Count.EqualTo(3), "Each click is mirrored into the Piece Editor.");
            Assert.That(m_mirrored[2].Name, Is.EqualTo("Freehand fixture Piece 1"));
            Assert.That(m_mirrored[2].Cells, Is.EquivalentTo(line));

            string grown = JsonUtility.ToJson(GetDraft());
            Click(root, CellCenter(canvas, new Vector2Int(3, 3)));
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(grown), "A click away from the piece changes nothing.");
            Assert.That(root.Q<HelpBox>(LevelDatabaseWindow.k_hintBoxName).text,
                Is.EqualTo("Click next to Freehand fixture Piece 1."));

            RightClick(root, CellCenter(canvas, new Vector2Int(2, 1)));
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(grown));
            Assert.That(status.text, Does.Contain("would split Freehand fixture Piece 1"));

            RightClick(root, CellCenter(canvas, new Vector2Int(1, 1)));
            Assert.That(GetPieceBlock().Id, Is.EqualTo(block.Id));
            Assert.That(GetPieceBlock().Origin, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(Cells(GetPieceBlock()), Is.EquivalentTo(new[] { Vector2Int.zero, Vector2Int.right }));
            Assert.That(GetTileCells(palette), Is.EquivalentTo(new[] { Vector2Int.zero, Vector2Int.right }));
            RightClick(root, CellCenter(canvas, new Vector2Int(2, 1)));
            int blocks = GetDraft().BlockCount;
            RightClick(root, CellCenter(canvas, new Vector2Int(3, 1)));
            // R-A (user request of 2026-09-26): the last cell removes the block, and the piece no block uses any more
            // leaves the Level pieces list.
            Assert.That(GetDraft().BlockCount, Is.EqualTo(blocks - 1), "The last cell removes the piece's block.");
            Assert.That(GetTileCells(palette), Is.Empty, "The piece leaves the Level pieces list.");
            Assert.That(status.text, Is.EqualTo("Deleted Freehand fixture Piece 1: no block uses it any more. Undo brings it back."));

            // Each change is one undo step, and the tile follows the undo.
            Undo.PerformUndo();
            yield return null;
            Assert.That(Cells(GetPieceBlock()), Is.EquivalentTo(new[] { Vector2Int.zero }), "Undo brings the block back.");
            Assert.That(GetTileCells(palette), Is.EquivalentTo(new[] { Vector2Int.zero }), "And its tile.");
            Undo.PerformUndo();
            yield return null;
            Assert.That(Cells(GetPieceBlock()), Is.EquivalentTo(new[] { Vector2Int.zero, Vector2Int.right }));
            Assert.That(GetTileCells(palette), Is.EquivalentTo(new[] { Vector2Int.zero, Vector2Int.right }));
        }

        [UnityTest]
        public IEnumerator ErasingAPiecesLastBlockWithAnotherToolDeletesItsTileUntilUndo()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = RequireCanvas(root);
            UseLevelPieceLibrary();
            LoadFixture(root, CreateFixture(new LevelBlockData("b1", "Red", new Vector2Int(4, 4), s_square)));
            Invoke("CreateNamedPiece");
            LevelShapePalette palette = root.Q<LevelShapePalette>();
            Click(root, CellCenter(canvas, new Vector2Int(1, 1)));
            Click(root, CellCenter(canvas, new Vector2Int(2, 1)));
            Assume.That(GetTileCells(palette).Count, Is.EqualTo(2));

            // R-A: the Eraser removes the block like a right click with any tool, and the tile goes with it. "+" keeps
            // the Pen, where 3 picks a colour (TASK-20260926-180414067-b923c8a9), so the Eraser button picks it up.
            Press(root.Q<Button>("tool-eraser"));
            Click(root, CellCenter(canvas, new Vector2Int(2, 1)));
            Assert.That(GetDraft().BlockCount, Is.EqualTo(1), "Only b1 is left.");
            Assert.That(GetTileCells(palette), Is.Empty, "A non-template piece no block uses leaves the Level pieces list.");

            Undo.PerformUndo();
            yield return null;
            Assert.That(GetDraft().BlockCount, Is.EqualTo(2));
            Assert.That(GetTileCells(palette).Count, Is.EqualTo(2), "Undo brings the tile back with the block.");
        }

        [UnityTest]
        public IEnumerator AnUndoHereMirrorsTheUndoneFootprintSoTheNextPieceEditorChangeBuildsOnIt()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = RequireCanvas(root);
            UseLevelPieceLibrary();
            LoadFixture(root, CreateFixture(new LevelBlockData("b1", "Red", new Vector2Int(4, 4), s_square)));
            Invoke("CreateNamedPiece");
            m_pieceEditorOpen = true;
            Click(root, CellCenter(canvas, new Vector2Int(1, 1)));
            Click(root, CellCenter(canvas, new Vector2Int(2, 1)));
            Click(root, CellCenter(canvas, new Vector2Int(3, 1)));
            Assume.That(GetPieceBlock().CellCount, Is.EqualTo(3));
            m_mirrored.Clear();

            Undo.PerformUndo();
            yield return null;

            // The Piece Editor's draft is not in this window's undo record, so the undone footprint is sent to it.
            Assert.That(m_mirrored, Has.Count.EqualTo(1));
            Assert.That(m_mirrored[0].Cells, Is.EquivalentTo(new[] { Vector2Int.zero, Vector2Int.right }));
            Assert.That(m_mirrored[0].Name, Is.EqualTo("Freehand fixture Piece 1"));

            // The Piece Editor's next change builds on those two cells: the undone third cell does not come back.
            Vector2Int[] next = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };
            Invoke("OnLevelPieceChanged",
                new LevelPieceRequest(m_mirrored[0].LevelId, m_mirrored[0].PieceId, next, m_mirrored[0].Name));
            Undo.FlushUndoRecordObjects();

            Assert.That(GetPieceBlock().Origin, Is.EqualTo(Vector2Int.one));
            Assert.That(Cells(GetPieceBlock()), Is.EquivalentTo(next));
            Assert.That(m_mirrored, Has.Count.EqualTo(1), "A change from the Piece Editor is not echoed back.");
        }

        [UnityTest]
        public IEnumerator APieceEditorChangeThatMakesBlocksOverlapIsAppliedMarkedAndHoldsSaveUntilTheyAreApart()
        {
            // The user's case of 2026-09-26 (contract D3-B): two squares of one named piece, b1 above b2; the Piece
            // Editor adds a cell on top of the left column, so b2's new cell lands on b1 at (0,2).
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = RequireCanvas(root);
            LoadFixture(root, CreateFixture(new LevelBlockData("b9", "Red", new Vector2Int(5, 5), s_single)));
            Invoke("CreateNamedPiece");
            string scope = (string)LevelEditorTestAccess.Invoke(m_window, "GetOpenLevelScope", null);
            Vector2Int[] square = { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.one };
            LoadFixture(root, CreateFixture(
                new LevelBlockData("b1", "Red", new Vector2Int(0, 2), square),
                new LevelBlockData("b2", "Red", Vector2Int.zero, square)));
            LevelPieceSession session =
                (LevelPieceSession)LevelEditorTestAccess.Get(m_window, "m_pieceSession");
            // b1 and b2 are the piece's blocks (DEC-193: a piece's changes move only its recorded blocks).
            LevelNamedPiece piece = session.DrawingPiece;
            piece.SetCells(square);
            piece.SetMembers(new[] { "b1", "b2" });
            Button save = root.Q<Button>("save-level");
            Assume.That(save.enabledSelf, Is.True, "The fixture saves before the change.");
            LevelData before = GetDraft();

            Vector2Int[] grown = { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.one, new Vector2Int(0, 2) };
            Invoke("OnLevelPieceChanged", new LevelPieceRequest(scope, piece.Token, grown, "Freehand fixture Piece 1"));
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();

            LevelData changed = GetDraft();
            Assert.That(Cells(changed.GetBlockAt(1)), Is.EquivalentTo(grown), "The change is applied, not refused.");
            Assert.That(GetLabelTexts(canvas), Is.EqualTo(new[] { "<s>B1</s>", "<s>B2</s>" }));
            Assert.That(root.Q<Label>("status").text, Does.Contain("Re-stamped 2 block(s): b1, b2.")
                .And.Contain("Blocks b1 and b2 overlap"));
            Assert.That(save.enabledSelf, Is.False, "Save Level waits until the blocks are apart.");
            Assert.That(save.tooltip, Is.EqualTo(LevelDatabaseWindow.k_saveBlockedTooltip));

            // Moving b1 two cells right separates them: both marks and the Save gate clear at once.
            Invoke("MoveBlock", 0, new Vector2Int(2, 0));
            Assert.That(GetDraft().GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(2, 2)));
            Assert.That(GetLabelTexts(canvas), Is.EqualTo(new[] { "B1", "B2" }));
            Assert.That(save.enabledSelf, Is.True);
            Assert.That(save.tooltip, Is.Not.EqualTo(LevelDatabaseWindow.k_saveBlockedTooltip));

            Undo.PerformUndo();
            yield return null;
            Assert.That(GetLabelTexts(canvas), Is.EqualTo(new[] { "<s>B1</s>", "<s>B2</s>" }), "Undo brings it back.");
            Assert.That(save.enabledSelf, Is.False);
            Undo.PerformUndo();
            yield return null;
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(JsonUtility.ToJson(before)), "One undo per step.");
            Assert.That(GetLabelTexts(canvas), Is.EqualTo(new[] { "B1", "B2" }));
            Assert.That(save.enabledSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator SplitOrOversizedStrokesAreRefusedAndEscCancelsAStroke()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = RequireCanvas(root);
            LoadFixture(root, CreateFixture(new LevelBlockData("b1", "Red", new Vector2Int(1, 0), s_single)));
            Invoke("CreateNamedPiece");
            m_mirrored.Clear();
            string before = JsonUtility.ToJson(GetDraft());

            // Across b1: the occupied cell is skipped, so the stroke splits.
            Stroke(root, canvas, new Vector2Int(0, 0), new Vector2Int(2, 0));
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before));
            Assert.That(root.Q<Label>("status").text, Does.Contain("touch edge to edge"));

            // Six cells in a row do not fit 5 x 5.
            Stroke(root, canvas, new Vector2Int(0, 3), new Vector2Int(5, 3));
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before));
            Assert.That(root.Q<Label>("status").text, Does.Contain("5 x 5"));

            // Esc drops a stroke in progress; the release then places nothing and the Pen stays in freehand mode.
            SendPointer(root, EventType.MouseDown, CellCenter(canvas, new Vector2Int(0, 1)));
            SendPointer(root, EventType.MouseDrag, CellCenter(canvas, new Vector2Int(2, 1)));
            Assert.That(canvas.IsPieceStrokeActive, Is.True);
            Assert.That(canvas.PieceStroke, Has.Count.EqualTo(3));
            SendKey(canvas, KeyCode.Escape);
            Assert.That(canvas.IsPieceStrokeActive, Is.False);
            SendPointer(root, EventType.MouseUp, CellCenter(canvas, new Vector2Int(2, 1)));
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before));
            Assert.That(GetToolState().IsFreehandArmed, Is.True);
            Assert.That(m_mirrored, Is.Empty, "Refused or cancelled strokes mirror nothing.");

            // A press on a block starts no stroke.
            SendPointer(root, EventType.MouseDown, CellCenter(canvas, new Vector2Int(1, 0)));
            Assert.That(canvas.IsPieceStrokeActive, Is.False);
            SendPointer(root, EventType.MouseUp, CellCenter(canvas, new Vector2Int(1, 0)));
        }

        [UnityTest]
        public IEnumerator AStrokeSkipsBoardObjectCells()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = RequireCanvas(root);
            // A barrier is a board object the general place rule allows, so only the freehand rule keeps it out.
            LevelCellObjectData barrier = LevelEffectTests.Object("o1",
                LevelEffectTests.Effect("barrier"), new Vector2Int(1, 3));
            LoadFixture(root, new LevelData("freehand-fixture", "Freehand fixture", 6, 6, 60, Array.Empty<Vector2Int>(),
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red") },
                new[] { new LevelBlockData("b1", "Red", new Vector2Int(5, 5), s_single) }, new[] { barrier }));
            Invoke("CreateNamedPiece");
            m_mirrored.Clear();
            string before = JsonUtility.ToJson(GetDraft());

            SendPointer(root, EventType.MouseDown, CellCenter(canvas, new Vector2Int(1, 3)));
            Assert.That(canvas.IsPieceStrokeActive, Is.False, "A press on a board object starts no stroke.");
            SendPointer(root, EventType.MouseUp, CellCenter(canvas, new Vector2Int(1, 3)));

            SendPointer(root, EventType.MouseDown, CellCenter(canvas, new Vector2Int(0, 3)));
            SendPointer(root, EventType.MouseDrag, CellCenter(canvas, new Vector2Int(2, 3)));
            Assert.That(canvas.PieceStroke, Is.EquivalentTo(new[] { new Vector2Int(0, 3), new Vector2Int(2, 3) }),
                "The walk crosses the barrier without taking it.");
            SendPointer(root, EventType.MouseUp, CellCenter(canvas, new Vector2Int(2, 3)));

            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before), "The split stroke is refused.");
            Assert.That(m_mirrored, Is.Empty);
        }

        [UnityTest]
        public IEnumerator APenClickStartsEditingAndATemplateEditReachesEveryBlockOfTheTemplate()
        {
            // Pen-edit task (user request of 2026-09-26): a Pen left click on a block starts editing it, and a template
            // edit changes every block of that template in the level (DEC-125, superseding the select-mode AC-6).
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = RequireCanvas(root);
            UseLibrary(Shape("Single", s_single), Shape("DoubleHorizontal", s_domino));
            LoadFixture(root, CreateFixture(new LevelBlockData("b1", "Red", new Vector2Int(5, 5), s_single),
                new LevelBlockData("b2", "Blue", new Vector2Int(1, 1), s_single)));
            string fixture = JsonUtility.ToJson(GetDraft());
            string library = File.ReadAllText(BoardKitAssets.ShapesJsonPath);
            LevelShapePalette palette = root.Q<LevelShapePalette>();
            Label status = root.Q<Label>("status");
            SendKey(canvas, KeyCode.Alpha2);
            Assume.That(GetToolState().HasArmedShape || GetToolState().IsFreehandArmed, Is.False);
            Assert.That(canvas.IsFreehand, Is.False, "The automatic default selection is not edited.");

            Click(root, CellCenter(canvas, new Vector2Int(5, 5)));
            Assert.That(canvas.IsFreehand, Is.True, "A Pen click on the block starts editing it.");
            Assert.That(canvas.FreehandBlockId, Is.EqualTo("b1"));
            Assert.That(GetToolState().HasArmedShape, Is.False, "Nothing is armed.");
            Assert.That(status.text, Does.Contain("template Single"));
            // The template banner now sits above the canvas, so cell positions are read from the new layout.
            ValidateLayout(root);

            Stroke(root, canvas, new Vector2Int(0, 3), new Vector2Int(2, 3));
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixture), "Cells away from the block change nothing.");
            // A press on empty space ends the edit (user order of 2026-09-26), so b1 is clicked again.
            Assert.That(canvas.IsFreehand, Is.False);
            Assert.That(status.text, Is.EqualTo("Editing ended; nothing is selected."));
            ValidateLayout(root);
            Click(root, CellCenter(canvas, new Vector2Int(5, 5)));
            Assume.That(canvas.FreehandBlockId, Is.EqualTo("b1"));
            ValidateLayout(root);

            Click(root, CellCenter(canvas, new Vector2Int(4, 5)));
            LevelData grown = GetDraft();
            Assert.That(grown.GetBlockAt(0).Id, Is.EqualTo("b1"));
            Assert.That(grown.GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(4, 5)));
            Assert.That(Cells(grown.GetBlockAt(0)), Is.EquivalentTo(s_domino));
            Assert.That(Cells(grown.GetBlockAt(1)), Is.EquivalentTo(s_domino), "The template's other block follows.");
            Assert.That(GetTileCells(palette), Is.Empty, "A template edit makes no level piece.");
            Assert.That(File.ReadAllText(BoardKitAssets.ShapesJsonPath), Is.EqualTo(library), "No library write.");

            // Back to one cell: the footprint is the library's again, so no template change is pending.
            ValidateLayout(root);
            RightClick(root, CellCenter(canvas, new Vector2Int(4, 5)));
            Assert.That(Cells(GetDraft().GetBlockAt(0)), Is.EquivalentTo(s_single));
            Assert.That(Cells(GetDraft().GetBlockAt(1)), Is.EquivalentTo(s_single));
            LevelDraft draft = (LevelDraft)LevelEditorTestAccess.Get(m_window, "m_draft");
            Assert.That(draft.TemplateEdits, Is.Empty);

            Undo.PerformUndo();
            yield return null;
            Assert.That(Cells(GetDraft().GetBlockAt(1)), Is.EquivalentTo(s_domino), "One undo step per change.");
            Undo.PerformUndo();
            yield return null;
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixture), "Each change was one undo step.");
        }

        [UnityTest]
        public IEnumerator APenWithNoBlockToDrawOnSaysWhatItIsFor()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = RequireCanvas(root);
            LoadFixture(root, CreateFixture());
            SendKey(canvas, KeyCode.Alpha2);
            string before = JsonUtility.ToJson(GetDraft());

            Stroke(root, canvas, new Vector2Int(0, 0), new Vector2Int(2, 0));

            Assert.That(canvas.IsFreehand, Is.False, "No block is selected, so there is nothing to draw on.");
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before), "No implicit freehand stroke.");
            Assert.That(root.Q<HelpBox>(LevelDatabaseWindow.k_hintBoxName).text, Is.EqualTo(LevelDatabaseWindow.k_penHint));
        }

        private static LevelData CreateFixture(params LevelBlockData[] blocks)
        {
            return new LevelData("freehand-fixture", "Freehand fixture", 6, 6, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red") }, blocks);
        }

        private static BlockShapeLibrary.Shape Shape(string displayName, Vector2Int[] cells)
        {
            return new BlockShapeLibrary.Shape(displayName, cells);
        }

        // A block type is its footprint (DEC-193), so a drawn single, domino or line would be the shipped Single,
        // Double or Triple template. A library with only a square keeps those footprints level pieces;
        // the fixture's other block is that square, so it is no level piece of its own.
        private void UseLevelPieceLibrary()
        {
            UseLibrary(Shape("Square", s_square));
        }

        // A transient library of these templates; no asset is written.
        private void UseLibrary(params BlockShapeLibrary.Shape[] shapes)
        {
            m_library = ScriptableObject.CreateInstance<BlockShapeLibrary>();
            m_library.Load(BlockShapeLibraryJson.Write(shapes));
            LevelEditorTestAccess.Set(m_window, "m_shapeLibrary", m_library);
            Invoke("OnFocus");
        }

        private void Invoke(string method, params object[] arguments)
        {
            LevelEditorTestAccess.Invoke(m_window, method, arguments);
        }

        private LevelCanvasElement RequireCanvas(VisualElement root)
        {
            // Every caller immediately replaces this with its own fixture through LoadFixture or UseLibrary, so a
            // deterministic fixture level here only needs to guarantee the canvas exists and is laid out
            // (Tests-Editor-1-F3), whatever the real project's active database holds.
            m_fixture.LoadDatabase();
            // A fresh undo group after the fixture load, so the test's own first edit starts a clean group.
            Undo.IncrementCurrentGroup();
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(IsLevelLoaded(root) && canvas != null && canvas.Geometry.IsValid, Is.True,
                "The fixture database has a loaded level.");
            return canvas;
        }

        private void LoadFixture(VisualElement root, LevelData level)
        {
            m_fixture.LoadFixture(root, level);
        }

        private LevelData GetDraft()
        {
            return ((LevelDraft)LevelEditorTestAccess.Get(m_window, "m_draft")).Snapshot();
        }

        private LevelEditorToolState GetToolState()
        {
            return (LevelEditorToolState)LevelEditorTestAccess.Get(m_window, "m_toolState");
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

        // Presses on the first cell, moves through each further cell, and releases on the last.
        private static void Stroke(VisualElement root, LevelCanvasElement canvas, params Vector2Int[] cells)
        {
            SendPointer(root, EventType.MouseDown, CellCenter(canvas, cells[0]));
            for (int index = 1; index < cells.Length; index++)
            {
                SendPointer(root, EventType.MouseDrag, CellCenter(canvas, cells[index]));
            }

            SendPointer(root, EventType.MouseUp, CellCenter(canvas, cells[cells.Length - 1]));
        }

        private static Vector2 CellCenter(LevelCanvasElement canvas, Vector2Int cell)
        {
            return canvas.LocalToWorld(canvas.Geometry.GetCellRect(cell).center);
        }

        private static void Click(VisualElement root, Vector2 position)
        {
            UiTestInput.Click(root, position);
        }

        private static void RightClick(VisualElement root, Vector2 position)
        {
            UiTestInput.RightClick(root, position);
        }

        // The block of the piece freehand clicks grow.
        private LevelBlockData GetPieceBlock()
        {
            string id = m_window.rootVisualElement.Q<LevelCanvasElement>().FreehandBlockId;
            LevelData level = GetDraft();
            for (int index = 0; index < level.BlockCount; index++)
            {
                if (level.GetBlockAt(index).Id == id)
                {
                    return level.GetBlockAt(index);
                }
            }

            Assert.Fail("The piece has no block.");
            return null;
        }

        // The cells the piece's Level pieces tile draws.
        private static IReadOnlyList<Vector2Int> GetTileCells(LevelShapePalette palette)
        {
            for (int index = 0; index < palette.CustomTileCount; index++)
            {
                if (palette.GetCustomChoice(index).DisplayName == "Freehand fixture Piece 1")
                {
                    return palette.GetCustomChoice(index).Cells;
                }
            }

            return Array.Empty<Vector2Int>();
        }

        private static List<string> GetLabelTexts(LevelCanvasElement canvas)
        {
            return canvas.Query<Label>(className: LevelCanvasElement.k_labelClass).ToList()
                .ConvertAll(label => label.text);
        }

        private string GetPieceTypeLabel()
        {
            return ((DropdownField)LevelEditorTestAccess.Get(m_window, "m_pieceTypeField"))
                .value;
        }

        private static void SendPointer(VisualElement root, EventType type, Vector2 position, int button = 0)
        {
            UiTestInput.SendPointer(root, type, position, button);
        }

        // Submit presses a button the way a click does, wherever the tools view is scrolled.
        private static void Press(Button button)
        {
            UiTestInput.Press(button);
        }

        // UI Toolkit routes keys to the focused element, so the target is focused first.
        private static void SendKey(VisualElement target, KeyCode key)
        {
            UiTestInput.SendKey(target, key);
        }

        private static IEnumerator ScrollTo(VisualElement root, VisualElement element)
        {
            root.Q<ScrollView>("tools-pane").ScrollTo(element);
            yield return null;
            ValidateLayout(root);
        }

        // Unity builds this window's GUI itself: a manual CreateGUI could be followed by Unity's own call, which
        // rebuilds the draft and clears its undo records between an edit and its undo.
        private IEnumerator ShowWindowBuiltByUnity()
        {
            LevelDatabaseWindow window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            window.IsPieceEditorOpen = () => m_pieceEditorOpen;
            window.ShowLevelPieceInEditor = m_mirrored.Add;
            window.ShowLibraryPieceInEditor = _ => { };
            m_fixture = new LevelEditorWindowFixture();
            yield return m_fixture.ShowBuiltByUnity(window);
            m_window = m_fixture.Window;
        }

        private static bool IsLevelLoaded(VisualElement root)
        {
            return LevelEditorWindowFixture.IsLevelLoaded(root);
        }

        private static void ValidateLayout(VisualElement root)
        {
            UiTestInput.ValidateLayout(root);
        }
    }
}
