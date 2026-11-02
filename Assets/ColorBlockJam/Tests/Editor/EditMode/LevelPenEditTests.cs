using System;
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
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor's Pen edit and template edits (TASK-20260926-140520462-2196c1d1, DEC-125): a Pen click
    /// starts editing a block, a swatch recolours only that block, a template stroke changes every block of the
    /// template in the level as one undo step and keeps the pending footprint in the draft, the pending footprint is the
    /// open level's catalog until Discard, which puts every block back where it was, Make a separate piece keeps the
    /// change for the edited block only, a click on empty space ends the edit and clears the selection, and Save Level
    /// writes the other
    /// levels and commits the library through the window's seams; a failed commit retries without asking again, can be
    /// given up with Keep library footprint, is dropped when no level holds it and keeps the close prompt; the tool
    /// info box follows the tool. Every seam is a fake, the library and the level database are in-memory copies, and
    /// SHA-256 guards prove no live piece or level file is written.
    /// </summary>
    public sealed class LevelPenEditTests
    {
        private const BindingFlags k_members = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string k_templateName = "Pen fixture L";
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_domino = { Vector2Int.zero, Vector2Int.right };
        private static readonly Vector2Int[] s_corner = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };
        // A column of three with a foot to the right at the top; its 8 rotations and mirrors all differ.
        private static readonly Vector2Int[] s_libraryL =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2)
        };
        // The L with (1,1) added inside its bounding box: a P pentomino.
        private static readonly Vector2Int[] s_grownP =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(1, 1)
        };
        // The L with (0,3) added on top of its column.
        private static readonly Vector2Int[] s_tallL =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(0, 3)
        };
        // The template's library key when every test starts; declared after the cells, so the initializer reads them.
        private static readonly ShapeKey s_templateKey = LevelTestShapes.Key(s_libraryL);

        private LevelEditorWindowFixture m_fixture;
        private LevelDatabaseWindow m_window;
        private BlockShapeLibrary m_library;
        // True once UseDatabase loaded the test's levels into the window; the fake write refuses before that.
        private bool m_usesDatabase;
        private LevelData m_previousPublished;
        private string[] m_liveFiles;
        private string[] m_liveHashes;
        private readonly List<List<LevelData>> m_writes = new List<List<LevelData>>();
        private readonly List<List<LevelTemplateEdit>> m_commits = new List<List<LevelTemplateEdit>>();
        private readonly List<string> m_confirms = new List<string>();
        private readonly List<string> m_deleteQuestions = new List<string>();
        private string m_commitIssue;
        private bool m_commitWritesLibrary;
        private bool m_confirmAnswer;

        [SetUp]
        public void RememberLiveFiles()
        {
            m_previousPublished = LevelDatabaseWindow.LastPublishedLevel;
            m_writes.Clear();
            m_commits.Clear();
            m_confirms.Clear();
            m_deleteQuestions.Clear();
            m_commitIssue = null;
            m_commitWritesLibrary = false;
            m_confirmAnswer = false;
            m_liveFiles = GetLiveFiles();
            m_liveHashes = Hash(m_liveFiles);
        }

        [TearDown]
        public void CloseWindow()
        {
            if (m_window != null)
            {
                // A test never saves for real, so drop any pending edit instead of opening the Save/Discard prompt.
                m_window.DiscardChanges();
                m_window.Close();
                m_window = null;
            }

            // This file closes the window itself (its own fake seams need CreateWindow), so the fixture only goes.
            m_fixture = null;
            m_usesDatabase = false;

            if (m_library != null)
            {
                ShapeLibraryAuthoring.ClearHistory(m_library);
                Object.DestroyImmediate(m_library);
                m_library = null;
            }

            RestorePublishedLevel(m_previousPublished);
            Assert.That(Hash(m_liveFiles), Is.EqualTo(m_liveHashes),
                "No test writes the live piece JSON, library asset or level JSON.");
        }

        [Test]
        public void OnlyAPenClickOnABlockStartsAnEdit()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = RequireCanvas(root);
            UseLibrary();
            LoadFixture(root, Level("pen-fixture", "Pen fixture", 8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL),
                new LevelBlockData("b2", "Blue", new Vector2Int(5, 5), s_single)));
            LevelShapePalette palette = root.Q<LevelShapePalette>();
            LevelEditorToolState state = GetToolState();

            SendKey(canvas, KeyCode.Alpha2);
            Assert.That(canvas.IsFreehand, Is.False, "The automatic default selection is not edited.");
            Assert.That(root.Q("template-banner").style.display.value, Is.EqualTo(DisplayStyle.None));

            // A just-placed block is not a choice either.
            state.Arm(GetEditCatalog().FindByName("Single"));
            Invoke("SyncTool");
            Invoke("PlaceBlock", new Vector2Int(6, 0));
            Assume.That(GetDraft().BlockCount, Is.EqualTo(3));
            SendKey(canvas, KeyCode.Alpha2);
            Assert.That(canvas.IsFreehand, Is.False, "A just-placed block is not edited.");

            ClickCell(root, canvas, new Vector2Int(5, 5));
            Assert.That(canvas.IsFreehand, Is.True, "A Pen click on a block starts editing it.");
            Assert.That(canvas.FreehandBlockId, Is.EqualTo("b2"));
            Assert.That(canvas.Selection.Index, Is.EqualTo(1), "The block is selected.");
            Assert.That(state.HasArmedShape, Is.False, "Nothing is armed.");
            Assert.That(GetTile(palette, "Single").style.borderTopColor.value, Is.EqualTo(LevelShapePalette.s_editBorder),
                "Its piece's tile has the green edit outline.");
            Assert.That(root.Q<Label>("status").text, Does.Contain("a block of template Single"));
            Assert.That(root.Q<Label>("template-banner-text").text,
                Does.StartWith("Editing template Single: a change reaches all 2 blocks here"));

            ClickCell(root, canvas, new Vector2Int(1, 1));
            Assert.That(canvas.FreehandBlockId, Is.EqualTo("b1"), "A Pen click on another block switches the edit.");
            Assert.That(GetTile(palette, k_templateName).style.borderTopColor.value, Is.EqualTo(LevelShapePalette.s_editBorder));
            Assert.That(GetTile(palette, "Single").style.borderTopColor.value, Is.Not.EqualTo(LevelShapePalette.s_editBorder));

            SendKey(canvas, KeyCode.Escape);
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Select));
            Assert.That(canvas.IsFreehand, Is.False, "Esc ends the edit.");
            Assert.That(GetTile(palette, k_templateName).style.borderTopColor.value, Is.Not.EqualTo(LevelShapePalette.s_editBorder));

            // Picking up the Pen starts no edit, whatever was chosen before (user revision of 2026-09-26).
            SendKey(canvas, KeyCode.Alpha2);
            Assert.That(canvas.IsFreehand, Is.False, "Esc ended the edit, and picking up the Pen again starts none.");
            // With the Pen, 1, 2 and 3 pick colours (TASK-20260926-180414067-b923c8a9), so tool buttons switch from it.
            Press(root.Q<Button>("tool-select"));
            ClickCell(root, canvas, new Vector2Int(5, 5));
            Assume.That(canvas.Selection.Index, Is.EqualTo(1));
            SendKey(canvas, KeyCode.Alpha2);
            Assert.That(canvas.IsFreehand, Is.False, "A block chosen with Select is not edited.");
            string chosen = JsonUtility.ToJson(GetDraft());
            Invoke("PickSwatch", PaletteColor.Purple);
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(chosen), "So a swatch recolours nothing.");
            Assert.That(GetTile(palette, "Single").style.borderTopColor.value,
                Is.Not.EqualTo(LevelShapePalette.s_editBorder));
            Invoke("SetTool", LevelEditorTool.RotateClockwise);
            Invoke("TransformClickedBlock", 1, LevelEditorTool.RotateClockwise);
            SendKey(canvas, KeyCode.Alpha2);
            Assert.That(canvas.IsFreehand, Is.False, "Nor a block chosen with a rotate or mirror tool.");

            // A Selected Block choice under the Pen ends an edit and starts none.
            ClickCell(root, canvas, new Vector2Int(5, 5));
            Assume.That(canvas.FreehandBlockId, Is.EqualTo("b2"));
            GetField<DropdownField>("m_blockChoiceField").value = "1. b1";
            Assert.That(canvas.IsFreehand, Is.False, "A dropdown choice is no Pen click.");
            Assert.That(canvas.Selection.Index, Is.Zero);

            ClickCell(root, canvas, new Vector2Int(5, 5));
            Press(root.Q<Button>("tool-eraser"));
            Assert.That(canvas.IsFreehand, Is.False, "The Eraser ends the edit.");
            SendKey(canvas, KeyCode.Alpha2);
            ClickCell(root, canvas, new Vector2Int(5, 5));
            Assume.That(canvas.FreehandBlockId, Is.EqualTo("b2"));
            Invoke("ArmObstacleBarrier");
            Assert.That(canvas.IsFreehand, Is.False, "The Barrier brush ends the edit.");

            // "+" New piece ends it too, and a Pen click on a block while "+" is armed leaves "+" for that block.
            Press(root.Q<Button>("tool-pen"));
            Invoke("CreateNamedPiece");
            Assert.That(state.IsFreehandArmed, Is.True);
            Assert.That(canvas.FreehandBlockId, Is.Null, "The new piece has no block yet.");
            ClickCell(root, canvas, new Vector2Int(1, 1));
            Assert.That(state.IsFreehandArmed, Is.False, "The Pen click leaves \"+\".");
            Assert.That(canvas.FreehandBlockId, Is.EqualTo("b1"));
            Assert.That(GetDraft().BlockCount, Is.EqualTo(3), "Starting an edit changes no block.");
        }

        [UnityTest]
        public IEnumerator ASwatchRecoloursOnlyTheBlockThePenEditsAsOneUndoStep()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = RequireCanvas(root);
            UseLibrary();
            LoadFixture(root, Level("pen-fixture", "Pen fixture", 8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL),
                new LevelBlockData("b2", "Blue", new Vector2Int(5, 1), Turned(s_libraryL))));
            string fixture = JsonUtility.ToJson(GetDraft());
            SendKey(canvas, KeyCode.Alpha2);

            Invoke("PickSwatch", PaletteColor.DarkGreen);
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixture), "Without a Pen click nothing is recoloured.");
            Assert.That(GetToolState().Color, Is.EqualTo(PaletteColor.DarkGreen), "The tool colour changes.");

            ClickCell(root, canvas, new Vector2Int(6, 2));
            Assume.That(canvas.FreehandBlockId, Is.EqualTo("b2"));
            Invoke("PickSwatch", PaletteColor.Purple);
            Assert.That(GetDraft().GetBlockAt(1).ColorId, Is.EqualTo(PaletteColor.Purple.ToString()));
            Assert.That(GetDraft().GetBlockAt(0).ColorId, Is.EqualTo("Red"), "Colour is per block, not per template.");
            SendKey(canvas, KeyCode.RightBracket);
            Assert.That(GetDraft().GetBlockAt(1).ColorId,
                Is.EqualTo(((PaletteColor)((int)PaletteColor.Purple + 1)).ToString()), "] recolours it too.");

            Undo.PerformUndo();
            yield return null;
            Assert.That(GetDraft().GetBlockAt(1).ColorId, Is.EqualTo(PaletteColor.Purple.ToString()), "One step each.");
            Undo.PerformUndo();
            yield return null;
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixture));
        }

        [UnityTest]
        public IEnumerator ATemplateStrokeChangesEveryBlockOfTheTemplateAndItsFootprintAsOneUndoStep()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            RequireCanvas(root);
            UseLibrary();
            LoadFixture(root, Level("pen-fixture", "Pen fixture", 8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL),
                new LevelBlockData("b2", "Blue", new Vector2Int(5, 1), Turned(s_libraryL)),
                new LevelBlockData("b3", "Pink", new Vector2Int(7, 7), s_single)));
            string fixture = JsonUtility.ToJson(GetDraft());
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);

            Draw(new Vector2Int(2, 2));

            LevelData grown = GetDraft();
            Assert.That(Cells(grown.GetBlockAt(0)), Is.EquivalentTo(s_grownP));
            Assert.That(grown.GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(Cells(grown.GetBlockAt(1)), Is.EquivalentTo(Turned(s_grownP)), "b2 follows in its own turn.");
            Assert.That(grown.GetBlockAt(1).Origin, Is.EqualTo(new Vector2Int(5, 1)));
            Assert.That(Cells(grown.GetBlockAt(2)), Is.EqualTo(s_single), "Other pieces are untouched.");
            LevelTemplateEdit edit = GetDraftEdit();
            Assert.That(edit, Is.Not.Null);
            Assert.That(edit.BaseCells, Is.EquivalentTo(s_libraryL));
            Assert.That(edit.Cells, Is.EquivalentTo(s_grownP), "The footprint is stored in library orientation.");
            Assert.That(root.Q<Label>("status").text, Does.Contain("Every Pen fixture L block in this level follows"));
            Assert.That(m_window.hasUnsavedChanges, Is.True);

            Undo.PerformUndo();
            yield return null;
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixture), "One undo restores every block.");
            Assert.That(GetDraftEdit(), Is.Null, "And drops the pending footprint with them.");
            Undo.PerformRedo();
            yield return null;
            Assert.That(Cells(GetDraft().GetBlockAt(1)), Is.EquivalentTo(Turned(s_grownP)));
            Assert.That(GetDraftEdit()?.Cells, Is.EquivalentTo(s_grownP), "Redo brings both back.");

            // Taking the cell off again is the library footprint: the pending edit is dropped.
            Invoke("RemovePieceCell", new Vector2Int(2, 2));
            Assert.That(Cells(GetDraft().GetBlockAt(1)), Is.EquivalentTo(Turned(s_libraryL)));
            Assert.That(GetDraftEdit(), Is.Null);
        }

        [Test]
        public void ATemplateStrokeThatAnyBlockCannotTakeChangesNothingAndNamesTheBlock()
        {
            VisualElement root = OpenWindow();
            RequireCanvas(root);
            UseLibrary();
            // b2's column has no room for a fourth cell on the 6 x 6 board.
            LoadFixture(root, Level("pen-fixture", "Pen fixture", 6, 6,
                new LevelBlockData("b1", "Red", Vector2Int.zero, s_libraryL),
                new LevelBlockData("b2", "Blue", new Vector2Int(4, 3), s_libraryL)));
            string fixture = JsonUtility.ToJson(GetDraft());
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);

            Draw(new Vector2Int(0, 3));

            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixture), "Refused as a whole.");
            Assert.That(root.Q<Label>("status").text, Does.Contain("b2").And.Contain("off the 6 x 6 board"));
            Assert.That(GetDraftEdit(), Is.Null);
        }

        [UnityTest]
        public IEnumerator APendingFootprintIsTheOpenLevelsCatalogUntilItIsDiscarded()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            RequireCanvas(root);
            UseLibrary();
            UseDatabase(root, Level("pen-fixture", "Pen fixture", 8, 8,
                    new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL),
                    new LevelBlockData("b2", "Blue", new Vector2Int(5, 1), Turned(s_libraryL)),
                    new LevelBlockData("b3", "Pink", new Vector2Int(5, 5), s_single)),
                Level("pen-other-a", "Other A", 8, 8,
                    new LevelBlockData("b1", "Red", new Vector2Int(3, 3), Turned(s_libraryL))),
                Level("pen-other-b", "Other B", 8, 8,
                    new LevelBlockData("b1", "Red", Vector2Int.zero, s_single)));
            string fixture = JsonUtility.ToJson(GetDraft());
            Label banner = root.Q<Label>("template-banner-text");
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);
            Assert.That(banner.text, Is.EqualTo("Editing template Pen fixture L: a change reaches all 2 blocks here and " +
                                                "1 in 1 other levels of Main when you Save Level, and the " +
                                                "piece library."));

            Draw(new Vector2Int(2, 2));

            Assert.That(banner.text, Does.StartWith("Template Pen fixture L changed: Save Level writes the 2 block(s) " +
                                                    "here, re-stamps 1 in 1 other levels of Main"));
            Assert.That(root.Q<Button>("template-banner-discard").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Button>("shape-banner-restamp").style.display.value, Is.EqualTo(DisplayStyle.None),
                "No Re-stamp is offered for the pending footprint.");
            Assert.That(FindShapeIssue(root, "b1"), Is.Null, "No rotation-or-mirror warning either.");
            LevelShapePalette palette = root.Q<LevelShapePalette>();
            Assert.That(GetChoice(palette, k_templateName).Cells, Is.EquivalentTo(s_grownP), "The tile shows the new footprint.");

            // Piece Type and Select placement use the pending footprint.
            Invoke("SelectOnCanvas", LevelCanvasSelection.ForBlock(2, "b3"));
            GetField<DropdownField>("m_pieceTypeField").value = k_templateName;
            Assert.That(Cells(GetDraft().GetBlockAt(2)), Is.EquivalentTo(s_grownP));
            Invoke("ArmShape", GetChoice(palette, k_templateName));
            Assert.That(GetToolState().ArmedCells, Is.EquivalentTo(s_grownP));
            Invoke("PlaceBlock", new Vector2Int(2, 5));
            Assert.That(Cells(GetDraft().GetBlockAt(3)), Is.EquivalentTo(s_grownP));

            // Deleting the template from the palette is refused while its footprint is pending.
            m_window.ConfirmPieceDelete = message =>
            {
                m_deleteQuestions.Add(message);
                return true;
            };
            Invoke("DeleteShape", GetChoice(palette, k_templateName));
            Assert.That(m_deleteQuestions, Is.Empty, "Refused before any question.");
            Assert.That(root.Q<Label>("status").text, Does.Contain("has a template change waiting for Save Level"));
            Assert.That(m_library.Shapes.Count, Is.EqualTo(3));

            // Undoing the edit disarms the template, whose footprint is the library's again.
            Undo.PerformUndo();
            Undo.PerformUndo();
            Undo.PerformUndo();
            yield return null;
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixture));
            Assert.That(GetToolState().HasArmedShape, Is.False, "An outdated footprint is never placed.");
            Assert.That(GetChoice(palette, k_templateName).Cells, Is.EquivalentTo(s_libraryL));
            Assert.That(root.Q("template-banner").style.display.value, Is.EqualTo(DisplayStyle.None));

            // Discard restores the library footprint as one undo step.
            Undo.PerformRedo();
            yield return null;
            Assume.That(GetDraftEdit(), Is.Not.Null);
            Press(root.Q<Button>("template-banner-discard"));
            Assert.That(Cells(GetDraft().GetBlockAt(0)), Is.EquivalentTo(s_libraryL));
            Assert.That(Cells(GetDraft().GetBlockAt(1)), Is.EquivalentTo(Turned(s_libraryL)));
            Assert.That(GetDraft().GetBlockAt(1).Origin, Is.EqualTo(new Vector2Int(5, 1)));
            Assert.That(GetDraftEdit(), Is.Null);
            Undo.PerformUndo();
            yield return null;
            Assert.That(Cells(GetDraft().GetBlockAt(1)), Is.EquivalentTo(Turned(s_grownP)), "One undo step.");
            Assert.That(GetDraftEdit()?.Cells, Is.EquivalentTo(s_grownP));
            Assert.That(m_writes, Is.Empty);
            Assert.That(m_commits, Is.Empty);
        }

        [UnityTest]
        public IEnumerator DiscardPutsEveryBlockBackWhereItWasAfterTheEditGrewPastTheBlocksCorner()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            RequireCanvas(root);
            UseLibrary();
            // D-001: a cell left of b1 moves its box onto b2's column, so a re-stamp at that corner covered b2.
            LoadFixture(root, Level("pen-fixture", "Pen fixture", 8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(2, 1), s_libraryL),
                new LevelBlockData("b2", "Blue", new Vector2Int(1, 1), s_single),
                new LevelBlockData("b3", "Pink", new Vector2Int(5, 4), Turned(s_libraryL))));
            string fixture = JsonUtility.ToJson(GetDraft());
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);
            Draw(new Vector2Int(1, 2));
            Assume.That(GetDraft().GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(1, 1)));
            Assume.That(GetDraftEdit(), Is.Not.Null);

            Press(root.Q<Button>("template-banner-discard"));

            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(fixture), "Every block is back as it was.");
            Assert.That(LevelBlockOverlap.Describe(GetDraft()), Is.Null, "No block covers another.");
            Assert.That(GetDraftEdit(), Is.Null);
            Assert.That(root.Q<Label>("status").text, Is.EqualTo("Discarded the change to Pen fixture L. Undo brings it back."));
            Undo.PerformUndo();
            yield return null;
            Assert.That(GetDraft().GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(1, 1)), "One undo step.");
            Assert.That(GetDraftEdit(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator MakeASeparatePieceKeepsTheChangeForTheEditedBlockOnlyAsOneUndoStep()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = RequireCanvas(root);
            UseLibrary();
            LoadFixture(root, Level("pen-fixture", "Pen fixture", 8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(2, 1), s_libraryL),
                new LevelBlockData("b2", "Blue", new Vector2Int(5, 4), Turned(s_libraryL))));
            Button separate = root.Q<Button>("template-banner-separate");
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);
            Assert.That(separate.style.display.value, Is.EqualTo(DisplayStyle.None), "No change to keep yet.");
            Draw(new Vector2Int(1, 2));
            LevelData grown = GetDraft();
            Assume.That(GetDraftEdit(), Is.Not.Null);
            Assert.That(separate.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            Press(separate);

            LevelData result = GetDraft();
            LevelBlockData kept = result.GetBlockAt(0);
            Assert.That(kept.Origin, Is.EqualTo(grown.GetBlockAt(0).Origin), "It keeps the change.");
            Assert.That(Cells(kept), Is.EqualTo(Cells(grown.GetBlockAt(0))));
            Assert.That(kept.ColorId, Is.EqualTo("Red"));
            Assert.That(result.GetBlockAt(1).Origin, Is.EqualTo(new Vector2Int(5, 4)));
            Assert.That(Cells(result.GetBlockAt(1)), Is.EqualTo(Turned(s_libraryL)), "And is back as it was.");
            Assert.That(GetDraftEdit(), Is.Null, "The template change is dropped.");
            Assert.That(canvas.FreehandBlockId, Is.EqualTo("b1"), "The Pen keeps editing b1.");
            Assert.That(root.Q("template-banner").style.display.value, Is.EqualTo(DisplayStyle.None),
                "No template change is left, so the banner and its buttons are gone.");
            Assert.That(root.Q<Label>("status").text, Does.StartWith("b1 is now its own piece Pen fixture Piece 1;"));
            Assert.That(m_library.Shapes.Count, Is.EqualTo(3), "The piece library does not change.");
            Assert.That(m_commits, Is.Empty);

            Undo.PerformUndo();
            yield return null;
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(JsonUtility.ToJson(grown)),
                "One undo brings the template change back.");
            Assert.That(GetDraftEdit(), Is.Not.Null);
        }

        [Test]
        public void AClickOnEmptySpaceEndsThePenEditAndClearsTheSelectionUntilTheNextPick()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = RequireCanvas(root);
            UseLibrary();
            LoadFixture(root, Level("pen-fixture", "Pen fixture", 8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(2, 2), s_single),
                new LevelBlockData("b2", "Blue", new Vector2Int(5, 5), s_single)));
            SendKey(canvas, KeyCode.Alpha2);
            ClickCell(root, canvas, new Vector2Int(2, 2));
            Assume.That(canvas.FreehandBlockId, Is.EqualTo("b1"));
            Assume.That(canvas.IsPenEditing, Is.True);

            // A free cell that is not green.
            ClickCell(root, canvas, new Vector2Int(6, 1));

            Assert.That(canvas.IsFreehand, Is.False, "The edit ends.");
            Assert.That(canvas.Selection.Kind, Is.EqualTo(LevelCanvasSelectionKind.None), "Nothing is selected.");
            Assert.That(root.Q<Label>("status").text, Is.EqualTo("Editing ended; nothing is selected."));
            string before = JsonUtility.ToJson(GetDraft());
            Invoke("PickSwatch", PaletteColor.Purple);
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before), "So a swatch recolours no block.");
            Invoke("OnDraftChanged");
            Assert.That(canvas.Selection.Kind, Is.EqualTo(LevelCanvasSelectionKind.None),
                "The first block does not come back as a default selection.");

            // A green cell still adds itself; a right click on empty space ends the edit too.
            ClickCell(root, canvas, new Vector2Int(2, 2));
            Assume.That(canvas.FreehandBlockId, Is.EqualTo("b1"));
            ClickCell(root, canvas, new Vector2Int(3, 2));
            Assert.That(GetDraft().GetBlockAt(0).CellCount, Is.EqualTo(2), "A green cell grows the edited block.");
            Assert.That(canvas.IsFreehand, Is.True);
            ClickCell(root, canvas, new Vector2Int(6, 1), 1);
            Assert.That(canvas.IsFreehand, Is.False, "A right click on empty space ends the edit.");

            // Select clears its selection the same way. With the Pen, 1 picks a colour
            // (TASK-20260926-180414067-b923c8a9), so the Select button switches.
            Press(root.Q<Button>("tool-select"));
            ClickCell(root, canvas, new Vector2Int(5, 5));
            Assume.That(canvas.Selection.Index, Is.EqualTo(1));
            ClickCell(root, canvas, new Vector2Int(0, 7));
            Assert.That(canvas.Selection.Kind, Is.EqualTo(LevelCanvasSelectionKind.None));
            Assert.That(GetField<DropdownField>("m_blockChoiceField").value, Is.EqualTo("Nothing selected"));

            // A "+" New piece drawing keeps its rule: a free cell away from the piece is refused, not an exit.
            SendKey(canvas, KeyCode.Alpha2);
            Invoke("CreateNamedPiece");
            ClickCell(root, canvas, new Vector2Int(0, 0));
            Assume.That(GetDraft().BlockCount, Is.EqualTo(3));
            ClickCell(root, canvas, new Vector2Int(6, 1));
            Assert.That(GetToolState().IsFreehandArmed, Is.True);
            Assert.That(root.Q<HelpBox>(LevelDatabaseWindow.k_hintBoxName).text, Does.StartWith("Click next to"));
        }

        [Test]
        public void TakingTheLastCellOffATemplateBlockRemovesOnlyThatBlock()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = RequireCanvas(root);
            UseLibrary();
            LoadFixture(root, Level("pen-fixture", "Pen fixture", 6, 6,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_domino),
                new LevelBlockData("b2", "Blue", new Vector2Int(1, 4), s_domino)));
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);

            Invoke("RemovePieceCell", new Vector2Int(2, 1));
            Assert.That(Cells(GetDraft().GetBlockAt(1)), Is.EquivalentTo(s_single), "b2 follows.");
            Invoke("RemovePieceCell", new Vector2Int(1, 1));

            Assert.That(GetDraft().BlockCount, Is.EqualTo(1));
            Assert.That(GetDraft().GetBlockAt(0).Id, Is.EqualTo("b2"));
            Assert.That(root.Q<Label>("status").text, Is.EqualTo("Removed block b1."));
            Assert.That(GetDraftEdit(LevelTestShapes.Key(s_domino))?.Cells, Is.EquivalentTo(s_single),
                "The pending edit stays.");
            Assert.That(canvas.IsFreehand, Is.False, "The edited block is gone, so the edit ends.");
        }

        [Test]
        public void SaveLevelWritesTheReStampedLevelsOnceAndCommitsEveryTemplateOnce()
        {
            VisualElement root = OpenWindow();
            RequireCanvas(root);
            UseLibrary();
            LevelData otherB = Level("pen-other-b", "Other B", 8, 8,
                new LevelBlockData("b1", "Red", Vector2Int.zero, s_single));
            UseDatabase(root, Level("pen-fixture", "Pen fixture", 8, 8,
                    new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL),
                    new LevelBlockData("b2", "Blue", new Vector2Int(5, 1), Turned(s_libraryL))),
                Level("pen-other-a", "Other A", 8, 8,
                    new LevelBlockData("b1", "Red", new Vector2Int(3, 3), Turned(s_libraryL))),
                otherB);
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);
            Draw(new Vector2Int(2, 2));

            Assert.That(Save(), Is.True);

            Assert.That(m_writes, Has.Count.EqualTo(1), "One database write.");
            List<LevelData> written = m_writes[0];
            Assert.That(Cells(written[0].GetBlockAt(1)), Is.EquivalentTo(Turned(s_grownP)), "The open level.");
            Assert.That(Cells(written[1].GetBlockAt(0)), Is.EquivalentTo(Turned(s_grownP)),
                "Another level's block takes the footprint in its own turn.");
            Assert.That(written[1].GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(3, 3)));
            Assert.That(DescribeBlocks(written[2]), Is.EqualTo(DescribeBlocks(otherB)), "Untouched.");
            Assert.That(m_commits, Has.Count.EqualTo(1), "One library commit.");
            Assert.That(m_commits[0], Has.Count.EqualTo(1));
            Assert.That(m_commits[0][0].TemplateKey, Is.EqualTo(s_templateKey));
            Assert.That(m_commits[0][0].Cells, Is.EquivalentTo(s_grownP));
            Assert.That(m_confirms, Is.Empty);
            Assert.That(GetDraftEdit(), Is.Null, "The saved footprint left the draft.");
            LevelTemplateState state = GetSession().FindTemplate(GetLoadedGuid(), s_templateKey);
            Assert.That(state.IsPending, Is.False, "Committed.");
            Assert.That(state.CommittedCells, Is.EquivalentTo(s_grownP));
            string status = root.Q<Label>("status").text;
            Assert.That(status, Does.StartWith("Saved Pen fixture to Main."));
            Assert.That(status, Does.Contain("Re-stamped Other A (1 block(s))."));
            Assert.That(status, Does.Contain("Wrote the new footprint of Pen fixture L to the piece library."));
            Assert.That(m_window.hasUnsavedChanges, Is.False);
        }

        [Test]
        public void AFailedTemplateCommitStaysPendingAndARetryReStampsNothingTwice()
        {
            VisualElement root = OpenWindow();
            RequireCanvas(root);
            UseLibrary();
            UseDatabase(root, Level("pen-fixture", "Pen fixture", 8, 8,
                    new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL)),
                Level("pen-other-a", "Other A", 8, 8,
                    new LevelBlockData("b1", "Red", new Vector2Int(3, 3), Turned(s_libraryL))));
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);
            Draw(new Vector2Int(2, 2));
            m_commitIssue = "The library is read-only.";

            Assert.That(Save(), Is.True, "The levels are saved even when the library commit fails.");

            Assert.That(m_writes, Has.Count.EqualTo(1));
            Assert.That(m_commits, Has.Count.EqualTo(1));
            LevelTemplateState state = GetSession().FindTemplate(GetLoadedGuid(), s_templateKey);
            Assert.That(state.IsPending, Is.True, "Kept for a retry.");
            Assert.That(state.Issue, Is.EqualTo("The library is read-only."));
            Assert.That(root.Q<Label>("status").text, Does.Contain(
                "Pen fixture L stayed out of the piece library: The library is read-only. Save Level again to retry."));
            Assert.That(m_window.hasUnsavedChanges, Is.True, "Closing asks Save/Discard/Cancel.");
            Assert.That(m_window.saveChangesMessage, Does.Contain("1 template footprint(s) are not in the piece library"));
            Assert.That(root.Q<Label>("template-banner-text").text, Does.Contain(
                "Pen fixture L is in the saved levels but not in the piece library yet: The library is read-only."));
            Assert.That(FindShapeIssue(root, "b1"), Is.Null, "The saved footprint is still the open level's catalog.");

            m_commitIssue = null;
            Assert.That(Save(), Is.True);

            Assert.That(m_writes, Has.Count.EqualTo(2));
            Assert.That(DescribeBlocks(m_writes[1][1]), Is.EqualTo(DescribeBlocks(m_writes[0][1])),
                "The other level already has the footprint, so the retry re-stamps nothing twice.");
            Assert.That(root.Q<Label>("status").text, Does.Not.Contain("Re-stamped"));
            Assert.That(m_commits, Has.Count.EqualTo(2));
            Assert.That(m_commits[1][0].Cells, Is.EquivalentTo(s_grownP));
            Assert.That(state.IsPending, Is.False);
            Assert.That(m_window.hasUnsavedChanges, Is.False);
        }

        [Test]
        public void OtherLevelsThatCannotTakeTheFootprintAreAskedAboutAndLeftUnchanged()
        {
            VisualElement root = OpenWindow();
            RequireCanvas(root);
            UseLibrary();
            // Other A's column has no room for a fourth cell on the 8 x 8 board; Other B's has.
            LevelData otherA = Level("pen-other-a", "Other A", 8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(5, 5), s_libraryL));
            UseDatabase(root, Level("pen-fixture", "Pen fixture", 8, 8,
                    new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL)),
                otherA,
                Level("pen-other-b", "Other B", 8, 8,
                    new LevelBlockData("b1", "Red", Vector2Int.zero, s_libraryL)));
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);
            Draw(new Vector2Int(1, 4));
            Assume.That(GetDraftEdit()?.Cells, Is.EquivalentTo(s_tallL));
            Assert.That(root.Q<Label>("template-banner-text").text,
                Does.Contain("Save Level asks before leaving these levels on the old footprint: Level Other A:"));

            m_confirmAnswer = false;
            Assert.That(Save(), Is.False, "Cancel writes nothing.");
            Assert.That(m_confirms, Has.Count.EqualTo(1));
            Assert.That(m_confirms[0], Does.Contain("Level Other A:").And.Contain("b1"));
            Assert.That(m_confirms[0], Does.Not.Contain("Other B"));
            Assert.That(m_writes, Is.Empty);
            Assert.That(m_commits, Is.Empty);
            Assert.That(GetDraftEdit(), Is.Not.Null, "The edit stays pending in the draft.");

            m_confirmAnswer = true;
            Assert.That(Save(), Is.True);
            Assert.That(m_writes, Has.Count.EqualTo(1));
            Assert.That(DescribeBlocks(m_writes[0][1]), Is.EqualTo(DescribeBlocks(otherA)),
                "The level that cannot take it stays on the old footprint.");
            Assert.That(Cells(m_writes[0][2].GetBlockAt(0)), Is.EquivalentTo(s_tallL), "The others take it.");
            Assert.That(m_commits, Has.Count.EqualTo(1));
            Assert.That(m_commits[0][0].Cells, Is.EquivalentTo(s_tallL));
            Assert.That(root.Q<Label>("status").text,
                Does.Contain("Re-stamped Other B (1 block(s)).").And.Contain("Left Other A on the old footprint"));
        }

        [Test]
        public void ALibraryChangedElsewhereDuringTheEditRefusesTheSave()
        {
            VisualElement root = OpenWindow();
            RequireCanvas(root);
            UseLibrary();
            UseDatabase(root, Level("pen-fixture", "Pen fixture", 8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL)));
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);
            Draw(new Vector2Int(2, 2));
            // The Piece Editor saves another footprint for the same template meanwhile.
            ChangeLibrary(Template(s_corner));

            Assert.That(Save(), Is.False);

            Assert.That(m_writes, Is.Empty);
            Assert.That(m_commits, Is.Empty);
            Assert.That(root.Q<Label>("status").text, Does.StartWith("Save Level wrote nothing.")
                .And.Contain("changed in the piece library after you started editing it here"));
            Assert.That(root.Q<Label>("template-banner-text").text, Does.Contain("changed in the piece library"));
        }

        [Test]
        public void ANewFootprintOfAnotherLibraryPiecesFamilyRefusesTheSave()
        {
            VisualElement root = OpenWindow();
            RequireCanvas(root);
            UseLibrary(new BlockShapeLibrary.Shape("Pen fixture tall", LevelCustomPiece.Transform(s_tallL, 4)));
            UseDatabase(root, Level("pen-fixture", "Pen fixture", 8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL)));
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);
            Draw(new Vector2Int(1, 4));

            Assert.That(Save(), Is.False);

            Assert.That(m_writes, Is.Empty);
            Assert.That(m_commits, Is.Empty);
            Assert.That(root.Q<Label>("status").text,
                Does.Contain("is Pen fixture tall turned or mirrored; the piece library keeps one piece per shape"));
        }

        [UnityTest]
        public IEnumerator AnUndoPastASaveThenSaveIsAcceptedAndKeepsAnArmedMatchingPiece()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            RequireCanvas(root);
            UseLibrary();
            UseDatabase(root, Level("pen-fixture", "Pen fixture", 8, 8,
                    new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL)),
                Level("pen-other-a", "Other A", 8, 8,
                    new LevelBlockData("b1", "Red", new Vector2Int(3, 3), Turned(s_libraryL))));
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);
            Draw(new Vector2Int(2, 2));
            // Below the column: a six-cell footprint whose 8 rotations and mirrors all differ.
            Draw(new Vector2Int(1, 0));
            Vector2Int[] second = ToArray(GetDraftEdit().Cells);
            Assume.That(second, Has.Length.EqualTo(6));
            // A piece armed with the new footprint, as the tile arms it while the edit is pending.
            Invoke("ArmShape", GetEditCatalog().FindByName(k_templateName));
            m_commitWritesLibrary = true;

            Assert.That(Save(), Is.True);
            Assert.That(m_library.Shapes[2].Cells, Is.EquivalentTo(second), "The library holds the saved footprint.");
            Assert.That(GetToolState().ArmedKey, Is.EqualTo(LevelTestShapes.Key(second)),
                "The window's own commit keeps a piece armed with the new footprint.");
            Assert.That(root.Q<Label>("status").text, Does.Not.Contain("Pick the shape again"));

            Undo.PerformUndo();
            yield return null;
            Assume.That(GetDraftEdit()?.Cells, Is.EquivalentTo(s_grownP), "The first stroke's footprint is back.");

            Assert.That(Save(), Is.True, "The library holds this window's own commit, so the base is accepted.");

            Assert.That(m_writes, Has.Count.EqualTo(2));
            Assert.That(m_commits, Has.Count.EqualTo(2));
            Assert.That(m_commits[1][0].Cells, Is.EquivalentTo(s_grownP));
            Assert.That(Cells(m_writes[1][1].GetBlockAt(0)), Is.EquivalentTo(Turned(s_grownP)),
                "The other level is re-stamped from the committed footprint.");
        }

        [UnityTest]
        public IEnumerator AfterAnUndoPastACommittedSaveDrawingBackAndDiscardReturnToTheSavedFootprint()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            RequireCanvas(root);
            UseLibrary();
            UseDatabase(root, Level("pen-fixture", "Pen fixture", 8, 8,
                    new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL)),
                Level("pen-other-a", "Other A", 8, 8,
                    new LevelBlockData("b1", "Red", new Vector2Int(3, 3), Turned(s_libraryL))));
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);
            Draw(new Vector2Int(2, 2));
            Draw(new Vector2Int(1, 0));
            Vector2Int[] saved = ToArray(GetDraftEdit().Cells);
            m_commitWritesLibrary = true;
            Assume.That(Save(), Is.True);
            Assume.That(m_library.Shapes[2].Cells, Is.EquivalentTo(saved));

            Undo.PerformUndo();
            yield return null;
            Assume.That(GetDraftEdit()?.Cells, Is.EquivalentTo(s_grownP), "The edit from before the Save is back.");
            Draw(new Vector2Int(1, 0));
            Assert.That(GetDraftEdit(), Is.Null, "Drawing back to the saved footprint drops the edit.");
            Assert.That(FindShapeIssue(root, "b1"), Is.Null, "The level matches the library.");

            Undo.PerformUndo();
            yield return null;
            Assume.That(GetDraftEdit()?.Cells, Is.EquivalentTo(s_grownP));
            Press(root.Q<Button>("template-banner-discard"));

            Assert.That(Cells(GetDraft().GetBlockAt(0)), Is.EquivalentTo(saved),
                "Discard returns to the saved footprint, not the one from before the Save.");
            Assert.That(GetDraftEdit(), Is.Null);
            Assert.That(FindShapeIssue(root, "b1"), Is.Null);
            Assert.That(Save(), Is.True, "The next save is accepted.");
            Assert.That(m_commits, Has.Count.EqualTo(1), "It writes nothing new to the piece library.");
        }

        [UnityTest]
        public IEnumerator AfterAnUndoPastAFailedSaveDiscardReturnsToTheSavedFootprintAndSaveIsAccepted()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            RequireCanvas(root);
            UseLibrary();
            UseDatabase(root, Level("pen-fixture", "Pen fixture", 8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL)));
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);
            Draw(new Vector2Int(2, 2));
            Draw(new Vector2Int(1, 0));
            Vector2Int[] saved = ToArray(GetDraftEdit().Cells);
            m_commitIssue = "The library is read-only.";
            Assume.That(Save(), Is.True);
            Assume.That(GetSession().FindTemplate(GetLoadedGuid(), s_templateKey).IsPending, Is.True);

            Undo.PerformUndo();
            yield return null;
            Assume.That(GetDraftEdit()?.Cells, Is.EquivalentTo(s_grownP));
            Press(root.Q<Button>("template-banner-discard"));

            Assert.That(Cells(GetDraft().GetBlockAt(0)), Is.EquivalentTo(saved),
                "Discard returns to the footprint the saved levels hold, not the library's.");
            Assert.That(FindShapeIssue(root, "b1"), Is.Null);
            m_commitIssue = null;
            Assert.That(Save(), Is.True, "The level is saveable again.");
            Assert.That(m_commits, Has.Count.EqualTo(2));
            Assert.That(m_commits[1][0].Cells, Is.EquivalentTo(saved), "The retry writes the saved footprint.");
            Assert.That(GetSession().FindTemplate(GetLoadedGuid(), s_templateKey).IsPending, Is.False);
        }

        [Test]
        public void ASavedFootprintNoLevelHoldsAnyMoreIsDroppedSoTheNextSaveReStampsAndCommitsNothing()
        {
            VisualElement root = OpenWindow();
            RequireCanvas(root);
            UseLibrary();
            LevelData otherA = Level("pen-other-a", "Other A", 8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(3, 3), Turned(s_libraryL)));
            UseDatabase(root, Level("pen-fixture", "Pen fixture", 8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL)), otherA);
            LevelDatabaseSnapshot beforeSave = LevelEditorTestAccess.GetLoadedSource(m_window);
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);
            Draw(new Vector2Int(2, 2));
            m_commitIssue = "The library is read-only.";
            Assume.That(Save(), Is.True);
            Assume.That(GetSession().FindTemplate(GetLoadedGuid(), s_templateKey).IsPending, Is.True);

            // Restore Previous Save puts back the levels from before that save, then reads them again (a level load
            // ends the Pen edit).
            Invoke("SetTool", LevelEditorTool.Select);
            Assert.That(beforeSave.LoadInto(LevelEditorTestAccess.GetLevels(m_window), out string restoreError), Is.True,
                restoreError);
            LoadFixture(root, LevelEditorTestAccess.GetLevels(m_window).GetAt(0));
            LevelEditorTestAccess.Set(m_window, "m_savedDraftJson", JsonUtility.ToJson(GetDraft()));
            string note = (string)LevelEditorTestAccess.Invoke(m_window, "ForgetUnheldTemplates", null);

            Assert.That(note, Does.Contain("No level holds the new footprint of Pen fixture L any more"));
            Assert.That(GetSession().FindTemplate(GetLoadedGuid(), s_templateKey).IsPending, Is.False);
            Assert.That(root.Q("template-banner").style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(FindShapeIssue(root, "b1"), Is.Null, "The open level matches the library again.");
            Assert.That(m_window.hasUnsavedChanges, Is.False);

            m_commitIssue = null;
            Assert.That(Save(), Is.True);
            Assert.That(m_writes, Has.Count.EqualTo(2));
            Assert.That(DescribeBlocks(m_writes[1][1]), Is.EqualTo(DescribeBlocks(otherA)), "No level is re-stamped.");
            Assert.That(m_commits, Has.Count.EqualTo(1), "Nothing is written to the piece library.");
        }

        [Test]
        public void ARetryAsksNothingAgainAndALevelLeftOnTheOldFootprintCanBeSaved()
        {
            VisualElement root = OpenWindow();
            RequireCanvas(root);
            UseLibrary();
            // Other A's column has no room for a fourth cell on the 8 x 8 board; Other B's has.
            UseDatabase(root, Level("pen-fixture", "Pen fixture", 8, 8,
                    new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL)),
                Level("pen-other-a", "Other A", 8, 8,
                    new LevelBlockData("b1", "Red", new Vector2Int(5, 5), s_libraryL)),
                Level("pen-other-b", "Other B", 8, 8,
                    new LevelBlockData("b1", "Red", Vector2Int.zero, s_libraryL)));
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);
            Draw(new Vector2Int(1, 4));
            m_confirmAnswer = true;
            m_commitIssue = "The library is read-only.";
            Assume.That(Save(), Is.True);
            Assume.That(m_confirms, Has.Count.EqualTo(1));
            // DEC-193: Other A's block keeps the old footprint, whose key is no template once the change is committed,
            // so the same write names that key after the template in Other A's pieces table.
            ShapeKey oldKey = LevelTestShapes.Key(s_libraryL);
            Assert.That(LevelPieceNames.Find(m_writes[0][1], oldKey), Is.EqualTo(k_templateName));
            Assert.That(Cells(m_writes[0][1].GetBlockAt(0)), Is.EquivalentTo(s_libraryL));
            Assert.That(LevelPieceNames.Find(m_writes[0][2], oldKey), Is.Null, "Re-stamped Other B needs no name.");

            // The designer opens Other A, left on the old footprint, and changes only its timer.
            Invoke("SetTool", LevelEditorTool.Select);
            LevelEditorTestAccess.Set(m_window, "m_selectedIndex", 1);
            LoadFixture(root, LevelEditorTestAccess.GetLevels(m_window).GetAt(1));
            LevelEditorTestAccess.Set(m_window, "m_savedDraftJson", JsonUtility.ToJson(GetDraft()));
            Invoke("SetDraftInt", "m_level.m_timerSeconds", 90, "Set Level Timer");
            Assume.That(root.Q<Label>("template-banner-text").text, Does.Not.Contain("Save Level asks"));
            m_commitIssue = null;

            Assert.That(Save(), Is.True, "The level left on the old footprint is saved.");

            Assert.That(m_confirms, Has.Count.EqualTo(1), "Nobody is asked again.");
            Assert.That(m_writes, Has.Count.EqualTo(2));
            Assert.That(m_writes[1][1].TimerSeconds, Is.EqualTo(90));
            Assert.That(Cells(m_writes[1][1].GetBlockAt(0)), Is.EquivalentTo(s_libraryL),
                "It keeps the old footprint.");
            Assert.That(LevelPieceNames.Find(m_writes[1][1], oldKey), Is.EqualTo(k_templateName),
                "And its level piece keeps the template's name.");
            Assert.That(DescribeBlocks(m_writes[1][2]), Is.EqualTo(DescribeBlocks(m_writes[0][2])),
                "The retry re-stamps nothing.");
            Assert.That(m_commits, Has.Count.EqualTo(2));
            Assert.That(m_commits[1][0].Cells, Is.EquivalentTo(s_tallL));
            Assert.That(GetSession().FindTemplate(GetLoadedGuid(), s_templateKey).IsPending, Is.False);
        }

        [Test]
        public void KeepLibraryFootprintUnblocksSaveLevelAfterTheLibraryChangedElsewhere()
        {
            VisualElement root = OpenWindow();
            RequireCanvas(root);
            UseLibrary();
            UseDatabase(root, Level("pen-fixture", "Pen fixture", 8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL)));
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);
            Draw(new Vector2Int(2, 2));
            m_commitIssue = "The library is read-only.";
            Assume.That(Save(), Is.True);
            // The Piece Editor then saves another footprint for the same template.
            ChangeLibrary(Template(s_corner));
            m_commitIssue = null;

            Assert.That(Save(), Is.False, "The saved footprint can no longer be committed.");
            Assert.That(root.Q<Label>("status").text,
                Does.Contain("Press Keep library footprint in the template banner"));
            Assert.That(root.Q<Button>("template-banner-keep").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Button>("template-banner-discard").style.display.value, Is.EqualTo(DisplayStyle.None));

            Press(root.Q<Button>("template-banner-keep"));

            Assert.That(GetSession().FindTemplate(GetLoadedGuid(), s_templateKey).IsPending, Is.False);
            Assert.That(root.Q<Label>("status").text,
                Does.StartWith("The piece library keeps its footprint of Pen fixture L."));
            Assert.That(root.Q<Button>("template-banner-keep").style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(root.Q<Label>("template-banner-text").text, Does.Not.Contain("not in the piece library yet"));
            Assert.That(Save(), Is.True, "Save Level works again.");
            Assert.That(m_commits, Has.Count.EqualTo(1), "Only the first, failed commit was tried.");
            Assert.That(m_window.hasUnsavedChanges, Is.False);
        }

        [Test]
        public void APendingFootprintOfAnotherLevelDatabaseKeepsTheClosePrompt()
        {
            VisualElement root = OpenWindow();
            RequireCanvas(root);
            UseLibrary();
            UseDatabase(root, Level("pen-fixture", "Pen fixture", 8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_libraryL)));
            Invoke("SetTool", LevelEditorTool.Pen);
            Invoke("EditBlockWithPen", 0);
            Draw(new Vector2Int(2, 2));
            m_commitIssue = "The library is read-only.";
            Assume.That(Save(), Is.True);
            string guid = GetLoadedGuid();

            // Another level database becomes the active one.
            LevelEditorTestAccess.Set(m_window, "m_loadedGuid", "other-database");
            Invoke("OnDraftChanged");

            Assert.That(m_window.hasUnsavedChanges, Is.True, "Closing still asks Save/Discard/Cancel.");
            Assert.That(m_window.saveChangesMessage, Does.Contain("1 template footprint(s) of ")
                .And.Contain("Open that level database and Save Level to write them."));
            m_window.SaveChanges();
            Assert.That(m_window.hasUnsavedChanges, Is.True, "Save here cannot write that database's footprint.");
            LevelEditorTestAccess.Set(m_window, "m_loadedGuid", guid);
        }

        [Test]
        public void AGateStrokeEndsThePenEditSoTheNextSwatchRecoloursNoBlock()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = RequireCanvas(root);
            UseLibrary();
            LoadFixture(root, Level("pen-fixture", "Pen fixture", 8, 8,
                new LevelBlockData("b1", "Red", new Vector2Int(2, 2), s_single)));
            SendKey(canvas, KeyCode.Alpha2);
            ClickCell(root, canvas, new Vector2Int(2, 2));
            Assume.That(canvas.FreehandBlockId, Is.EqualTo("b1"));
            Invoke("PickSwatch", PaletteColor.DarkGreen);
            Assume.That(GetDraft().GetBlockAt(0).ColorId, Is.EqualTo(PaletteColor.DarkGreen.ToString()));

            Invoke("PaintDoors", BoardSide.Bottom, (IReadOnlyList<int>)new[] { 3 });
            Assume.That(GetDraft().DoorCount, Is.EqualTo(2));
            Assert.That(canvas.IsFreehand, Is.False, "The painted gate is the selection now.");
            Invoke("PickSwatch", PaletteColor.Purple);

            Assert.That(GetDraft().GetBlockAt(0).ColorId, Is.EqualTo(PaletteColor.DarkGreen.ToString()),
                "The block keeps its colour.");
            Assert.That(GetDraft().GetDoorAt(1).ColorId, Is.EqualTo(PaletteColor.DarkGreen.ToString()),
                "A just-painted gate is no choice either.");
        }

        [Test]
        public void NoToolInfoBoxSitsBetweenTheTopBarAndTheTemplateBanner()
        {
            // The tool info box was removed on the user's request of 2026-09-26.
            VisualElement root = OpenWindow();
            Assert.That(root.Q("tool-info"), Is.Null);
            VisualElement pane = root.Q("selection-pane");
            Assert.That(pane.IndexOf(root.Q("template-banner")), Is.EqualTo(pane.IndexOf(root.Q("top-bar")) + 1),
                "The template banner sits directly under the top bar.");
        }

        private static LevelData Level(string id, string name, int width, int height, params LevelBlockData[] blocks)
        {
            return new LevelData(id, name, width, height, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red") }, blocks);
        }

        private static BlockShapeLibrary.Shape Template(IReadOnlyList<Vector2Int> cells)
        {
            return new BlockShapeLibrary.Shape(k_templateName, ToArray(cells));
        }

        // A transient library; no asset is written.
        private void UseLibrary(params BlockShapeLibrary.Shape[] extra)
        {
            m_library = ScriptableObject.CreateInstance<BlockShapeLibrary>();
            ChangeLibrary(extra, false);
            LevelEditorTestAccess.Set(m_window, "m_shapeLibrary", m_library);
            Invoke("OnFocus");
        }

        // A library commit as the piece library makes it, delivered to this window only.
        private void ChangeLibrary(BlockShapeLibrary.Shape template)
        {
            ChangeLibrary(new[] { template }, true);
        }

        private void ChangeLibrary(BlockShapeLibrary.Shape[] shapes, bool notify)
        {
            List<BlockShapeLibrary.Shape> all = new List<BlockShapeLibrary.Shape>
            {
                new BlockShapeLibrary.Shape("Single", s_single),
                new BlockShapeLibrary.Shape("DoubleHorizontal", s_domino)
            };
            bool hasTemplate = false;
            foreach (BlockShapeLibrary.Shape shape in shapes)
            {
                hasTemplate |= shape.DisplayName == k_templateName;
            }

            if (!hasTemplate)
            {
                all.Add(Template(s_libraryL));
            }

            all.AddRange(shapes);
            m_library.Load(BlockShapeLibraryJson.Write(all));
            if (notify)
            {
                Invoke("OnLibraryCommitted");
            }
        }

        // The test's levels stand in for the project's in the window's own catalog, under the active JSON's GUID; the
        // first level opens. Save Level's write goes to the fake seam, which reloads that catalog only.
        private void UseDatabase(VisualElement root, params LevelData[] levels)
        {
            Assert.That(LevelDatabaseJson.TryWrite(levels, out string json, out string error), Is.True, error);
            LevelEditorTestAccess.LoadLevels(m_window, json);
            m_usesDatabase = true;
            LevelEditorTestAccess.Set(m_window, "m_selectedIndex", 0);
            LevelEditorTestAccess.SetLoadedSource(m_window, json);
            LoadFixture(root, levels[0]);
            // As loaded: the draft is the saved level.
            LevelEditorTestAccess.Set(m_window, "m_savedDraftJson", JsonUtility.ToJson(GetDraft()));
            Invoke("OnDraftChanged");
        }

        private bool Save()
        {
            return (bool)LevelEditorTestAccess.Invoke(m_window, "SaveLevel", null);
        }

        // Draws a Pen stroke through the window's release handler, as the canvas raises it.
        private void Draw(params Vector2Int[] cells)
        {
            Invoke("PlacePieceStroke", (IReadOnlyList<Vector2Int>)cells);
        }

        private void Invoke(string method, params object[] arguments)
        {
            LevelEditorTestAccess.Invoke(m_window, method, arguments);
        }

        private T GetField<T>(string name)
        {
            return (T)LevelEditorTestAccess.Get(m_window, name);
        }

        private LevelData GetDraft()
        {
            return GetField<LevelDraft>("m_draft").Snapshot();
        }

        // The pending edit of the fixture template, found by the key the library held when the edit began.
        private LevelTemplateEdit GetDraftEdit()
        {
            return GetDraftEdit(s_templateKey);
        }

        private LevelTemplateEdit GetDraftEdit(ShapeKey templateKey)
        {
            return GetField<LevelDraft>("m_draft").FindTemplateEdit(templateKey);
        }

        private LevelEditorToolState GetToolState()
        {
            return GetField<LevelEditorToolState>("m_toolState");
        }

        private LevelPieceSession GetSession()
        {
            return GetField<LevelPieceSession>("m_pieceSession");
        }

        private string GetLoadedGuid()
        {
            return GetField<string>("m_loadedGuid");
        }

        private LevelShapeCatalog GetEditCatalog()
        {
            return (LevelShapeCatalog)LevelEditorTestAccess.Invoke(m_window, "GetEditCatalog", null);
        }

        // A template tile, found by the template's name, which stays while a pending footprint changes its key.
        private static VisualElement GetTile(LevelShapePalette palette, string displayName)
        {
            for (int index = 0; index < palette.TileCount; index++)
            {
                if (palette.GetChoice(index).DisplayName == displayName)
                {
                    return palette.GetTile(index);
                }
            }

            Assert.Fail($"No tile for {displayName}.");
            return null;
        }

        private static LevelShapeChoice GetChoice(LevelShapePalette palette, string displayName)
        {
            for (int index = 0; index < palette.TileCount; index++)
            {
                if (palette.GetChoice(index).DisplayName == displayName)
                {
                    return palette.GetChoice(index);
                }
            }

            Assert.Fail($"No tile for {displayName}.");
            return null;
        }

        // The issues list's library-shape warning for a block, or null.
        private static LevelIssue FindShapeIssue(VisualElement root, string blockId)
        {
            LevelEditorIssuesList list = root.Q<LevelEditorIssuesList>();
            for (int index = 0; index < list.IssueCount; index++)
            {
                LevelIssue issue = list.GetIssue(index);
                if (issue.EntityId == blockId && issue.Message.Contains("library piece"))
                {
                    return issue;
                }
            }

            return null;
        }

        private static Vector2Int[] Cells(LevelBlockData block)
        {
            return LevelRecordCopy.GetCells(block);
        }

        // Each block's id, piece key, origin and cells: what a re-stamp can change, whatever JSON round trip the level
        // took.
        private static string DescribeBlocks(LevelData level)
        {
            string[] blocks = new string[level.BlockCount];
            for (int index = 0; index < blocks.Length; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                Vector2Int[] cells = Cells(block);
                Array.Sort(cells, (a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
                blocks[index] = $"{block.Id}:{block.Key}@{block.Origin}:{string.Join(",", cells)}";
            }

            return string.Join(";", blocks);
        }

        private static Vector2Int[] ToArray(IReadOnlyList<Vector2Int> cells)
        {
            Vector2Int[] copy = new Vector2Int[cells.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = cells[index];
            }

            return copy;
        }

        // Transform 1: the footprint turned clockwise once.
        private static Vector2Int[] Turned(IReadOnlyList<Vector2Int> cells)
        {
            return LevelCustomPiece.Transform(cells, 1);
        }

        private static string[] GetLiveFiles()
        {
            return LiveAssetGuard.GetLevelEditorLiveFiles();
        }

        private static string[] Hash(string[] files)
        {
            return LiveAssetGuard.Hash(files);
        }

        private LevelCanvasElement RequireCanvas(VisualElement root)
        {
            // Every caller immediately replaces this with its own fixture through LoadFixture or UseDatabase, so a
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
            GetField<LevelDraft>("m_draft").Replace(level);
            Invoke("OnDraftChanged");
            ValidateLayout(root);
        }

        // Every seam is a fake: nothing reaches the level JSON, the piece library, a dialog or the Piece Editor.
        private LevelDatabaseWindow CreateWindow()
        {
            LevelDatabaseWindow window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            window.IsPieceEditorOpen = () => false;
            window.ShowLevelPieceInEditor = _ => { };
            window.ShowLibraryPieceInEditor = _ => { };
            window.ConfirmPieceDelete = message =>
            {
                m_deleteQuestions.Add(message);
                return false;
            };
            window.CommitTemplatesToLibrary = edits =>
            {
                m_commits.Add(new List<LevelTemplateEdit>(edits));
                if (m_commitIssue == null && m_commitWritesLibrary)
                {
                    // As the real commit does: the transient library takes the footprint and its event reaches the
                    // window inside the commit.
                    ChangeLibrary(Template(edits[0].Cells));
                }

                return m_commitIssue;
            };
            window.WriteLevels = levels =>
            {
                m_writes.Add(new List<LevelData>(levels));
                if (!m_usesDatabase || !LevelDatabaseJson.TryWrite(levels, out string json, out _))
                {
                    return false;
                }

                LevelEditorTestAccess.LoadLevels(window, json);
                return true;
            };
            window.ConfirmTemplateSave = message =>
            {
                m_confirms.Add(message);
                return m_confirmAnswer;
            };
            return window;
        }

        private VisualElement OpenWindow()
        {
            m_fixture = new LevelEditorWindowFixture();
            VisualElement root = m_fixture.Open(CreateWindow());
            m_window = m_fixture.Window;
            return root;
        }

        // Unity builds this window's GUI itself here: a manual CreateGUI could be followed by Unity's own call, which
        // rebuilds the draft and clears its undo records between an edit and its undo.
        private IEnumerator ShowWindowBuiltByUnity()
        {
            m_fixture = new LevelEditorWindowFixture();
            yield return m_fixture.ShowBuiltByUnity(CreateWindow());
            m_window = m_fixture.Window;
        }

        // The template banner shows and hides above the canvas as editing starts and ends, so the layout is brought up
        // to date before a cell's position is read. Button 0 is a left click and 1 a right click.
        private static void ClickCell(VisualElement root, LevelCanvasElement canvas, Vector2Int cell, int button = 0)
        {
            ValidateLayout(root);
            Click(root, canvas.LocalToWorld(canvas.Geometry.GetCellRect(cell).center), button);
        }

        private static void Click(VisualElement root, Vector2 position, int button = 0)
        {
            UiTestInput.Click(root, position, button);
        }

        private static void SendPointer(VisualElement root, EventType type, Vector2 position, int button = 0)
        {
            UiTestInput.SendPointer(root, type, position, button);
        }

        // UI Toolkit routes keys to the focused element, so the target is focused first.
        private static void SendKey(VisualElement target, KeyCode key)
        {
            UiTestInput.SendKey(target, key);
        }

        // Submit presses a button the way a click does, wherever the view is scrolled.
        private static void Press(Button button)
        {
            UiTestInput.Press(button);
        }

        private static void ValidateLayout(VisualElement root)
        {
            UiTestInput.ValidateLayout(root);
        }

        private static bool IsLevelLoaded(VisualElement root)
        {
            return LevelEditorWindowFixture.IsLevelLoaded(root);
        }

        // Puts back the snapshot a test window replaced, so an open LevelEditor scene preview shows the user's level again.
        private static void RestorePublishedLevel(LevelData previous)
        {
            LevelEditorWindowFixture.RestorePublishedLevel(previous);
        }
    }
}
