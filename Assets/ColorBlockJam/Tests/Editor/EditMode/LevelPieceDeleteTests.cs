using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
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
    /// Verifies the template delete flow (DEC-126, TASK-20260926-191802848-515eb50b, choices revised by DEC-193) in
    /// the Level Editor window and through the Piece Editor route: an unused piece keeps the two-button delete; a used
    /// piece asks Delete template, Delete template and its blocks or Cancel; Delete template names the template's key in
    /// every level in one write and in the open draft as one undo step whose baseline moves with it; Delete template and
    /// its blocks deletes emptied levels and opens the nearest level when the open one goes; a failed write changes
    /// nothing; a failed library delete keeps the written levels; two Undos restore the piece and then the blocks; the
    /// Piece Editor's delete takes the same path with and without a Level Editor. The piece library is a transient
    /// library with no asset, so its real undoable delete runs in memory; the level database is in memory, every dialog
    /// and write is a fake seam, and SHA-256 guards prove no live piece or level file is written.
    /// </summary>
    public sealed class LevelPieceDeleteTests
    {
        private const BindingFlags k_members = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string k_templateName = "Triple";
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_domino = { Vector2Int.zero, Vector2Int.right };
        private static readonly Vector2Int[] s_bar = { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) };
        private static readonly Vector2Int[] s_column = { Vector2Int.zero, Vector2Int.up, new Vector2Int(0, 2) };
        // Declared after the cells, so the static initializers read them.
        private static readonly ShapeKey s_barKey = LevelTestShapes.Key(s_bar);
        private static readonly ShapeKey s_dominoKey = LevelTestShapes.Key(s_domino);

        private LevelEditorWindowFixture m_fixture;
        private LevelDatabaseWindow m_window;
        private BlockShapeLibrary m_library;
        // True once UseDatabase loaded the test's levels into the window; the fake write refuses before that.
        private bool m_usesDatabase;
        private Func<ShapeKey, string> m_previousPieceEditorRoute;
        private string[] m_liveFiles;
        private string[] m_liveHashes;
        private readonly List<List<LevelData>> m_writes = new List<List<LevelData>>();
        private readonly List<LevelPieceDeleteQuestion> m_questions = new List<LevelPieceDeleteQuestion>();
        private readonly List<string> m_confirms = new List<string>();
        private LevelPieceDeleteChoice m_answer;
        private bool m_writeFails;

        [SetUp]
        public void RememberLiveFiles()
        {
            m_previousPieceEditorRoute = PieceEditorWindow.DeleteLibraryPiece;
            m_writes.Clear();
            m_questions.Clear();
            m_confirms.Clear();
            m_answer = LevelPieceDeleteChoice.Cancel;
            m_writeFails = false;
            m_liveFiles = GetLiveFiles();
            m_liveHashes = Hash(m_liveFiles);
        }

        [TearDown]
        public void CloseWindow()
        {
            LevelPieceDeleteRoute.ResetSeams();
            PieceEditorWindow.DeleteLibraryPiece = m_previousPieceEditorRoute;
            // Discards, closes and restores the published-level snapshot.
            m_fixture?.Dispose();
            m_fixture = null;
            m_window = null;
            m_usesDatabase = false;

            if (m_library != null)
            {
                ShapeLibraryAuthoring.ClearHistory(m_library);
                Object.DestroyImmediate(m_library);
                m_library = null;
            }

            Assert.That(Hash(m_liveFiles), Is.EqualTo(m_liveHashes),
                "No test writes the live piece JSON, library asset or level JSON.");
        }

        [Test]
        public void AnUnusedPieceKeepsTheTwoButtonDelete()
        {
            VisualElement root = OpenFixture(Level("l1", "One", Bar("b1", 0, 0), Single("b2", 6, 6)));
            m_window.ChoosePieceDelete = _ =>
            {
                Assert.Fail("An unused piece never asks Delete template or Delete template and its blocks.");
                return LevelPieceDeleteChoice.Cancel;
            };
            string statusBeforeDelete = Status(root);

            DeleteShape(s_dominoKey);

            Assert.That(m_confirms, Has.Count.EqualTo(1));
            Assert.That(m_confirms[0], Does.Contain("No block in this level database uses it."));
            Assert.That(m_writes, Is.Empty);
            Assert.That(m_library.Shapes.Count, Is.EqualTo(3), "The fake confirmation cancels.");
            Assert.That(Status(root), Is.EqualTo(statusBeforeDelete),
                "Unlike the used-piece Cancel (k_cancelledStatus), an unused-piece Cancel leaves the status untouched.");
        }

        [Test]
        public void CancelChangesNothing()
        {
            VisualElement root = OpenFixture(Level("l1", "One", Bar("b1", 0, 0), Single("b2", 6, 6)),
                Level("l2", "Two", Bar("b1", 0, 0), Bar("b2", 0, 3), Single("b3", 6, 6)));
            string draft = DraftJson();

            DeleteShape(s_barKey);

            Assert.That(m_confirms, Is.Empty, "A used piece never asks the two-button question.");
            Assert.That(m_questions, Has.Count.EqualTo(1));
            Assert.That(m_questions[0].Message, Does.Contain("3 block(s) in 2 level(s)"));
            Assert.That(m_questions[0].Message, Does.Contain("- One (open level): 1 block(s)\n- Two: 2 block(s)"));
            Assert.That(m_questions[0].CanDeleteTemplate && m_questions[0].CanDeleteBlocks, Is.True);
            Assert.That(m_writes, Is.Empty);
            Assert.That(m_library.Shapes.Count, Is.EqualTo(3));
            Assert.That(DraftJson(), Is.EqualTo(draft));
            Assert.That(Status(root), Is.EqualTo(LevelPieceDeletePlan.k_cancelledStatus));
        }

        [Test]
        public void DeleteTemplateWritesEveryLevelOnceKeepsACleanDraftCleanThenDeletesThePiece()
        {
            VisualElement root = OpenFixture(Level("l1", "One", Bar("b1", 0, 0), Single("b2", 6, 6)),
                Level("l2", "Two", Bar("b1", 0, 0), Column("b2", 5, 3), Single("b3", 6, 6)));
            m_answer = LevelPieceDeleteChoice.DeleteTemplate;

            DeleteShape(s_barKey);

            Assert.That(m_writes, Has.Count.EqualTo(1), "One database write.");
            Assert.That(LevelPieceNames.Find(m_writes[0][0], s_barKey), Is.EqualTo(k_templateName),
                "The open level's saved record.");
            Assert.That(LevelPieceNames.Find(m_writes[0][1], s_barKey), Is.EqualTo(k_templateName));
            Assert.That(m_writes[0][1].GetBlockAt(1).Key, Is.EqualTo(s_barKey),
                "A turned bar has the same canonical footprint, so the same key.");
            Assert.That(LevelRecordCopy.GetCells(m_writes[0][1].GetBlockAt(1)), Is.EqualTo(s_column), "Its cells stay.");
            Assert.That(LevelPieceNames.Find(m_writes[0][1], LevelTestShapes.Key(s_single)), Is.Null);
            Assert.That(IndexOf(s_barKey), Is.LessThan(0), "The piece is deleted after the write.");
            Assert.That(LevelPieceNames.Find(GetDraft(), s_barKey), Is.EqualTo(k_templateName), "The draft follows.");
            Assert.That(HasDraftChanges(), Is.False, "A clean draft stays clean.");
            Assert.That(Status(root), Does.StartWith($"Deleted {k_templateName} from the piece library. 3 block(s) in " +
                                                     $"2 level(s) keep their cells as the level piece {k_templateName}."));
        }

        [Test]
        public void DeleteTemplateKeepsUnsavedEditsUnsaved()
        {
            OpenFixture(Level("l1", "One", Bar("b1", 0, 0), Single("b2", 6, 6)),
                Level("l2", "Two", Bar("b1", 0, 0), Single("b2", 6, 6)));
            // An unsaved edit: a new block the saved record does not have.
            LoadDraft(Level("l1", "One", Bar("b1", 0, 0), Single("b2", 6, 6), Single("b9", 4, 4)));
            Assume.That(HasDraftChanges(), Is.True);
            m_answer = LevelPieceDeleteChoice.DeleteTemplate;

            DeleteShape(s_barKey);

            Assert.That(m_writes, Has.Count.EqualTo(1));
            Assert.That(m_writes[0][0].BlockCount, Is.EqualTo(2), "The saved record is written, not the draft.");
            Assert.That(GetDraft().BlockCount, Is.EqualTo(3), "The unsaved block stays in the draft.");
            Assert.That(LevelPieceNames.Find(GetDraft(), s_barKey), Is.EqualTo(k_templateName));
            Assert.That(HasDraftChanges(), Is.True, "The draft still differs from the saved level only by its edit.");
            LevelData baseline = JsonUtility.FromJson<LevelData>(GetField<string>("m_savedDraftJson"));
            Assert.That(LevelPieceNames.Find(baseline, s_barKey), Is.EqualTo(k_templateName),
                "The dirty baseline moved with the write.");
        }

        [Test]
        public void DeleteBlocksRemovesThemEverywhereAndDeletesTheEmptiedLevel()
        {
            VisualElement root = OpenFixture(Level("l1", "One", Bar("b1", 0, 0), Single("b2", 6, 6)),
                Level("l2", "Two", Bar("b1", 0, 0)),
                Level("l3", "Three", Single("b1", 0, 0)));
            m_answer = LevelPieceDeleteChoice.DeleteBlocks;

            DeleteShape(s_barKey);

            Assert.That(m_questions[0].Message, Does.Contain(LevelPieceDeletePlan.k_emptiedHeading + "\n- Two"));
            Assert.That(m_writes, Has.Count.EqualTo(1));
            Assert.That(m_writes[0], Has.Count.EqualTo(2), "Two is deleted in the same write.");
            Assert.That(m_writes[0][0].BlockCount, Is.EqualTo(1));
            Assert.That(m_writes[0][1].Id, Is.EqualTo("l3"));
            Assert.That(LevelEditorTestAccess.GetLevels(m_window).Count, Is.EqualTo(2));
            Assert.That(GetDraft().BlockCount, Is.EqualTo(1), "The open level's draft loses the same blocks.");
            Assert.That(HasDraftChanges(), Is.False);
            Assert.That(GetField<int>("m_selectedIndex"), Is.Zero);
            Assert.That(GetTargetChoices(), Has.Count.EqualTo(2));
            Assert.That(IndexOf(s_barKey), Is.LessThan(0));
            Assert.That(Status(root), Does.Contain("deleted Two because no block remained"));
        }

        [Test]
        public void DeleteBlocksOfTheOpenLevelOpensTheNearestRemainingLevel()
        {
            VisualElement root = OpenFixture(Level("l1", "One", Bar("b1", 0, 0)),
                Level("l2", "Two", Bar("b1", 0, 0), Single("b2", 6, 6)),
                Level("l3", "Three", Single("b1", 0, 0)));
            LoadDraft(Level("l1", "One", Bar("b1", 0, 0), Single("b9", 4, 4)));
            m_answer = LevelPieceDeleteChoice.DeleteBlocks;

            DeleteShape(s_barKey);

            Assert.That(m_questions[0].Message, Does.Contain("- One (open level)\nOne has unsaved changes, which will be lost."));
            Assert.That(m_writes[0], Has.Count.EqualTo(2));
            Assert.That(GetField<int>("m_selectedIndex"), Is.Zero);
            Assert.That(GetDraft().Id, Is.EqualTo("l2"), "The next remaining level opens.");
            Assert.That(GetDraft().BlockCount, Is.EqualTo(1));
            Assert.That(HasDraftChanges(), Is.False);
            Assert.That(Status(root), Does.Contain("deleted One (open level) because no block remained"));
        }

        [Test]
        public void AFailedLevelWriteChangesNothing()
        {
            VisualElement root = OpenFixture(Level("l1", "One", Bar("b1", 0, 0), Single("b2", 6, 6)),
                Level("l2", "Two", Bar("b1", 0, 0), Single("b2", 6, 6)));
            string draft = DraftJson();
            m_answer = LevelPieceDeleteChoice.DeleteTemplate;
            m_writeFails = true;

            DeleteShape(s_barKey);

            Assert.That(m_writes, Has.Count.EqualTo(1), "The write was tried.");
            Assert.That(IndexOf(s_barKey), Is.GreaterThanOrEqualTo(0), "The piece stays.");
            Assert.That(DraftJson(), Is.EqualTo(draft), "The draft stays.");
            Assert.That(LevelPieceNames.Find(LevelEditorTestAccess.GetLevels(m_window).GetAt(1), s_barKey), Is.Null);
            Assert.That(Status(root), Does.StartWith("Deleted nothing."));
        }

        [Test]
        public void AWriteThatCouldNotBeReloadedSaysTheLevelsWereWrittenAndWhatToDoNext()
        {
            VisualElement root = OpenFixture(Level("l1", "One", Bar("b1", 0, 0), Single("b2", 6, 6)),
                Level("l2", "Two", Bar("b1", 0, 0), Single("b2", 6, 6)));
            string draft = DraftJson();
            m_answer = LevelPieceDeleteChoice.DeleteTemplate;
            // SaveLevels' reload failure: the JSON was replaced, then reading it back failed, which shows the error and
            // marks the source invalid.
            m_window.WriteLevels = levels =>
            {
                m_writes.Add(new List<LevelData>(levels));
                LevelEditorTestAccess.Set(m_window, "m_sourceValid", false);
                root.Q<Label>("status").text = "Saved JSON but could not reload it: Injected reload failure.";
                return false;
            };
            Assume.That(GetField<bool>("m_sourceValid"), Is.True);

            DeleteShape(s_barKey);

            Assert.That(m_writes, Has.Count.EqualTo(1));
            Assert.That(IndexOf(s_barKey), Is.GreaterThanOrEqualTo(0), "The piece stays in the library.");
            Assert.That(DraftJson(), Is.EqualTo(draft), "The draft stays.");
            Assert.That(Status(root), Does.Not.StartWith("Deleted nothing."), "The level JSON was written.");
            Assert.That(Status(root), Does.StartWith("Wrote the level JSON: 2 block(s) in 2 level(s) keep their cells " +
                                                     $"as the level piece {k_templateName}"));
            Assert.That(Status(root), Does.Contain("Saved JSON but could not reload it: Injected reload failure."));
            Assert.That(Status(root), Does.EndWith($"{k_templateName} is still in the piece library and the open " +
                                                   $"draft is unchanged. Press Reload JSON, then delete " +
                                                   $"{k_templateName} again."));
        }

        [Test]
        public void AFailedLibraryDeleteKeepsTheWrittenLevelsAndSaysToDeleteAgain()
        {
            VisualElement root = OpenFixture(Level("l1", "One", Bar("b1", 0, 0), Single("b2", 6, 6)),
                Level("l2", "Two", Bar("b1", 0, 0), Single("b2", 6, 6)));
            m_answer = LevelPieceDeleteChoice.DeleteBlocks;
            m_window.DeletePieceFromLibrary = _ => "Injected library failure.";

            DeleteShape(s_barKey);

            Assert.That(m_writes, Has.Count.EqualTo(1));
            Assert.That(IndexOf(s_barKey), Is.GreaterThanOrEqualTo(0));
            Assert.That(LevelEditorTestAccess.GetLevels(m_window).GetAt(1).BlockCount, Is.EqualTo(1),
                "The written levels stay.");
            Assert.That(Status(root), Does.Contain("but Triple is still in the piece library: Injected library failure. " +
                                                   "Press its × again to delete it."));

            // No block uses it now, so the × asks the ordinary question.
            m_window.DeletePieceFromLibrary = null;
            DeleteShape(s_barKey);
            Assert.That(m_confirms, Has.Count.EqualTo(1));
            Assert.That(m_questions, Has.Count.EqualTo(1));
        }

        [Test]
        public void APendingTemplateRefusesBeforeAnyQuestionOnBothPaths()
        {
            VisualElement root = OpenFixture(Level("l1", "One", Bar("b1", 0, 0), Single("b2", 6, 6)),
                Level("l2", "Two", Bar("b1", 0, 0), Single("b2", 6, 6)));
            GetField<LevelPieceSession>("m_pieceSession").SetPendingTemplate(GetField<string>("m_loadedGuid"),
                new LevelTemplateEdit(s_barKey, k_templateName, s_bar, s_column));
            LevelPieceDeleteRoute.FindLevelEditor = () => m_window;

            DeleteShape(s_barKey);
            Assert.That(LevelPieceDeleteRoute.Route(s_barKey), Does.Contain("has a template change waiting for Save Level"),
                "The route hands the refusal to the Piece Editor.");

            Assert.That(m_questions, Is.Empty);
            Assert.That(m_confirms, Is.Empty);
            Assert.That(m_writes, Is.Empty);
            Assert.That(Status(root), Does.Contain("has a template change waiting for Save Level"));
        }

        [UnityTest]
        public IEnumerator TheFirstUndoRestoresThePieceAndTheSecondTheBlocks()
        {
            yield return ShowWindowBuiltByUnity();
            UseLibrary();
            UseDatabase(Level("l1", "One", Bar("b1", 0, 0), Single("b2", 6, 6)),
                Level("l2", "Two", Bar("b1", 0, 0), Single("b2", 6, 6)));
            m_answer = LevelPieceDeleteChoice.DeleteTemplate;

            DeleteShape(s_barKey);
            Assume.That(IndexOf(s_barKey), Is.LessThan(0));
            Assume.That(LevelPieceNames.Find(GetDraft(), s_barKey), Is.EqualTo(k_templateName));

            Undo.PerformUndo();
            yield return null;
            Assert.That(IndexOf(s_barKey), Is.GreaterThanOrEqualTo(0), "The first Undo restores the piece.");
            Assert.That(LevelPieceNames.Find(GetDraft(), s_barKey), Is.EqualTo(k_templateName));

            Undo.PerformUndo();
            yield return null;
            Assert.That(LevelPieceNames.Find(GetDraft(), s_barKey), Is.Null, "The second restores the blocks.");
            Assert.That(m_writes, Has.Count.EqualTo(1), "Undo writes no level JSON.");
        }

        [Test]
        public void ThePieceEditorRouteRunsTheOpenLevelEditorsFlow()
        {
            OpenFixture(Level("l1", "One", Bar("b1", 0, 0), Single("b2", 6, 6)),
                Level("l2", "Two", Bar("b1", 0, 0), Single("b2", 6, 6)));
            LevelPieceDeleteRoute.FindLevelEditor = () => m_window;

            Assert.That(LevelPieceDeleteRoute.Route(s_dominoKey), Is.Null,
                "An unused piece keeps the Piece Editor's own delete.");
            Assert.That(m_questions, Is.Empty);

            Assert.That(LevelPieceDeleteRoute.Route(s_barKey), Is.EqualTo(LevelPieceDeletePlan.k_cancelledStatus),
                "The Piece Editor says a Cancel was a Cancel.");
            Assert.That(m_writes, Is.Empty);

            m_answer = LevelPieceDeleteChoice.DeleteTemplate;
            Assert.That(LevelPieceDeleteRoute.Route(s_barKey),
                Does.StartWith($"Deleted {k_templateName} from the piece library."));
            Assert.That(m_questions, Has.Count.EqualTo(2));
            Assert.That(m_writes, Has.Count.EqualTo(1));
            Assert.That(IndexOf(s_barKey), Is.LessThan(0));
            Assert.That(LevelPieceNames.Find(GetDraft(), s_barKey), Is.EqualTo(k_templateName));
        }

        [Test]
        public void ThePieceEditorRouteWithoutALevelEditorRunsThePlanOnTheSavedJson()
        {
            m_library = CreateLibrary();
            LevelData[] saved =
            {
                Level("l1", "One", Bar("b1", 0, 0)),
                Level("l2", "Two", Bar("b1", 0, 0), Single("b2", 6, 6))
            };
            LevelPieceDeleteRoute.ActiveDatabase database =
                new LevelPieceDeleteRoute.ActiveDatabase("guid", "Assets/Levels.json", null, saved);
            List<IReadOnlyList<LevelData>> written = new List<IReadOnlyList<LevelData>>();
            List<string> messages = new List<string>();
            LevelPieceDeleteRoute.FindLevelEditor = () => null;
            LevelPieceDeleteRoute.ReadActiveDatabase = () => database;
            LevelPieceDeleteRoute.WriteActiveDatabase = (source, levels) =>
            {
                Assert.That(source, Is.SameAs(database));
                written.Add(levels);
                return null;
            };
            LevelPieceDeleteRoute.LoadLibrary = () => m_library;
            LevelPieceDeleteRoute.CountOtherDatabases = (guid, key) => new List<LevelPieceDeletePlan.OtherDatabase>();
            LevelPieceDeleteRoute.ShowMessage = messages.Add;
            LevelPieceDeleteRoute.ChoosePieceDelete = question =>
            {
                m_questions.Add(question);
                return LevelPieceDeleteChoice.DeleteBlocks;
            };

            Assert.That(LevelPieceDeleteRoute.Route(s_dominoKey), Is.Null, "No block uses it.");
            LevelPieceDeleteRoute.ReadActiveDatabase = () => new LevelPieceDeleteRoute.ActiveDatabase("guid",
                "Assets/Levels.json", null, null);
            Assert.That(LevelPieceDeleteRoute.Route(s_barKey), Is.Null, "An unreadable database keeps today's delete.");
            LevelPieceDeleteRoute.ReadActiveDatabase = () => database;

            Assert.That(LevelPieceDeleteRoute.Route(s_barKey),
                Does.StartWith($"Deleted {k_templateName} and its 2 block(s) from 2 level(s); deleted One because no " +
                               "block remained."), "The Piece Editor shows what changed.");

            Assert.That(m_questions, Has.Count.EqualTo(1));
            Assert.That(m_questions[0].Message, Does.Contain(LevelPieceDeletePlan.k_emptiedHeading + "\n- One"));
            Assert.That(written, Has.Count.EqualTo(1));
            Assert.That(written[0], Has.Count.EqualTo(1), "One is deleted because no block remained.");
            Assert.That(written[0][0].BlockCount, Is.EqualTo(1));
            Assert.That(IndexOf(s_barKey), Is.LessThan(0), "The piece is deleted after the write.");
            Assert.That(messages, Is.Empty);
        }

        [Test]
        public void ThePieceEditorRouteWithoutALevelEditorSaysWhatHappenedInEveryOutcome()
        {
            m_library = CreateLibrary();
            LevelData[] saved =
            {
                Level("l1", "One", Bar("b1", 0, 0), Single("b2", 6, 6)),
                Level("l2", "Two", Bar("b1", 0, 0), Single("b2", 6, 6))
            };
            LevelPieceDeleteRoute.ActiveDatabase database =
                new LevelPieceDeleteRoute.ActiveDatabase("guid", "Assets/Levels.json", null, saved);
            List<string> messages = new List<string>();
            string writeError = null;
            int writes = 0;
            LevelPieceDeleteRoute.FindLevelEditor = () => null;
            LevelPieceDeleteRoute.ReadActiveDatabase = () => database;
            LevelPieceDeleteRoute.WriteActiveDatabase = (source, levels) =>
            {
                writes++;
                return writeError;
            };
            LevelPieceDeleteRoute.LoadLibrary = () => m_library;
            LevelPieceDeleteRoute.DeleteFromLibrary = (library, key) => "Injected library failure.";
            LevelPieceDeleteRoute.CountOtherDatabases = (guid, key) => new List<LevelPieceDeletePlan.OtherDatabase>();
            LevelPieceDeleteRoute.ShowMessage = messages.Add;
            m_answer = LevelPieceDeleteChoice.Cancel;
            LevelPieceDeleteRoute.ChoosePieceDelete = question =>
            {
                m_questions.Add(question);
                return m_answer;
            };

            Assert.That(LevelPieceDeleteRoute.Route(s_barKey), Is.EqualTo(LevelPieceDeletePlan.k_cancelledStatus),
                "Cancel says it was cancelled; no Level Editor message exists.");
            Assert.That(messages, Is.Empty);
            Assert.That(writes, Is.Zero);

            m_answer = LevelPieceDeleteChoice.DeleteTemplate;
            writeError = "Injected write failure.";
            string failedWrite = LevelPieceDeleteRoute.Route(s_barKey);
            Assert.That(failedWrite, Is.EqualTo("Deleted nothing from the piece library. Injected write failure."));
            Assert.That(messages, Is.EqualTo(new[] { failedWrite }), "The reason is shown and repeated in the status.");

            writeError = null;
            string failedDelete = LevelPieceDeleteRoute.Route(s_barKey);
            Assert.That(failedDelete, Does.Contain($"but {k_templateName} is still in the piece library: Injected " +
                                                   "library failure."));
            Assert.That(messages, Has.Count.EqualTo(2));
            Assert.That(messages[1], Is.EqualTo(failedDelete));
            Assert.That(IndexOf(s_barKey), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void ThePieceEditorDeleteCallsTheRouteForTheSelectedPiece()
        {
            Assert.That(m_previousPieceEditorRoute?.Method.DeclaringType, Is.EqualTo(typeof(LevelPieceDeleteRoute)),
                "The route is registered on domain load.");
            BlockShapeLibrary library = BlockShapeLibrary.LoadProject();
            ShapeKey key = library.Shapes[1].Key;
            PieceEditorWindow pieceEditor = ScriptableObject.CreateInstance<PieceEditorWindow>();
            try
            {
                pieceEditor.ShowUtility();
                pieceEditor.CreateGUI();
                pieceEditor.LoadLibraryPiece(key);
                bool isActive = (bool)typeof(PieceEditorWindow).GetMethod("IsActiveDatabase", k_members)
                    .Invoke(pieceEditor, null);
                if (!isActive)
                {
                    // Without the seam the Piece Editor would open its own modal question.
                    Assert.Ignore("The Piece Editor has no active piece database here.");
                }

                ShapeKey routed = default;
                PieceEditorWindow.DeleteLibraryPiece = piece =>
                {
                    routed = piece;
                    return LevelPieceDeletePlan.k_cancelledStatus;
                };
                typeof(PieceEditorWindow).GetMethod("DeletePiece", k_members).Invoke(pieceEditor, null);

                Assert.That(routed, Is.EqualTo(key));
                Assert.That(pieceEditor.rootVisualElement.Q<HelpBox>("piece-info").text,
                    Does.Contain(LevelPieceDeletePlan.k_cancelledStatus),
                    "The Piece Editor shows the route's own status, so a Cancel reads as a Cancel.");
            }
            finally
            {
                pieceEditor.Close();
            }
        }

        private VisualElement OpenFixture(params LevelData[] levels)
        {
            VisualElement root = OpenWindow();
            RequireCanvas(root);
            UseLibrary();
            UseDatabase(levels);
            return root;
        }

        private void DeleteShape(ShapeKey key)
        {
            LevelShapeChoice shape = GetField<LevelShapeCatalog>("m_shapeCatalog").Find(key);
            Assert.That(shape, Is.Not.Null, $"{key} is in the catalog.");
            Invoke("DeleteShape", shape);
        }

        private int IndexOf(ShapeKey key)
        {
            IReadOnlyList<BlockShapeLibrary.Shape> shapes = m_library.Shapes;
            for (int index = 0; index < shapes.Count; index++)
            {
                if (shapes[index].Key == key)
                {
                    return index;
                }
            }

            return -1;
        }

        private static LevelBlockData Bar(string id, int x, int y)
        {
            return new LevelBlockData(id, "Red", new Vector2Int(x, y), s_bar);
        }

        private static LevelBlockData Column(string id, int x, int y)
        {
            return new LevelBlockData(id, "Blue", new Vector2Int(x, y), s_column);
        }

        private static LevelBlockData Single(string id, int x, int y)
        {
            return new LevelBlockData(id, "Blue", new Vector2Int(x, y), s_single);
        }

        private static LevelData Level(string id, string name, params LevelBlockData[] blocks)
        {
            return new LevelData(id, name, 8, 8, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red") }, blocks);
        }

        // A transient library with no asset, so the real library delete and its undo run on this copy in memory.
        private static BlockShapeLibrary CreateLibrary()
        {
            BlockShapeLibrary library = ScriptableObject.CreateInstance<BlockShapeLibrary>();
            BlockShapeLibrary.Shape[] shapes =
            {
                new BlockShapeLibrary.Shape("Single", s_single),
                new BlockShapeLibrary.Shape("Domino", s_domino),
                new BlockShapeLibrary.Shape(k_templateName, s_bar)
            };
            library.Load(BlockShapeLibraryJson.Write(shapes));
            return library;
        }

        private void UseLibrary()
        {
            m_library = CreateLibrary();
            LevelEditorTestAccess.Set(m_window, "m_shapeLibrary", m_library);
            Invoke("OnFocus");
        }

        // The test's levels stand in for the project's in the window's own catalog, under the active JSON's GUID; the
        // first level opens.
        private void UseDatabase(params LevelData[] levels)
        {
            Assert.That(LevelDatabaseJson.TryWrite(levels, out string json, out string error), Is.True, error);
            LevelEditorTestAccess.LoadLevels(m_window, json);
            m_usesDatabase = true;
            LevelEditorTestAccess.Set(m_window, "m_selectedIndex", 0);
            LevelEditorTestAccess.SetLoadedSource(m_window, json);
            GetField<LevelDraft>("m_draft").Replace(LevelEditorTestAccess.GetLevels(m_window).GetAt(0));
            LevelEditorTestAccess.Set(m_window, "m_savedDraftJson", JsonUtility.ToJson(GetDraft()));
            Invoke("OnDraftChanged");
        }

        // Replaces the draft without an undo record, as an unsaved edit the saved level lacks.
        private void LoadDraft(LevelData level)
        {
            GetField<LevelDraft>("m_draft").Replace(level);
            Invoke("OnDraftChanged");
        }

        private string DraftJson()
        {
            return JsonUtility.ToJson(GetDraft());
        }

        private LevelData GetDraft()
        {
            return GetField<LevelDraft>("m_draft").Snapshot();
        }

        private bool HasDraftChanges()
        {
            return (bool)LevelEditorTestAccess.Invoke(m_window, "HasDraftChanges", null);
        }

        private List<string> GetTargetChoices()
        {
            return GetField<DropdownField>("m_targetField").choices;
        }

        private static string Status(VisualElement root)
        {
            return root.Q<Label>("status").text;
        }

        private T GetField<T>(string name)
        {
            return (T)LevelEditorTestAccess.Get(m_window, name);
        }

        private void Invoke(string method, params object[] arguments)
        {
            LevelEditorTestAccess.Invoke(m_window, method, arguments);
        }

        // Every seam is a fake: nothing reaches the level JSON, a dialog or the Piece Editor.
        private LevelDatabaseWindow CreateWindow()
        {
            LevelDatabaseWindow window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            window.IsPieceEditorOpen = () => false;
            window.ShowLevelPieceInEditor = _ => { };
            window.ShowLibraryPieceInEditor = _ => { };
            window.ConfirmPieceDelete = message =>
            {
                m_confirms.Add(message);
                return false;
            };
            window.ChoosePieceDelete = question =>
            {
                m_questions.Add(question);
                return m_answer;
            };
            window.CountOtherDatabaseUses = (guid, key) => new List<LevelPieceDeletePlan.OtherDatabase>();
            window.CommitTemplatesToLibrary = _ => "No template is committed in this test.";
            window.ConfirmTemplateSave = _ => false;
            window.WriteLevels = levels =>
            {
                m_writes.Add(new List<LevelData>(levels));
                if (m_writeFails || !m_usesDatabase || !LevelDatabaseJson.TryWrite(levels, out string json, out _))
                {
                    return false;
                }

                LevelEditorTestAccess.LoadLevels(window, json);
                return true;
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

        // Unity builds this window's GUI itself here, so a later automatic CreateGUI cannot clear the draft's undo
        // records between the delete and its undo.
        private IEnumerator ShowWindowBuiltByUnity()
        {
            m_fixture = new LevelEditorWindowFixture();
            yield return m_fixture.ShowBuiltByUnity(CreateWindow());
            m_window = m_fixture.Window;
            RequireCanvas(m_window.rootVisualElement);
        }

        private void RequireCanvas(VisualElement root)
        {
            // Every caller immediately replaces this with its own fixture through UseLibrary/UseDatabase, so a
            // deterministic fixture level here only needs to guarantee the canvas exists (Tests-Editor-1-F3),
            // whatever the real project's active database holds.
            m_fixture.LoadDatabase();
            // A fresh undo group after the fixture load, so the test's own first edit starts a clean group.
            Undo.IncrementCurrentGroup();
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(IsLevelLoaded(root) && canvas != null, Is.True, "The fixture database has a loaded level.");
        }

        // The live piece JSON, library asset and the active level JSON.
        private static string[] GetLiveFiles()
        {
            List<string> files = new List<string> { BoardKitAssets.ShapesJsonPath };
            string levels = LevelDatabaseAddressables.GetActivePath(LevelDatabaseAddressables.LoadProjectScope());
            if (!string.IsNullOrEmpty(levels))
            {
                files.Add(levels);
            }

            return files.ToArray();
        }

        private static string[] Hash(string[] files)
        {
            string[] hashes = new string[files.Length];
            using (SHA256 sha = SHA256.Create())
            {
                for (int index = 0; index < files.Length; index++)
                {
                    hashes[index] = File.Exists(files[index])
                        ? BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(files[index])))
                        : "missing";
                }
            }

            return hashes;
        }

        // Puts back the snapshot a test window replaced, so an open LevelEditor scene preview shows the user's level.
        private static void RestorePublishedLevel(LevelData previous)
        {
            LevelEditorWindowFixture.RestorePublishedLevel(previous);
        }

        private static bool IsLevelLoaded(VisualElement root)
        {
            return LevelEditorWindowFixture.IsLevelLoaded(root);
        }
    }
}
