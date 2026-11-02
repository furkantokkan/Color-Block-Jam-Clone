using System;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>Exercises piece ownership and window adapters using transient objects without authored asset writes.</summary>
    public sealed class LevelPieceEditControllerTests
    {
        private const string k_guid = "piece-controller-fixture";
        private const string k_templateName = "Controller corner";
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_corner = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };
        private static readonly Vector2Int[] s_square =
            { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.one };
        // Declared after the cells, so the static initializer reads them.
        private static readonly ShapeKey s_cornerKey = LevelTestShapes.Key(s_corner);
        private LevelDraftController m_draft;
        private LevelPieceSession m_session;
        private LevelPieceEditController m_controller;
        private BlockShapeLibrary m_library;
        private LevelEditorToolState m_tool;

        [SetUp]
        public void CreateOwners()
        {
            m_draft = new LevelDraftController();
            m_draft.Load(Level());
            m_session = new LevelPieceSession();
            m_controller = new LevelPieceEditController(m_draft, m_session);
            m_tool = new LevelEditorToolState();
            m_library = ScriptableObject.CreateInstance<BlockShapeLibrary>();
            m_library.hideFlags = HideFlags.HideAndDontSave;
            m_library.Load(BlockShapeLibraryJson.Write(new[]
            {
                new BlockShapeLibrary.Shape("Single", s_single),
                new BlockShapeLibrary.Shape(k_templateName, s_corner)
            }));
            m_controller.Library = m_library;
            m_controller.RefreshCatalog(true);
        }

        [TearDown]
        public void DestroyOwners()
        {
            m_draft.Dispose();
            UnityEngine.Object.DestroyImmediate(m_library);
        }

        [Test]
        public void CatalogCachesKeepIdentityAndDraftOverlayWinsOnlyForOpenLevel()
        {
            LevelShapeCatalog original = m_controller.Catalog;
            Assert.That(m_controller.RefreshCatalog(false), Is.False);
            Assert.That(m_controller.Catalog, Is.SameAs(original));
            m_session.SetPendingTemplate(k_guid,
                new LevelTemplateEdit(s_cornerKey, "Corner", s_corner, s_square));
            LevelShapeCatalog saved = m_controller.GetSavedCatalog(k_guid);
            Assert.That(m_controller.GetSavedCatalog(k_guid), Is.SameAs(saved));
            m_draft.Draft.SetTemplateEdit(new LevelTemplateEdit(s_cornerKey, "Corner", s_corner, s_single));
            LevelShapeCatalog edit = m_controller.GetEditCatalog(k_guid);
            Assert.That(FindTemplate(edit).Cells.Count, Is.EqualTo(1));
            Assert.That(FindTemplate(saved).Cells.Count, Is.EqualTo(4));
            Assert.That(m_controller.GetEditCatalog(k_guid), Is.SameAs(edit));
            Assert.That(FindTemplate(m_controller.GetSavedCatalog("other-database")).Cells.Count, Is.EqualTo(3));
            m_controller.RefreshCatalog(true);
            Assert.That(m_controller.GetEditCatalog(k_guid), Is.Not.SameAs(edit));
        }

        [TestCase("PlacePieceStroke")]
        [TestCase("RemovePieceCell")]
        [TestCase("MakeSeparatePiece")]
        public void NoOpPieceGestureStillRefreshesThePaletteAndDisarmsAnOutdatedShape(string gesture)
        {
            LevelDatabaseWindow window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            try
            {
                window.Show();
                LevelEditorTestAccess.Invoke(window, "CreateGUI");
                LevelEditorTestAccess.Invoke(window, "DestroyDraft");
                LevelDraftController draft = LevelEditorTestAccess.Get<LevelDraftController>(window, "m_draftController");
                draft.Load(Level());
                LevelPieceEditController pieces = LevelEditorTestAccess.Get<LevelPieceEditController>(window,
                    "m_pieceEditController");
                pieces.Library = m_library;
                LevelEditorTestAccess.Invoke(window, "RefreshShapeCatalog", true);
                LevelEditorToolState tool = LevelEditorTestAccess.Get<LevelEditorToolState>(window, "m_toolState");
                tool.Arm(pieces.Catalog.Find(s_cornerKey));
                LevelShapeCatalog previous = LevelEditorTestAccess.Get<LevelShapeCatalog>(window, "m_paletteCatalog");
                Assert.That(previous.Find(s_cornerKey).Cells.Count, Is.EqualTo(3));
                string before = JsonUtility.ToJson(draft.Snapshot());

                m_library.Load(BlockShapeLibraryJson.Write(new[]
                {
                    new BlockShapeLibrary.Shape("Single", s_single),
                    new BlockShapeLibrary.Shape(k_templateName, s_square)
                }));
                if (gesture == "PlacePieceStroke")
                {
                    LevelEditorTestAccess.Invoke(window, gesture, (object)Array.Empty<Vector2Int>());
                }
                else if (gesture == "RemovePieceCell")
                {
                    LevelEditorTestAccess.Invoke(window, gesture, new Vector2Int(-1, -1));
                }
                else
                {
                    LevelEditorTestAccess.Invoke(window, gesture);
                }

                LevelShapeCatalog current = LevelEditorTestAccess.Get<LevelShapeCatalog>(window, "m_paletteCatalog");
                Assert.That(current, Is.Not.SameAs(previous));
                Assert.That(current.FindByName(k_templateName).Cells.Count, Is.EqualTo(4));
                Assert.That(tool.HasArmedShape, Is.False, "A changed library footprint must be picked again.");
                Assert.That(JsonUtility.ToJson(draft.Snapshot()), Is.EqualTo(before),
                    "The refused/no-op gesture keeps the level unchanged.");
            }
            finally
            {
                window.DiscardChanges();
                window.Close();
            }
        }

        [Test]
        public void PenNeedsExplicitStart()
        {
            m_draft.Load(Level(Block("b1", s_single)));
            Assert.That(m_controller.StartPenEdit(k_guid, 0, m_tool), Is.Null);
            m_tool.SelectPen();
            Assert.That(m_controller.IsPenEditing(m_tool, out _), Is.False);
            Assert.That(m_controller.Draw(Source(), m_tool, new[] { new Vector2Int(2, 1) }).Feedback,
                Is.EqualTo(LevelPieceFeedback.Hint));
            m_draft.Load(Level(Block("b1", s_corner)));
            m_controller.StartPenEdit(k_guid, 0, m_tool);
            Assert.That(m_controller.IsPenEditing(m_tool, out string id), Is.True);
            Assert.That(id, Is.EqualTo("b1"));
            m_controller.EndPenEdit();
            Assert.That(m_controller.IsPenEditing(m_tool, out _), Is.False);
        }

        [Test]
        public void NewPieceStrokePlansBeforeApplyingAndRefusedStrokeKeepsTheDraft()
        {
            LevelNamedPiece piece = m_controller.CreateNamedPiece(Source());
            m_tool.ArmFreehand();
            LevelPieceEdit refused = m_controller.Draw(Source(), m_tool, new[] { new Vector2Int(-1, 0) });
            Assert.That(refused.Level, Is.Null);
            Assert.That(m_draft.Snapshot().BlockCount, Is.Zero);
            LevelPieceEdit edit = m_controller.Draw(Source(), m_tool,
                new[] { new Vector2Int(1, 1), new Vector2Int(2, 1) });
            Assert.That(edit.Piece, Is.SameAs(piece));
            Assert.That(m_draft.Snapshot().BlockCount, Is.Zero);
            Apply(edit);
            m_controller.UpdateDrawnPiece(k_guid, edit.Scope, piece);
            Assert.That(m_draft.Snapshot().BlockCount, Is.EqualTo(1));
            Assert.That(piece.Cells.Count, Is.EqualTo(2));
            Assert.That(m_draft.Snapshot().GetBlockAt(0).Key, Is.EqualTo(piece.Key));
        }

        [Test]
        public void TemplateStrokeAndMetadataUndoTogether()
        {
            m_draft.Load(Level(Block("b1", s_corner)));
            m_tool.SelectPen();
            m_controller.StartPenEdit(k_guid, 0, m_tool);
            LevelPieceEdit edit = m_controller.Draw(Source(), m_tool, new[] { new Vector2Int(2, 2) });
            Assert.That(edit.Level.GetBlockAt(0).CellCount, Is.EqualTo(4));
            Assert.That(m_draft.Draft.TemplateEdits, Is.Empty);
            Apply(edit);
            Assert.That(m_draft.Draft.TemplateEdits, Has.Count.EqualTo(1));
            Assert.That(FindTemplate(m_controller.GetEditCatalog(k_guid)).Cells.Count, Is.EqualTo(4));
            Undo.PerformUndo();
            Assert.That(m_draft.Snapshot().GetBlockAt(0).CellCount, Is.EqualTo(3));
            Assert.That(m_draft.Draft.TemplateEdits, Is.Empty);
        }

        [Test]
        public void NamedExternalChangeJoinsUndoAndMirrorReadsRestoredBlock()
        {
            string scope = Source().GetScope(m_draft.Snapshot());
            LevelNamedPiece piece = m_session.AddNamedPiece(scope, "Named corner");
            piece.SetCells(s_corner);
            piece.BlockId = "b1";
            piece.SetMembers(new[] { "b1" });
            m_draft.Load(Level(Block("b1", s_corner)));
            string token = piece.Token;
            var request = new LevelPieceRequest(scope, token, s_square, piece.DisplayName);
            Undo.IncrementCurrentGroup();
            LevelPieceEditorEdit edit = m_controller.PlanEditorChange(Source(), request);
            Assert.That(m_draft.ApplyJoined(edit.Before, edit.After), Is.True);
            m_controller.RefreshCustomPieces(Source(), m_draft.Snapshot());
            m_controller.CompleteEditorChange(k_guid, edit);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            Assert.That(m_controller.GetUndoMirror(k_guid, scope), Is.Null);
            Undo.PerformUndo();
            m_controller.RefreshCustomPieces(Source(), m_draft.Snapshot());
            LevelPieceRequest mirror = m_controller.GetUndoMirror(k_guid, scope);
            Assert.That(mirror.Cells.Count, Is.EqualTo(3));
            Assert.That(mirror.PieceId, Is.EqualTo(token));
            m_controller.RememberMirror(mirror);
            Assert.That(m_controller.GetUndoMirror(k_guid, scope), Is.Null);
            Assert.That(m_controller.GetUndoMirror(k_guid, "other-scope"), Is.Null);
        }

        [Test]
        public void DeletingUnplacedPieceClearsSessionWithoutChangingDraft()
        {
            LevelNamedPiece piece = m_controller.CreateNamedPiece(Source());
            piece.SetCells(s_corner);
            string before = JsonUtility.ToJson(m_draft.Snapshot());
            LevelPieceEdit edit = m_controller.DeleteCustomPiece(piece.Scope,
                new LevelShapeChoice(piece.DisplayName, piece.Cells), 0);
            Assert.That(edit.Level, Is.Null);
            Assert.That(m_session.DrawingPiece, Is.Null);
            Assert.That(m_session.NamedPieces, Is.Empty);
            Assert.That(JsonUtility.ToJson(m_draft.Snapshot()), Is.EqualTo(before));
        }

        private void Apply(LevelPieceEdit edit)
        {
            Assert.That(m_draft.Apply(edit.UndoName, edit.Level, edit.ApplyMetadata), Is.True);
        }

        // The corner template in a catalog, found by the key the library holds for it, with or without an overlay.
        private static LevelShapeChoice FindTemplate(LevelShapeCatalog catalog)
        {
            return catalog.Get(catalog.IndexOfTemplate(s_cornerKey, null));
        }

        private LevelPieceSource Source()
        {
            return new LevelPieceSource(null, k_guid, null, 0, true);
        }

        private static LevelBlockData Block(string id, Vector2Int[] cells)
        {
            return new LevelBlockData(id, "Red", Vector2Int.one, cells);
        }

        private static LevelData Level(params LevelBlockData[] blocks)
        {
            return new LevelData("controller-level", "Controller level", 6, 6, 60,
                Array.Empty<LevelDoorData>(), blocks);
        }
    }
}
