using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor's block tools through real pointer and key events: place, drag, nudge, recolour and
    /// remove each make one draft edit and one undo step, refusals change nothing and name the cell, and nothing is
    /// written to the level JSON.
    /// </summary>
    public sealed class LevelCanvasBlockToolsTests
    {
        private LevelEditorWindowFixture m_fixture;
        private LevelDatabaseWindow m_window;

        [TearDown]
        public void CloseWindow()
        {
            m_fixture?.Dispose();
            m_fixture = null;
            m_window = null;
        }

        [Test]
        public void ToolsColumnHoldsSwatchesShapesAndEditControls()
        {
            VisualElement root = OpenWindow();
            VisualElement tools = root.Q<ScrollView>("tools-pane");

            LevelColorSwatches swatches = tools.Q<Foldout>("colors-group").Q<LevelColorSwatches>();
            Assert.That(swatches.Query(className: LevelColorSwatches.k_swatchClass).ToList(), Has.Count.EqualTo(11));
            // The tooltip names the swatch's Pen colour key in the order the window set (TASK-20260926-180414067-b923c8a9).
            Assert.That(swatches.GetSwatch(PaletteColor.Red).tooltip, Is.EqualTo("Red: key 1 with the Pen ([ / ] cycles)"));
            Assert.That(swatches.GetSwatch(PaletteColor.Pink).style.backgroundColor.value,
                Is.EqualTo(root.Q<LevelCanvasElement>().GetPaletteColor(PaletteColor.Pink)), "Swatches match the canvas.");

            Foldout blocks = tools.Q<Foldout>("blocks-group");
            LevelShapePalette shapes = blocks.Q<LevelShapePalette>();
            Assert.That(shapes, Is.Not.Null);
            Assert.That(shapes.TileCount + (shapes.Q<Label>(LevelShapePalette.k_messageName).text.Length > 0 ? 1 : 0),
                Is.GreaterThan(0), "The shape list shows shapes or the library's error.");
            Assert.That(blocks.Q<Button>("flip-block-v"), Is.Null, "The transforms moved to Tools.");

            Foldout toolsGroup = tools.Q<Foldout>("tools-group");
            Assert.That(tools.contentContainer.IndexOf(toolsGroup), Is.Zero, "The Tools group comes first.");
            string[] toolButtons = { "tool-select", "tool-pen", "tool-eraser" };
            VisualElement toolRowElement = toolsGroup.Q("canvas-tools");
            Assert.That(toolRowElement.Query<Button>().ToList().ConvertAll(button => button.name), Is.EqualTo(toolButtons),
                "The Rotate tool is gone; the transforms under the divider replace it.");
            for (int index = 0; index < toolButtons.Length; index++)
            {
                Button button = toolsGroup.Q<Button>(toolButtons[index]);
                Assert.That(button, Is.Not.Null, toolButtons[index]);
                Assert.That(button.tooltip, Does.Contain($"({index + 1})"), "The tooltip names the tool's key.");
            }

            int toolRow = toolsGroup.IndexOf(toolsGroup.Q("canvas-tools"));
            Assert.That(toolsGroup.IndexOf(toolsGroup.Q("tools-divider")), Is.EqualTo(toolRow + 1),
                "A divider follows the tools.");
            Assert.That(toolsGroup.IndexOf(toolsGroup.Q("block-actions")), Is.EqualTo(toolRow + 2),
                "The transforms sit under the divider.");

            Foldout edit = tools.Q<Foldout>("edit-group");
            Assert.That(edit.Q<Button>("tool-select"), Is.Null, "The tool buttons left the Edit group.");
            foreach (string button in new[] { "undo", "redo" })
            {
                Assert.That(edit.Q<Button>(button), Is.Not.Null, button);
                Assert.That(edit.Q<Button>(button).tooltip, Is.Not.Empty, button);
            }

            Assert.That(edit.Q<Button>("nudge-up"), Is.Null, "The arrow keys replace the nudge buttons.");

            Assert.That(root.Q<Button>("delete-block"), Is.Null, "The Eraser and the Delete key replace the button.");
        }

        [UnityTest]
        public IEnumerator PlacingAShapeAddsOneBlockAndPublishesItOnce()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = RequireCanvas(root);

            LevelShapePalette shapes = root.Q<LevelShapePalette>();
            VisualElement tile = null;
            for (int index = 0; index < shapes.TileCount && tile == null; index++)
            {
                tile = shapes.GetTile(index).enabledSelf ? shapes.GetTile(index) : null;
            }

            Assume.That(tile, Is.Not.Null, "The piece library has a shape with a prefab.");
            root.Q<ScrollView>("tools-pane").ScrollTo(tile);
            yield return null;
            ValidateLayout(root);
            Click(root, tile.worldBound.center, 0);
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Select), "A shape tile arms Select (2026-09-26).");

            LevelEditorToolState state = GetToolState();
            ShapeKey armedKey = state.ArmedKey;
            LevelData level = GetDraft();
            FindPlacements(level, state, canvas.Model, out Vector2Int valid, out Vector2Int invalid);
            Assume.That(valid.x, Is.GreaterThanOrEqualTo(0), "The level has room for the armed shape.");
            int publishCount = 0;
            LevelData published = null;
            void OnPublished(LevelData publishedLevel)
            {
                publishCount++;
                published = publishedLevel;
            }

            LevelDatabaseWindow.DraftLevelPublished += OnPublished;
            try
            {
                yield return WaitFor(1.0);
                publishCount = 0;

                ClickCell(root, canvas, valid, 0);

                Assert.That(canvas.Model.BlockCount, Is.EqualTo(level.BlockCount + 1));
                LevelBlockData block = GetDraft().GetBlockAt(level.BlockCount);
                Assert.That(block.Id, Is.EqualTo(LevelEditCommands.GetNextBlockId(level)));
                Assert.That(block.Origin, Is.EqualTo(valid));
                Assert.That(block.ColorId, Is.EqualTo(state.ColorId));
                Assert.That(block.Key, Is.EqualTo(armedKey));
                Assert.That(state.HasArmedShape, Is.False, "A successful placement drops the picked piece.");
                Assert.That(state.GhostCells, Is.Empty, "The held-piece ghost is cleared.");
                Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Select));
                Assert.That(tile.style.borderTopColor.value, Is.Not.EqualTo(LevelShapePalette.s_armedBorder),
                    "The template no longer has an armed outline.");
                Assert.That(m_window.hasUnsavedChanges, Is.True);
                yield return WaitFor(1.5);
                Assert.That(publishCount, Is.EqualTo(1), "One place reaches the preview once.");
                Assert.That(published?.BlockCount, Is.EqualTo(level.BlockCount + 1));

                if (invalid.x >= 0)
                {
                    Click(root, tile.worldBound.center, 0);
                    Assert.That(state.HasArmedShape, Is.True, "Pick the template again for another placement.");
                    LevelData placed = GetDraft();
                    LevelEditCommands.TryPlaceBlock(placed, state.ArmedCells, state.ColorId, invalid, out _,
                        out string issue);
                    ClickCell(root, canvas, invalid, 0);

                    Assert.That(canvas.Model.BlockCount, Is.EqualTo(placed.BlockCount), "A refused place adds nothing.");
                    Assert.That(root.Q<Label>("status").text, Is.EqualTo(issue));
                    Assert.That(issue, Does.Contain("("), "The message names the cell.");
                    Assert.That(state.HasArmedShape, Is.True, "A refused placement keeps the piece in hand.");
                }
            }
            finally
            {
                LevelDatabaseWindow.DraftLevelPublished -= OnPublished;
            }
        }

        [UnityTest]
        public IEnumerator RefusedPlacementsKeepThePieceAndSuccessfulPlacementReturnsToSelection()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = RequireCanvas(root, CreateRotateLevel());
            LevelEditorToolState state = GetToolState();
            state.SetColor(PaletteColor.Purple);
            state.Arm(LevelTestShapes.Template("Double", s_domino));
            LevelEditorTestAccess.Invoke(m_window, "SyncTool", null);
            string before = JsonUtility.ToJson(GetDraft());

            foreach (Vector2Int refused in new[] { new Vector2Int(1, 2), new Vector2Int(3, 3) })
            {
                ClickCell(root, canvas, refused, 0);
                Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before), "Overlap and off-board clicks change nothing.");
                Assert.That(root.Q<Label>("status").text, Does.Contain("("), "The refusal names the cell.");
                Assert.That(state.HasArmedShape, Is.True);
                Assert.That(state.GhostCells, Is.Not.Empty);
            }

            Vector2Int placedCell = new Vector2Int(0, 2);
            ClickCell(root, canvas, placedCell, 0);
            Assert.That(GetDraft().BlockCount, Is.EqualTo(4));
            Assert.That(GetDraft().GetBlockAt(3).ColorId, Is.EqualTo(PaletteColor.Purple.ToString()));
            Assert.That(state.HasArmedShape, Is.False);
            Assert.That(state.ArmedCells, Is.Empty);
            Assert.That(state.GhostCells, Is.Empty);
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Select));
            Assert.That(root.Q<Button>("tool-select").style.borderTopColor.value, Is.EqualTo(Color.white));
            Assert.That(canvas.Selection.Kind, Is.EqualTo(LevelCanvasSelectionKind.None));
            string placed = JsonUtility.ToJson(GetDraft());

            ClickCell(root, canvas, new Vector2Int(0, 3), 0);
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(placed), "A second empty click cannot place another block.");
            ClickCell(root, canvas, placedCell, 0);
            Assert.That(canvas.Selection.Index, Is.EqualTo(3), "The placed block can be selected normally.");

            Undo.PerformUndo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before), "One Undo removes the placement.");
            Assert.That(state.HasArmedShape, Is.False, "Undo does not pick up the template again.");
            Undo.PerformRedo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(placed), "One Redo restores the placement.");
            Assert.That(state.HasArmedShape, Is.False);
        }

        [UnityTest]
        public IEnumerator DraggingABlockMovesItAndUndoRestoresOriginCanvasAndDirtyState()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = RequireCanvas(root, CreateRotateLevel());
            Assert.That(canvas.Model.BlockCount, Is.GreaterThan(0), "The fixture level has a block.");

            LevelData level = GetDraft();
            FindMoves(level, 0, out Vector2Int valid, out Vector2Int invalid, out string invalidIssue);
            Assume.That(valid, Is.Not.EqualTo(Vector2Int.zero), "Block 0 has somewhere to go.");
            Vector2Int start = canvas.Model.GetBlock(0).GetCell(0);

            if (invalid != Vector2Int.zero)
            {
                Drag(root, canvas, start, start + invalid);

                Assert.That(GetDraft().GetBlockAt(0).Origin, Is.EqualTo(level.GetBlockAt(0).Origin), "It snaps back.");
                Assert.That(canvas.Model.GetBlock(0).GetCell(0), Is.EqualTo(start));
                Assert.That(root.Q<Label>("status").text, Is.EqualTo(invalidIssue));
                Assert.That(m_window.hasUnsavedChanges, Is.False);
            }

            Drag(root, canvas, start, start + valid);

            Assert.That(GetDraft().GetBlockAt(0).Origin, Is.EqualTo(level.GetBlockAt(0).Origin + valid));
            Assert.That(canvas.Model.GetBlock(0).GetCell(0), Is.EqualTo(start + valid));
            Assert.That(m_window.hasUnsavedChanges, Is.True);

            Undo.PerformUndo();

            Assert.That(GetDraft().GetBlockAt(0).Origin, Is.EqualTo(level.GetBlockAt(0).Origin), "Undo restores the origin.");
            Assert.That(canvas.Model.GetBlock(0).GetCell(0), Is.EqualTo(start), "Undo redraws the canvas.");
            Assert.That(m_window.hasUnsavedChanges, Is.False, "Undo restores the clean state.");
        }

        [UnityTest]
        public IEnumerator RightClickAndEraserRemoveABlockAsOneUndoStep()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = RequireCanvas(root, CreateRotateLevel());

            int count = canvas.Model.BlockCount;
            Assert.That(count, Is.GreaterThan(0), "The fixture level has a block.");
            string lastId = canvas.Model.GetBlock(count - 1).Id;

            ClickCell(root, canvas, canvas.Model.GetBlock(count - 1).LabelCell, 1);

            Assert.That(canvas.Model.BlockCount, Is.EqualTo(count - 1), "Right click removes the block.");
            Assert.That(m_window.hasUnsavedChanges, Is.True);
            Undo.PerformUndo();
            Assert.That(canvas.Model.BlockCount, Is.EqualTo(count));
            Assert.That(canvas.Model.GetBlock(count - 1).Id, Is.EqualTo(lastId));
            Assert.That(m_window.hasUnsavedChanges, Is.False);

            // Submit presses the button the way a click does; the Edit group can sit below the scrolled tools view.
            Button eraser = root.Q<Button>("tool-eraser");
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = eraser;
                eraser.SendEvent(submit);
            }

            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Eraser));

            ClickCell(root, canvas, canvas.Model.GetBlock(0).LabelCell, 0);

            Assert.That(canvas.Model.BlockCount, Is.EqualTo(count - 1), "The Eraser removes the block.");
            Undo.PerformUndo();
            Assert.That(canvas.Model.BlockCount, Is.EqualTo(count));
            Assert.That(m_window.hasUnsavedChanges, Is.False);

            SendKey(canvas, KeyCode.Escape, EventModifiers.None);
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Select), "Esc returns to Select.");
        }

        [Test]
        public void ArrowKeysNudgeAndSwatchesRecolourTheSelectedBlock()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = RequireCanvas(root, CreateRotateLevel());
            Assert.That(canvas.Model.BlockCount, Is.GreaterThan(0), "The fixture level has a block.");
            LevelData level = GetDraft();
            ClickCell(root, canvas, canvas.Model.GetBlock(0).LabelCell, 0);
            Assert.That(canvas.Selection.Kind, Is.EqualTo(LevelCanvasSelectionKind.Block));
            Assert.That(canvas.Selection.Index, Is.Zero);

            KeyCode[] keys = { KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow };
            Vector2Int[] steps = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            for (int index = 0; index < keys.Length; index++)
            {
                bool allowed = LevelEditCommands.TryMoveBlock(level, 0, level.GetBlockAt(0).Origin + steps[index],
                    out _, out string issue);
                SendKey(canvas, keys[index], EventModifiers.None);
                Vector2Int expected = level.GetBlockAt(0).Origin + (allowed ? steps[index] : Vector2Int.zero);
                Assert.That(GetDraft().GetBlockAt(0).Origin, Is.EqualTo(expected), keys[index].ToString());
                if (!allowed)
                {
                    Assert.That(root.Q<Label>("status").text, Is.EqualTo(issue));
                    continue;
                }

                level = GetDraft();
            }

            LevelColorSwatches swatches = root.Q<LevelColorSwatches>();
            PaletteColor other = level.GetBlockAt(0).ColorId == PaletteColor.Purple.ToString()
                ? PaletteColor.Orange
                : PaletteColor.Purple;
            Click(root, swatches.GetSwatch(other).worldBound.center, 0);
            Assert.That(GetDraft().GetBlockAt(0).ColorId, Is.EqualTo(other.ToString()), "A swatch recolours the selection.");

            SendKey(canvas, KeyCode.RightBracket, EventModifiers.None);
            Assert.That(GetDraft().GetBlockAt(0).ColorId, Is.EqualTo(((PaletteColor)((int)other + 1)).ToString()),
                "] cycles to the next colour.");
            Assert.That(GetDraft().GetBlockAt(0).Origin, Is.EqualTo(level.GetBlockAt(0).Origin), "Recolour keeps the place.");
            Assert.That(m_window.hasUnsavedChanges, Is.True);
        }

        [Test]
        public void SwatchesOnAFreshLevelSetOnlyTheToolColour()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = RequireCanvas(root, CreateRotateLevel());
            Assert.That(canvas.Model.BlockCount, Is.GreaterThan(0), "The fixture level has a block.");
            Assert.That(canvas.Selection.Kind, Is.EqualTo(LevelCanvasSelectionKind.Block), "Block 0 is the default.");
            string before = JsonUtility.ToJson(GetDraft());
            PaletteColor other = GetDraft().GetBlockAt(0).ColorId == PaletteColor.Purple.ToString()
                ? PaletteColor.Orange
                : PaletteColor.Purple;

            Click(root, root.Q<LevelColorSwatches>().GetSwatch(other).worldBound.center, 0);
            SendKey(canvas, KeyCode.RightBracket, EventModifiers.None);

            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before), "The default selection is not recoloured.");
            Assert.That(m_window.hasUnsavedChanges, Is.False);
            Assert.That(GetToolState().Color, Is.EqualTo((PaletteColor)((int)other + 1)), "The tool colour changes.");
        }

        [Test]
        public void ALibraryReloadedElsewhereRefreshesTheShapesAndDisarmsAChangedShape()
        {
            VisualElement root = OpenWindow();

            BlockShapeLibrary projectLibrary = (BlockShapeLibrary)LevelEditorTestAccess.Get(m_window, "m_shapeLibrary");
            IReadOnlyList<BlockShapeLibrary.Shape> shapes = null;
            try
            {
                shapes = projectLibrary != null ? projectLibrary.Shapes : null;
            }
            catch (System.Exception)
            {
                // Reported below: the project library is unreadable while the Piece Editor edits it.
            }

            if (shapes == null || shapes.Count < 2)
            {
                Assert.Ignore("The project piece library is missing, unreadable or has fewer than two shapes.");
            }

            BlockShapeLibrary.Shape first = shapes[0];
            // A piece fits inside 5 x 5 (DEC-193).
            Vector2Int[] line = new Vector2Int[Mathf.Min(first.Cells.Count + 1, ShapeKey.MaxExtent)];
            for (int index = 0; index < line.Length; index++)
            {
                line[index] = new Vector2Int(index, 0);
            }

            BlockShapeLibrary.Shape changed = new BlockShapeLibrary.Shape(first.DisplayName, line);
            // A piece is its footprint key (DEC-193): the added shape needs a key of its own, or it would be the armed
            // shape under another name and keep it armed.
            BlockShapeLibrary.Shape added = null;
            foreach (Vector2Int[] cells in new[]
                     {
                         new[] { Vector2Int.zero },
                         new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up },
                         new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0), new Vector2Int(1, 1) }
                     })
            {
                BlockShapeLibrary.Shape candidate = new BlockShapeLibrary.Shape("Added", cells);
                if (candidate.Key != first.Key && candidate.Key != changed.Key && candidate.Key != shapes[1].Key)
                {
                    added = candidate;
                    break;
                }
            }
            LevelShapePalette palette = root.Q<LevelShapePalette>();
            DropdownField pieceType = root.Query<DropdownField>().Where(field => field.label == "Piece Type").First();
            LevelEditorToolState state = GetToolState();
            // A transient library stands in for the project asset the Piece Editor reloads in place.
            BlockShapeLibrary library = ScriptableObject.CreateInstance<BlockShapeLibrary>();
            try
            {
                library.Load(BlockShapeLibraryJson.Write(new[] { first, shapes[1] }));
                LevelEditorTestAccess.Set(m_window, "m_shapeLibrary", library);
                FocusWindow();
                Assert.That(palette.TileCount, Is.EqualTo(2));
                state.Arm(GetShapeCatalog().Find(first.Key));
                // Pieces are armed with Select since the Select-mode placement task, so "armed" is HasArmedShape.
                Assume.That(state.HasArmedShape, Is.True);
                Assume.That(state.Tool, Is.EqualTo(LevelEditorTool.Select));

                library.Load(BlockShapeLibraryJson.Write(new[] { changed, shapes[1], added }));
                FocusWindow();

                Assert.That(palette.TileCount, Is.EqualTo(3), "The added shape gets a tile without a Reload.");
                Assert.That(pieceType.choices, Does.Contain(added.DisplayName), "Piece Type lists the added shape.");
                Assert.That(GetShapeCatalog().FindByName(first.DisplayName).Cells, Is.EqualTo(line),
                    "The new cells are used.");
                Assert.That(state.HasArmedShape, Is.False, "A shape whose cells changed is disarmed.");
                Assert.That(state.Tool, Is.EqualTo(LevelEditorTool.Select));
                Assert.That(root.Q<LevelCanvasElement>().Tool, Is.EqualTo(LevelEditorTool.Select));

                state.Arm(GetShapeCatalog().Find(shapes[1].Key));
                library.Load(BlockShapeLibraryJson.Write(new[] { changed, shapes[1], added }));
                FocusWindow();
                Assert.That(state.ArmedKey, Is.EqualTo(shapes[1].Key), "An unchanged shape stays armed.");

                Assert.Catch(() => library.Load("{"));
                FocusWindow();
                Assert.That(palette.TileCount, Is.Zero);
                Assert.That(palette.Q<Label>(LevelShapePalette.k_messageName).text, Is.Not.Empty, "The error shows.");
                Assert.That(state.Tool, Is.EqualTo(LevelEditorTool.Select), "A broken library disarms the Pen.");
            }
            finally
            {
                LevelEditorTestAccess.Set(m_window, "m_shapeLibrary", projectLibrary);
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void KeysInTextFieldsAreNotShortcuts()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = RequireCanvas(root, CreateRotateLevel());
            Assert.That(canvas.Model.BlockCount, Is.GreaterThan(0), "The fixture level has a block.");
            LevelData level = GetDraft();
            KeyCode[] keys = { KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow };
            Vector2Int[] steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            KeyCode key = KeyCode.None;
            for (int index = 0; index < keys.Length && key == KeyCode.None; index++)
            {
                bool allowed = LevelEditCommands.TryMoveBlock(level, 0, level.GetBlockAt(0).Origin + steps[index], out _,
                    out _);
                key = allowed ? keys[index] : KeyCode.None;
            }

            Assume.That(key, Is.Not.EqualTo(KeyCode.None), "The first block can move one cell.");
            TextField levelName = root.Q<TextField>("level-name");
            levelName.Focus();
            string before = JsonUtility.ToJson(level);

            SendKey(levelName, key, EventModifiers.None);

            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before), "An arrow key in the name field moves no block.");

            // The same key on the canvas does move the selected block.
            ClickCell(root, canvas, canvas.Model.GetBlock(0).LabelCell, 0);
            SendKey(canvas, key, EventModifiers.None);
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.Not.EqualTo(before));
        }

        [Test]
        public void ToolButtonsAndNumberKeysSwitchTheToolAndThePenNeverArmsAPiece()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            LevelEditorToolState state = GetToolState();
            // The first three have number keys; the rotate and mirror buttons are picked up the same exclusive way.
            string[] names =
            {
                "tool-select", "tool-pen", "tool-eraser", "rotate-block-ccw", "rotate-block-cw", "flip-block-h",
                "flip-block-v"
            };
            LevelEditorTool[] tools =
            {
                LevelEditorTool.Select, LevelEditorTool.Pen, LevelEditorTool.Eraser, LevelEditorTool.RotateCounterClockwise,
                LevelEditorTool.RotateClockwise, LevelEditorTool.MirrorHorizontal, LevelEditorTool.MirrorVertical
            };
            KeyCode[] keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };
            for (int index = 0; index < names.Length; index++)
            {
                Press(root.Q<Button>(names[index]));
                Assert.That(canvas.Tool, Is.EqualTo(tools[index]), names[index]);
                for (int other = 0; other < names.Length; other++)
                {
                    StyleKeyword highlight = root.Q<Button>(names[other]).style.backgroundColor.keyword;
                    Assert.That(highlight == StyleKeyword.Undefined, Is.EqualTo(other == index),
                        $"Only {names[index]} is highlighted.");
                }
            }

            for (int index = keys.Length - 1; index >= 0; index--)
            {
                if (canvas.Tool == LevelEditorTool.Pen)
                {
                    // With the Pen, 1 and 3 pick colours (TASK-20260926-180414067-b923c8a9): key 1 is pressed from the
                    // Eraser instead.
                    Press(root.Q<Button>("tool-eraser"));
                }

                SendKey(canvas, keys[index], EventModifiers.None);
                Assert.That(canvas.Tool, Is.EqualTo(tools[index]), keys[index].ToString());
            }

            SendKey(canvas, KeyCode.Alpha4, EventModifiers.None);
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Select), "4 no longer picks a tool.");

            SendKey(canvas, KeyCode.Alpha2, EventModifiers.None);
            Assert.That(state.HasArmedShape, Is.False, "The Pen never holds a piece.");
            if (IsLevelLoaded(root) && canvas.Geometry.IsValid)
            {
                // With no block to draw on, a Pen click on a cell says what the Pen is for (user order of 2026-09-26).
                LoadFixture(root, new LevelData("fixture", "Empty fixture", 4, 4, 60, new LevelDoorData[0],
                    new LevelBlockData[0]));
                string before = JsonUtility.ToJson(GetDraft());
                ClickCell(root, canvas, Vector2Int.zero, 0);
                Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before), "A Pen without a block changes nothing.");
                HelpBox hint = root.Q<HelpBox>(LevelDatabaseWindow.k_hintBoxName);
                Assert.That(hint.text, Is.EqualTo(LevelDatabaseWindow.k_penHint), "The hint box above the canvas says it.");
                Assert.That(hint.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(hint.pickingMode, Is.EqualTo(PickingMode.Ignore), "Clicks under the box reach the canvas.");
                Assert.That(hint.parent, Is.SameAs(canvas.parent), "The box floats over the canvas, not above it.");
                SendKey(canvas, KeyCode.Alpha1, EventModifiers.None);
                Assert.That(hint.style.display.value, Is.EqualTo(DisplayStyle.None), "A tool change clears the hint.");
                SendKey(canvas, KeyCode.Alpha2, EventModifiers.None);
                Assert.That(hint.style.display.value, Is.EqualTo(DisplayStyle.None),
                    "Picking up the Pen shows no hint until a click needs one.");
                Assert.That(root.Q<Label>("status").text, Is.Not.EqualTo(LevelDatabaseWindow.k_penHint),
                    "The status line keeps action results only.");
            }

            // Any template can be armed (DEC-193: templates carry no prefab).
            LevelShapeCatalog catalog = GetShapeCatalog();
            LevelShapeChoice piece = catalog.Count > 0 ? catalog.Get(0) : null;
            Assume.That(piece, Is.Not.Null, "The piece library has a template.");
            state.Arm(piece);
            SendKey(canvas, KeyCode.Alpha1, EventModifiers.None);
            Assert.That(state.HasArmedShape, Is.False, "Select disarms.");
            state.Arm(piece);
            SendKey(canvas, KeyCode.Alpha2, EventModifiers.None);
            Assert.That(state.HasArmedShape, Is.False, "The Pen disarms the piece instead of placing it.");
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Pen));
            // With the Pen, 2 picks a colour (TASK-20260926-180414067-b923c8a9), so the Pen button picks it up again.
            Press(root.Q<Button>("tool-pen"));
            Assert.That(state.HasArmedShape, Is.False, "The Pen never arms the last piece again.");
        }

        [UnityTest]
        public IEnumerator TransformToolsActOnTheClickedBlockWithoutASelection()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = RequireCanvas(root);

            LoadCornerFixture(root);
            string fixtureJson = JsonUtility.ToJson(GetDraft());
            Assert.That(canvas.Selection.Index, Is.Zero, "The domino is the automatic default selection.");
            string[] buttons = { "rotate-block-ccw", "rotate-block-cw", "flip-block-h", "flip-block-v" };
            LevelEditorTool[] tools =
            {
                LevelEditorTool.RotateCounterClockwise, LevelEditorTool.RotateClockwise, LevelEditorTool.MirrorHorizontal,
                LevelEditorTool.MirrorVertical
            };
            Vector2Int[][] expected =
            {
                LevelPieceTools.RotateCounterClockwise(s_corner), LevelPieceTools.RotateClockwise(s_corner),
                LevelPieceTools.FlipHorizontal(s_corner), LevelPieceTools.FlipVertical(s_corner)
            };

            for (int index = 0; index < buttons.Length; index++)
            {
                Press(root.Q<Button>(buttons[index]));
                Assert.That(canvas.Tool, Is.EqualTo(tools[index]), buttons[index]);
                Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixtureJson),
                    $"{buttons[index]} only picks up the tool; the selected block does not change.");

                ClickCell(root, canvas, new Vector2Int(0, 2), 0);
                LevelBlockData changed = GetDraft().GetBlockAt(1);
                Assert.That(LevelRecordCopy.GetCells(changed), Is.EqualTo(expected[index]), buttons[index]);
                Assert.That(changed.Origin, Is.EqualTo(new Vector2Int(0, 2)), "The origin stays.");
                Assert.That(canvas.Selection.Index, Is.EqualTo(1), "The clicked block becomes the selection.");
                Undo.PerformUndo();
                Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixtureJson), "One undo step restores the block.");
            }
        }

        [Test]
        public void RotateToolClicksTurnADominoAQuarterTurnEachAndFourRestoreIt()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = RequireCanvas(root, CreateRotateLevel());
            string fixtureJson = JsonUtility.ToJson(GetDraft());
            Press(root.Q<Button>("rotate-block-cw"));

            // D-001: the domino at (2,2) turns around its first cell (2,2), which every click hits.
            Vector2Int pivot = new Vector2Int(2, 2);
            Vector2Int[][] footprints =
            {
                new[] { pivot, new Vector2Int(2, 1) },
                new[] { pivot, new Vector2Int(1, 2) },
                new[] { pivot, new Vector2Int(2, 3) },
                new[] { pivot, new Vector2Int(3, 2) }
            };
            foreach (Vector2Int[] footprint in footprints)
            {
                ClickCell(root, canvas, pivot, 0);
                LevelBlockData block = GetDraft().GetBlockAt(2);
                Vector2Int[] cells = LevelRecordCopy.GetCells(block);
                Vector2Int[] absolute = new Vector2Int[cells.Length];
                for (int index = 0; index < cells.Length; index++)
                {
                    absolute[index] = block.Origin + cells[index];
                }

                Assert.That(absolute, Is.EquivalentTo(footprint), "Each click is one more clockwise quarter turn.");
            }

            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixtureJson), "Four clockwise clicks restore the domino.");
            Undo.PerformUndo();
            LevelBlockData undone = GetDraft().GetBlockAt(2);
            Assert.That(undone.Origin, Is.EqualTo(pivot), "Each click is one undo step: back to the third turn.");
            Assert.That(LevelRecordCopy.GetCells(undone), Is.EquivalentTo(new[] { Vector2Int.zero, Vector2Int.up }));
        }

        [Test]
        public void ATransformToolRefusesAnOverlapIgnoresEmptyCellsAndRightClickStillRemoves()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = RequireCanvas(root, CreateRotateLevel());
            LevelData fixture = GetDraft();
            string fixtureJson = JsonUtility.ToJson(fixture);
            Press(root.Q<Button>("rotate-block-cw"));

            // The turn around the domino's first cell would leave the bottom edge, so it slides up one cell onto b2.
            Vector2Int[] landed = LevelPieceTools.RotateClockwise(s_domino);
            Vector2Int shift = LevelPieceTools.GetTurnOriginShift(s_domino, true);
            shift += LevelPieceTools.GetBoardKick(landed, fixture.GetBlockAt(0).Origin + shift, fixture.Width, fixture.Height);
            Assume.That(shift, Is.EqualTo(Vector2Int.zero), "The kick slides the turned domino back to row 0.");
            for (int index = 0; index < landed.Length; index++)
            {
                landed[index] += shift;
            }

            LevelEditCommands.TryReplaceBlockCells(fixture, 0, landed, out _, out string issue);
            Assume.That(issue, Is.Not.Null, "The fixture's first domino cannot turn onto b2.");
            ClickCell(root, canvas, Vector2Int.zero, 0);
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixtureJson), "An overlapping turn changes nothing.");
            Assert.That(root.Q<Label>("status").text, Is.EqualTo(issue), "The refusal names the cell.");

            ClickCell(root, canvas, new Vector2Int(3, 0), 0);
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixtureJson), "A click on an empty cell changes nothing.");

            ClickCell(root, canvas, new Vector2Int(2, 2), 1);
            Assert.That(GetDraft().BlockCount, Is.EqualTo(2), "Right click removes a block with a transform tool too.");
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.RotateClockwise), "The tool stays in hand.");
        }

        [Test]
        public void TransformKeysTurnTheArmedGhostElseTheSelectedBlockWithoutChangingTheTool()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = RequireCanvas(root, CreateRotateLevel());
            string fixtureJson = JsonUtility.ToJson(GetDraft());
            LevelEditorToolState state = GetToolState();
            Vector2Int[] corner = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };
            state.Arm(LevelTestShapes.Template("Corner", corner));

            SendKey(canvas, KeyCode.R, EventModifiers.None);
            Assert.That(state.ArmedCells, Is.EqualTo(LevelPieceTools.RotateClockwise(corner)), "R turns the ghost.");
            SendKey(canvas, KeyCode.R, EventModifiers.Shift);
            Assert.That(state.ArmedCells, Is.EquivalentTo(corner), "Shift+R turns the ghost back.");
            SendKey(canvas, KeyCode.F, EventModifiers.None);
            Assert.That(state.ArmedCells, Is.EquivalentTo(LevelPieceTools.FlipHorizontal(corner)), "F mirrors the ghost.");
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Select), "The keys keep Select placing the piece.");
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixtureJson), "Ghost transforms never touch the draft.");

            // Transform-tools AC-8 as revised by the Select-mode placement task: with a piece armed, a transform button
            // acts on the ghost and Select keeps placing it.
            Vector2Int[] mirrored = LevelPieceTools.FlipHorizontal(corner);
            Press(root.Q<Button>("rotate-block-cw"));
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Select), "The button keeps Select in hand.");
            Assert.That(state.ArmedKey, Is.EqualTo(LevelTestShapes.Key(corner)), "The same piece stays armed.");
            Assert.That(state.ArmedCells, Is.EqualTo(LevelPieceTools.RotateClockwise(mirrored)), "The button turns the ghost.");
            Assert.That(root.Q<Button>("tool-select").style.borderTopColor.value, Is.EqualTo(Color.white),
                "Select stays the highlighted tool.");
            Press(root.Q<Button>("flip-block-v"));
            Assert.That(state.ArmedCells,
                Is.EqualTo(LevelPieceTools.FlipVertical(LevelPieceTools.RotateClockwise(mirrored))), "The button mirrors it.");
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Select));
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixtureJson), "Ghost transforms never touch the draft.");

            SendKey(canvas, KeyCode.Alpha1, EventModifiers.None);
            Assert.That(state.HasArmedShape, Is.False, "Key 1 drops the armed piece.");
            Press(root.Q<Button>("rotate-block-cw"));
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.RotateClockwise), "Without an armed piece it picks up the tool.");
            Assert.That(state.HasArmedShape, Is.False);
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixtureJson), "The button only picks up the tool.");

            SendKey(canvas, KeyCode.Alpha1, EventModifiers.None);
            ClickCell(root, canvas, new Vector2Int(2, 2), 0);
            Assert.That(canvas.Selection.Index, Is.EqualTo(2));
            Vector2Int[] upright = LevelPieceTools.RotateClockwise(s_domino);
            SendKey(canvas, KeyCode.R, EventModifiers.None);
            Assert.That(LevelRecordCopy.GetCells(GetDraft().GetBlockAt(2)), Is.EqualTo(upright),
                "Without a ghost R turns the selection.");
            SendKey(canvas, KeyCode.R, EventModifiers.None);
            Assert.That(LevelRecordCopy.GetCells(GetDraft().GetBlockAt(2)),
                Is.EqualTo(LevelPieceTools.RotateClockwise(upright)));
            SendKey(canvas, KeyCode.R, EventModifiers.Shift);
            Assert.That(LevelRecordCopy.GetCells(GetDraft().GetBlockAt(2)), Is.EqualTo(upright), "Shift+R turns it back.");

            SendKey(canvas, KeyCode.F, EventModifiers.Shift);
            Assert.That(LevelRecordCopy.GetCells(GetDraft().GetBlockAt(2)), Is.EqualTo(LevelPieceTools.FlipVertical(upright)),
                "Shift+F mirrors the selection vertically.");
            SendKey(canvas, KeyCode.F, EventModifiers.None);
            Assert.That(LevelRecordCopy.GetCells(GetDraft().GetBlockAt(2)),
                Is.EqualTo(LevelPieceTools.FlipHorizontal(LevelPieceTools.FlipVertical(upright))), "F mirrors it horizontally.");
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Select), "The keys never change the tool.");

            SendKey(canvas, KeyCode.Alpha2, EventModifiers.None);
            ClickCell(root, canvas, Vector2Int.zero, 1);
            Assert.That(GetDraft().BlockCount, Is.EqualTo(2), "Right click removes a block with the Pen too.");
        }

        [Test]
        public void AGateClickWithAnArmedPieceTakesTheGateColourWithoutSelectingIt()
        {
            // User order of 2026-09-26: with a piece in hand in Select, a left click on a gate changes the colour.
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = RequireCanvas(root);
            LoadFixture(root, new LevelData("fixture", "Gate fixture", 4, 4, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Pink") }, new LevelBlockData[0]));
            string before = JsonUtility.ToJson(GetDraft());
            LevelEditorToolState state = GetToolState();
            state.SetColor(PaletteColor.Red);
            state.Arm(LevelTestShapes.Template("Single", new[] { Vector2Int.zero }));
            MethodInfo select = typeof(LevelDatabaseWindow).GetMethod("SelectOnCanvas",
                BindingFlags.Instance | BindingFlags.NonPublic);

            select.Invoke(m_window, new object[] { LevelCanvasSelection.ForDoor(0, "d1") });

            Assert.That(state.Color, Is.EqualTo(PaletteColor.Pink), "The gate's colour is taken.");
            Assert.That(state.ArmedKey, Is.EqualTo(LevelTestShapes.Key(new[] { Vector2Int.zero })),
                "The piece stays in hand.");
            Assert.That(canvas.Selection.Kind, Is.Not.EqualTo(LevelCanvasSelectionKind.Door), "The gate is not selected.");
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before), "The draft does not change.");

            SendKey(canvas, KeyCode.Alpha1, EventModifiers.None);
            select.Invoke(m_window, new object[] { LevelCanvasSelection.ForDoor(0, "d1") });
            Assert.That(canvas.Selection.Kind, Is.EqualTo(LevelCanvasSelectionKind.Door),
                "Without a piece in hand the click selects the gate as before.");
        }

        [Test]
        public void APlacedTemplateBlockIsRecolouredByAClickOnItThenASwatch()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = RequireCanvas(root);
            LoadFixture(root, new LevelData("fixture", "Recolour fixture", 5, 5, 60, new LevelDoorData[0],
                new LevelBlockData[0]));
            LevelEditorToolState state = GetToolState();
            state.SetColor(PaletteColor.Red);
            Vector2Int[] lShape = { Vector2Int.zero, Vector2Int.up, new Vector2Int(0, 2), new Vector2Int(1, 2) };
            state.Arm(LevelTestShapes.Template("L Shape", lShape));
            LevelEditorTestAccess.Invoke(m_window, "SyncTool", null);
            Vector2Int hovered = new Vector2Int(2, 2);

            ClickCell(root, canvas, hovered, 0);
            Assert.That(GetDraft().BlockCount, Is.EqualTo(1), "The click places the L.");
            Click(root, root.Q<LevelColorSwatches>().GetSwatch(PaletteColor.Blue).worldBound.center, 0);
            Assert.That(GetDraft().GetBlockAt(0).ColorId, Is.EqualTo(PaletteColor.Red.ToString()),
                "A swatch after placing only changes the tool colour until a block is selected.");
            Assert.That(state.Color, Is.EqualTo(PaletteColor.Blue));
            Assert.That(state.HasArmedShape, Is.False, "The placed piece is no longer in hand.");

            ClickCell(root, canvas, hovered, 0);
            Click(root, root.Q<LevelColorSwatches>().GetSwatch(PaletteColor.Orange).worldBound.center, 0);
            Assert.That(GetDraft().GetBlockAt(0).ColorId, Is.EqualTo(PaletteColor.Orange.ToString()),
                "A swatch recolours the placed block the designer clicked.");
        }

        [Test]
        public void RotateKeyKeepsTheDefaultSelectionSoASwatchDoesNotRecolourIt()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = RequireCanvas(root, CreateRotateLevel());
            Assert.That(canvas.Selection.Index, Is.Zero, "Block 0 is the automatic default selection.");
            string colour = GetDraft().GetBlockAt(0).ColorId;

            SendKey(canvas, KeyCode.R, EventModifiers.None);
            PaletteColor other = colour == PaletteColor.Purple.ToString() ? PaletteColor.Orange : PaletteColor.Purple;
            Click(root, root.Q<LevelColorSwatches>().GetSwatch(other).worldBound.center, 0);

            Assert.That(GetDraft().GetBlockAt(0).ColorId, Is.EqualTo(colour),
                "R is not a choice, so a swatch still only sets the tool colour.");
            Assert.That(GetToolState().Color, Is.EqualTo(other));
        }

        [Test]
        public void ToolBadgeFollowsThePointerInAFrameAndTakesThePenColour()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = RequireCanvas(root);
            VisualElement badge = canvas.ToolBadge;
            Image icon = canvas.ToolBadgeIcon;
            Assert.That(badge.style.display.value, Is.EqualTo(DisplayStyle.None), "Hidden until the pointer enters.");
            Assert.That(badge.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(icon.parent, Is.SameAs(badge), "The icon sits inside the badge's square frame.");
            Assert.That(badge.style.width.value.value, Is.EqualTo(badge.style.height.value.value), "The frame is square.");

            Rect cell = canvas.Geometry.GetCellRect(Vector2Int.zero);
            Vector2 local = cell.center + new Vector2(-2f, 1f);
            SendPointer(root, EventType.MouseMove, canvas.LocalToWorld(local), 0);
            Assert.That(badge.style.display.value, Is.EqualTo(DisplayStyle.None), "The pointer tool shows only the system arrow.");

            SendKey(canvas, KeyCode.Alpha2, EventModifiers.None);
            SendPointer(root, EventType.MouseMove, canvas.LocalToWorld(local), 0);
            Assert.That(badge.style.display.value, Is.EqualTo(DisplayStyle.Flex), "The Pen shows its framed badge.");
            Translate translate = badge.style.translate.value;
            Assert.That(translate.x.value, Is.EqualTo(local.x + LevelCanvasElement.k_toolBadgeOffset).Within(0.01f));
            Assert.That(translate.y.value, Is.EqualTo(local.y + LevelCanvasElement.k_toolBadgeOffset).Within(0.01f));
            LevelCanvasHit hover = canvas.Hover;
            SendPointer(root, EventType.MouseMove, canvas.LocalToWorld(cell.center + new Vector2(3f, -2f)), 0);
            Assert.That(canvas.Hover, Is.EqualTo(hover), "A move inside one cell keeps the hover, so no repaint.");
            Assert.That(badge.style.translate.value.x.value,
                Is.EqualTo(cell.center.x + 3f + LevelCanvasElement.k_toolBadgeOffset).Within(0.01f),
                "The badge still follows the pointer.");
            Assert.That(badge.parent.IndexOf(badge), Is.EqualTo(badge.parent.childCount - 1), "The badge stays above labels.");

            LevelEditorToolState state = GetToolState();
            Assert.That(icon.tintColor, Is.EqualTo(canvas.GetPaletteColor(state.Color)), "The Pen takes the colour.");
            Assert.That(icon.image, Is.Not.Null.And.EqualTo(LevelEditorIcons.Load(LevelEditorIcons.k_pen)),
                "The badge shows the Pen icon.");
            PaletteColor other = state.Color == PaletteColor.Blue ? PaletteColor.DarkGreen : PaletteColor.Blue;
            Click(root, root.Q<LevelColorSwatches>().GetSwatch(other).worldBound.center, 0);
            Assert.That(icon.tintColor, Is.EqualTo(canvas.GetPaletteColor(other)), "A swatch recolours the badge.");

            // The swatch click took the pointer off the canvas; back on it, a tool switch alone updates the badge. With
            // the Pen, 3 picks a colour (TASK-20260926-180414067-b923c8a9), so the Eraser button switches.
            SendPointer(root, EventType.MouseMove, canvas.LocalToWorld(local), 0);
            Press(root.Q<Button>("tool-eraser"));
            Assert.That(icon.image, Is.Not.Null.And.EqualTo(LevelEditorIcons.Load(LevelEditorIcons.k_eraser)),
                "A tool switch updates a visible badge without a pointer move.");
            Assert.That(badge.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Press(root.Q<Button>("flip-block-v"));
            Assert.That(icon.image, Is.Not.Null.And.EqualTo(LevelEditorIcons.Load(LevelEditorIcons.k_reflectVertical)),
                "A picked-up transform tool shows its own icon in the frame.");
            Assert.That(badge.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(icon.tintColor, Is.EqualTo(Color.white), "Transform tools use the light icon in the dark frame.");
            // A real capture loss names its pointer; UI Toolkit indexes its pointer state with that id.
            using (PointerCaptureOutEvent captureOut = PointerCaptureOutEvent.GetPooled(canvas, null, PointerId.mousePointerId))
            {
                canvas.SendEvent(captureOut);
            }

            Assert.That(badge.style.display.value, Is.EqualTo(DisplayStyle.None), "Losing the pointer capture hides it.");
            SendPointer(root, EventType.MouseMove, canvas.LocalToWorld(local), 0);
            Assert.That(icon.tintColor, Is.EqualTo(Color.white), "Other tools use the light icon in the dark frame.");

            using (PointerLeaveEvent leave = PointerLeaveEvent.GetPooled())
            {
                leave.target = canvas;
                canvas.SendEvent(leave);
            }

            Assert.That(badge.style.display.value, Is.EqualTo(DisplayStyle.None), "Leaving the canvas hides the badge.");
            SendPointer(root, EventType.MouseMove, canvas.LocalToWorld(local), 0);
            SendKey(canvas, KeyCode.Alpha1, EventModifiers.None);
            Assert.That(badge.style.display.value, Is.EqualTo(DisplayStyle.None), "Switching to Select hides it at once.");
        }

        [Test]
        public void ToolRowsWrapAtANarrowToolsWidthInsteadOfClipping()
        {
            VisualElement root = OpenWindow();
            VisualElement tools = root.Q<ScrollView>("tools-pane");
            foreach (string rowName in new[] { "canvas-tools", "block-actions" })
            {
                VisualElement row = tools.Q(rowName);
                Assert.That(row.style.flexWrap.value, Is.EqualTo(Wrap.Wrap), rowName);
                foreach (Button button in row.Query<Button>().ToList())
                {
                    Assert.That(button.style.width.value.value, Is.EqualTo(button.style.height.value.value),
                        $"{button.name} is square.");
                    Assert.That(button.style.flexShrink.value, Is.Zero, $"{button.name} keeps its size.");
                }
            }

            // Narrow the row as a tight tools column would: the last button moves to a new line, still inside.
            VisualElement toolRow = tools.Q("canvas-tools");
            toolRow.style.width = 90f;
            ValidateLayout(root);
            Button eraser = toolRow.Q<Button>("tool-eraser");
            Assert.That(eraser.layout.y, Is.GreaterThan(toolRow.Q<Button>("tool-select").layout.y), "The Eraser wraps.");
            Assert.That(eraser.layout.xMax, Is.LessThanOrEqualTo(toolRow.layout.width + 0.5f), "Nothing is clipped.");
        }

        [Test]
        public void TileDeleteAsksFirstThenDeletesThroughTheLibraryTransaction()
        {
            VisualElement root = OpenWindow();
            // A used piece would ask the DEC-126 question and write levels, which this fixture never does.
            m_window.WriteLevels = _ =>
            {
                Assert.Fail("This fixture never writes level JSON.");
                return false;
            };
            m_window.ChoosePieceDelete = _ =>
            {
                Assert.Fail("An unused piece never asks Delete template or Delete template and its blocks.");
                return LevelPieceDeleteChoice.Cancel;
            };

            BlockShapeLibrary projectLibrary = (BlockShapeLibrary)LevelEditorTestAccess.Get(m_window, "m_shapeLibrary");
            LevelShapePalette palette = root.Q<LevelShapePalette>();
            LevelEditorToolState state = GetToolState();
            // A transient library stands in for the project asset, so no real piece is deleted.
            BlockShapeLibrary library = ScriptableObject.CreateInstance<BlockShapeLibrary>();
            // In-memory levels whose only block is a domino stand in for the live ones in the window's own catalog, its
            // level open and clean, so neither piece of the transient library is used, whatever the live levels hold.
            // LoadDatabase sets m_sourceValid and the window's levels, which m_fixture's Dispose drops with the window,
            // so only the other window fields need the finally block's own restore; no level file is read or written.
            string[] standInFields =
            {
                "m_selectedIndex", "m_loadedSource", "m_savedDraftJson"
            };
            object[] liveValues = new object[standInFields.Length];
            for (int index = 0; index < standInFields.Length; index++)
            {
                liveValues[index] = LevelEditorTestAccess.Get(m_window, standInFields[index]);
            }

            try
            {
                LevelData unused = new LevelData("fixture", "Unused pieces", 4, 4, 60, new LevelDoorData[0],
                    new[] { new LevelBlockData("b1", "Red", Vector2Int.zero, s_domino) });
                m_fixture.LoadDatabase(unused);

                library.Load(BlockShapeLibraryJson.Write(new[]
                {
                    new BlockShapeLibrary.Shape("One Cell", new[] { Vector2Int.zero }),
                    new BlockShapeLibrary.Shape("Plus",
                        new[] { Vector2Int.right, Vector2Int.up, Vector2Int.one, new Vector2Int(2, 1), new Vector2Int(1, 2) })
                }));
                LevelEditorTestAccess.Set(m_window, "m_shapeLibrary", library);
                FocusWindow();
                Assert.That(palette.TileCount, Is.EqualTo(2));

                string asked = null;
                m_window.ConfirmPieceDelete = message =>
                {
                    asked = message;
                    return false;
                };
                Press(palette.GetDeleteButton(0));
                Assert.That(asked, Does.Contain("One Cell"), "The dialog names the piece.");
                Assert.That(asked, Does.Contain("No block in this level database uses it."),
                    "The dialog counts the blocks that use the piece.");

                Assert.That(library.Shapes.Count, Is.EqualTo(2), "Cancel deletes nothing.");
                Assert.That(state.HasArmedShape, Is.False, "The × never arms the piece.");

                state.Arm(GetShapeCatalog().Find(LevelTestShapes.Key(new[] { Vector2Int.zero })));
                m_window.ConfirmPieceDelete = _ => true;
                Press(palette.GetDeleteButton(0));

                Assert.That(library.Shapes.Count, Is.EqualTo(1), "Confirm deletes through the library transaction.");
                Assert.That(library.Shapes[0].DisplayName, Is.EqualTo("Plus"));
                Assert.That(palette.TileCount, Is.EqualTo(1), "The palette shows the library as it is now.");
                Assert.That(state.HasArmedShape, Is.False, "The deleted piece is disarmed.");
                Assert.That(root.Q<LevelCanvasElement>().Tool, Is.EqualTo(LevelEditorTool.Select));
                Assert.That(root.Q<Label>("status").text, Is.EqualTo("Deleted One Cell from the piece library."));
                Assert.That(root.panel.focusController.focusedElement, Is.SameAs(root.Q<LevelCanvasElement>()),
                    "The canvas keeps the shortcut keys after the × is rebuilt.");

                // A delete request from a tile of an older catalog finds nothing and deletes nothing.
                LevelEditorTestAccess.Invoke(m_window, "DeleteShape",
                    new object[] { LevelTestShapes.Template("One Cell", new[] { Vector2Int.zero }) });
                Assert.That(library.Shapes.Count, Is.EqualTo(1));
                Assert.That(root.Q<Label>("status").text, Is.EqualTo("That piece is no longer in the piece library."));
            }
            finally
            {
                LevelEditorTestAccess.Set(m_window, "m_shapeLibrary", projectLibrary);
                ShapeLibraryAuthoring.ClearHistory(library);
                Object.DestroyImmediate(library);
                for (int index = 0; index < standInFields.Length; index++)
                {
                    LevelEditorTestAccess.Set(m_window, standInFields[index], liveValues[index]);
                }
            }
        }

        // The first valid and the first refused origin for the armed shape, scanning the board row by row. Only free
        // board cells count: Select places on a free cell and selects a block it is pressed on.
        private static void FindPlacements(LevelData level, LevelEditorToolState state, LevelCanvasModel model,
            out Vector2Int valid, out Vector2Int invalid)
        {
            valid = new Vector2Int(-1, -1);
            invalid = new Vector2Int(-1, -1);
            for (int y = 0; y < level.Height; y++)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    Vector2Int origin = new Vector2Int(x, y);
                    if (!model.IsFree(origin, -1) || model.IsBoardObject(origin))
                    {
                        continue;
                    }

                    bool fits = LevelEditCommands.TryPlaceBlock(level, state.ArmedCells, state.ColorId, origin, out _,
                        out _);
                    if (fits && valid.x < 0)
                    {
                        valid = origin;
                    }
                    else if (!fits && invalid.x < 0)
                    {
                        invalid = origin;
                    }
                }
            }
        }

        // The nearest accepted and refused offsets for one block, closest first.
        private static void FindMoves(LevelData level, int blockIndex, out Vector2Int valid, out Vector2Int invalid,
            out string invalidIssue)
        {
            valid = Vector2Int.zero;
            invalid = Vector2Int.zero;
            invalidIssue = null;
            Vector2Int origin = level.GetBlockAt(blockIndex).Origin;
            for (int distance = 1; distance <= level.Width + level.Height; distance++)
            {
                for (int dx = -distance; dx <= distance; dx++)
                {
                    int remaining = distance - Mathf.Abs(dx);
                    foreach (int dy in new[] { remaining, -remaining })
                    {
                        Vector2Int offset = new Vector2Int(dx, dy);
                        bool moved = LevelEditCommands.TryMoveBlock(level, blockIndex, origin + offset, out _,
                            out string issue);
                        if (moved && valid == Vector2Int.zero)
                        {
                            valid = offset;
                        }
                        else if (!moved && invalid == Vector2Int.zero)
                        {
                            invalid = offset;
                            invalidIssue = issue;
                        }
                    }
                }

                if (valid != Vector2Int.zero && invalid != Vector2Int.zero)
                {
                    return;
                }
            }
        }

        // A 4 x 4 board: a domino at (0,0) with a single on (0,1) above its left cell, and a free domino at (2,2).
        private static readonly Vector2Int[] s_domino = { Vector2Int.zero, Vector2Int.right };

        // Loaded through RequireCanvas as the database's level, so it is also the saved baseline and the window
        // starts clean.
        private static LevelData CreateRotateLevel()
        {
            return new LevelData("fixture", "Rotate fixture", 4, 4, 60, new LevelDoorData[0], new[]
            {
                new LevelBlockData("b1", "Red", Vector2Int.zero, s_domino),
                new LevelBlockData("b2", "Blue", new Vector2Int(0, 1), new[] { Vector2Int.zero }),
                new LevelBlockData("b3", "DarkGreen", new Vector2Int(2, 2), s_domino)
            });
        }

        // A 4 x 4 board: a domino at (2,0), the automatic default selection, and a free corner at (0,2) whose turns
        // and mirrors all stay on the board and change its cells.
        private static readonly Vector2Int[] s_corner = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };

        private void LoadCornerFixture(VisualElement root)
        {
            LoadFixture(root, new LevelData("fixture", "Corner fixture", 4, 4, 60, new LevelDoorData[0], new[]
            {
                new LevelBlockData("b1", "Red", new Vector2Int(2, 0), s_domino),
                new LevelBlockData("b2", "Blue", new Vector2Int(0, 2), s_corner)
            }));
        }

        private void LoadFixture(VisualElement root, LevelData level)
        {
            m_fixture.LoadFixture(root, level);
        }

        // Submit presses a button the way a click does, wherever the tools view is scrolled.
        private static void Press(Button button)
        {
            UiTestInput.Press(button);
        }

        private LevelData GetDraft()
        {
            return m_fixture.GetDraft();
        }

        private LevelEditorToolState GetToolState()
        {
            return (LevelEditorToolState)LevelEditorTestAccess.Get(m_window, "m_toolState");
        }

        private LevelShapeCatalog GetShapeCatalog()
        {
            return (LevelShapeCatalog)LevelEditorTestAccess.Get(m_window, "m_shapeCatalog");
        }

        // The window checks the piece library again when it regains focus, as after work in the Piece Editor.
        private void FocusWindow()
        {
            LevelEditorTestAccess.Invoke(m_window, "OnFocus", null);
        }

        private static void ClickCell(VisualElement root, LevelCanvasElement canvas, Vector2Int cell, int button)
        {
            Click(root, canvas.LocalToWorld(canvas.Geometry.GetCellRect(cell).center), button);
        }

        private static void Drag(VisualElement root, LevelCanvasElement canvas, Vector2Int from, Vector2Int to)
        {
            Vector2 start = canvas.LocalToWorld(canvas.Geometry.GetCellRect(from).center);
            Vector2 end = canvas.LocalToWorld(canvas.Geometry.GetCellRect(to).center);
            UiTestInput.SendPointer(root, EventType.MouseDown, start, 0);
            UiTestInput.SendPointer(root, EventType.MouseDrag, end, 0);
            UiTestInput.SendPointer(root, EventType.MouseUp, end, 0);
        }

        private static void Click(VisualElement root, Vector2 position, int button,
            EventModifiers modifiers = EventModifiers.None)
        {
            UiTestInput.Click(root, position, button, modifiers);
        }

        private static void SendPointer(VisualElement root, EventType type, Vector2 position, int button,
            EventModifiers modifiers = EventModifiers.None)
        {
            UiTestInput.SendPointer(root, type, position, button, modifiers);
        }

        // Sends a key press to one element after focusing it: UI Toolkit routes keyboard events to the focused
        // element, so a key with nothing focused never reaches the window's shortcut handler.
        private static void SendKey(VisualElement target, KeyCode key, EventModifiers modifiers)
        {
            UiTestInput.SendKey(target, key, modifiers);
        }

        private VisualElement OpenWindow()
        {
            m_fixture = new LevelEditorWindowFixture();
            // A tile click never drives a Piece Editor the designer has open in this Editor.
            VisualElement root = m_fixture.Open(isPieceEditorOpen: () => false);
            m_window = m_fixture.Window;
            return root;
        }

        // A fixture database load guarantees a loaded level with valid canvas geometry, whatever the real
        // project's active database holds (Tests-Editor-1-F3). The loaded level is also the saved baseline, so a
        // caller that checks the dirty state passes its content here (CreateRotateLevel); LoadFixture replaces only
        // the draft, which leaves the window dirty against the placeholder baseline.
        private LevelCanvasElement RequireCanvas(VisualElement root, params LevelData[] levels)
        {
            m_fixture.LoadDatabase(levels);
            // A fresh undo group after the fixture load, so the test's own first edit starts a clean group.
            Undo.IncrementCurrentGroup();
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(IsLevelLoaded(root) && canvas != null && canvas.Geometry.IsValid, Is.True,
                "The fixture database has a loaded level.");
            return canvas;
        }

        // Unity builds this window's GUI itself here: a manual CreateGUI could be followed by Unity's own
        // call, which rebuilds the draft and clears its undo records between an edit and its undo.
        private IEnumerator ShowWindowBuiltByUnity()
        {
            m_fixture = new LevelEditorWindowFixture();
            yield return m_fixture.ShowBuiltByUnity(isPieceEditorOpen: () => false);
            m_window = m_fixture.Window;
        }

        private static void ValidateLayout(VisualElement root)
        {
            UiTestInput.ValidateLayout(root);
        }

        private static bool IsLevelLoaded(VisualElement root)
        {
            return LevelEditorWindowFixture.IsLevelLoaded(root);
        }

        private static IEnumerator WaitFor(double seconds)
        {
            double end = EditorApplication.timeSinceStartup + seconds;
            while (EditorApplication.timeSinceStartup < end)
            {
                yield return null;
            }
        }
    }
}
