using System;
using System.Collections.Generic;
using System.Reflection;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>Exercises save ordering and failure boundaries with transient owners and injected writes/commits.</summary>
    public sealed class LevelSaveControllerTests
    {
        private const string k_guid = "save-controller-fixture";
        private const string k_templateName = "Save corner";
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_corner = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };
        private static readonly Vector2Int[] s_square =
            { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.one };
        private static readonly Vector2Int[] s_domino = { Vector2Int.zero, Vector2Int.right };
        // Declared after the cells, so the static initializer reads them.
        private static readonly ShapeKey s_cornerKey = LevelTestShapes.Key(s_corner);
        private LevelDraftController m_draft;
        private LevelPieceSession m_session;
        private LevelPieceEditController m_pieces;
        private LevelSaveController m_save;
        private BlockShapeLibrary m_library;
        private string m_sourceJson;
        private LevelDatabaseSnapshot m_source;

        [SetUp]
        public void CreateOwners()
        {
            LevelData baseline = Level("save-level", Block("b1", Vector2Int.one, s_corner));
            Assert.That(LevelDatabaseJson.TryWrite(new[] { baseline }, out m_sourceJson, out string error), Is.True, error);
            m_draft = new LevelDraftController();
            m_draft.Load(baseline);
            m_session = new LevelPieceSession();
            m_pieces = new LevelPieceEditController(m_draft, m_session);
            m_save = new LevelSaveController(m_draft, m_pieces);
            // Seed the exact loaded-source snapshot without registering a database or touching Addressables.
            Assert.That(m_save.Levels.LoadJson(m_sourceJson, out error), Is.True, error);
            SetSourceField("m_sourceValid", true);
            SetSourceField("m_loadedGuid", k_guid);
            m_source = LevelEditorTestAccess.CreateUnsavedSource(m_sourceJson);
            SetSourceField("m_loadedSource", m_source);
            m_library = ScriptableObject.CreateInstance<BlockShapeLibrary>();
            m_library.hideFlags = HideFlags.HideAndDontSave;
            m_library.Load(BlockShapeLibraryJson.Write(new[]
            {
                new BlockShapeLibrary.Shape("Single", s_single),
                new BlockShapeLibrary.Shape(k_templateName, s_corner)
            }));
            m_pieces.Library = m_library;
            m_pieces.RefreshCatalog(true);
        }

        [TearDown]
        public void DestroyOwners()
        {
            m_draft.Dispose();
            UnityEngine.Object.DestroyImmediate(m_library);
        }

        [Test]
        public void InvalidOrDuplicateDraftRefusesBeforePlanningOrWriting()
        {
            m_draft.Load(new LevelData("invalid", "Invalid", 0, 0, 0,
                Array.Empty<LevelDoorData>(), Array.Empty<LevelBlockData>()));
            LevelSaveValidation invalid = m_save.Validate(0);
            Assert.That(invalid.Candidate, Is.Null);
            Assert.That(invalid.Error, Is.Not.Empty);
            Assert.That(invalid.ShowDialog, Is.True);

            LevelData first = m_save.Levels.GetAt(0);
            LevelData second = Level("another-level", Block("b2", Vector2Int.one, s_corner));
            Assert.That(LevelDatabaseJson.TryWrite(new[] { first, second }, out string json, out string error),
                Is.True, error);
            Assert.That(m_save.Levels.LoadJson(json, out error), Is.True, error);
            m_draft.Load(second);
            LevelSaveValidation duplicate = m_save.Validate(0);
            Assert.That(duplicate.Candidate, Is.Null);
            Assert.That(duplicate.Error, Is.EqualTo("Level ID another-level is already in the database."));
            Assert.That(m_save.LoadedSource, Is.SameAs(m_source));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnwrittenOrUnreloadedFileStopsAllPostWriteState(bool jsonWritten)
        {
            PrepareTemplateAndNewPiece();
            string before = JsonUtility.ToJson(m_draft.Snapshot());
            string baseline = JsonUtility.ToJson(m_draft.GetSavedLevel());
            int writes = 0;
            int commits = 0;
            int notifications = 0;
            LevelSaveResult result = m_save.Save(Plan(), "Fixture.json", _ =>
                {
                    writes++;
                    if (jsonWritten)
                    {
                        SetSourceField("m_sourceValid", false);
                    }

                    return new LevelFileWriteResult(jsonWritten, false, "Injected file failure");
                }, _ => { commits++; return null; }, _ => notifications++);
            Assert.That(writes, Is.EqualTo(1));
            Assert.That(commits, Is.Zero);
            Assert.That(notifications, Is.Zero);
            Assert.That(result.File.JsonWritten, Is.EqualTo(jsonWritten));
            Assert.That(result.File.Reloaded, Is.False);
            Assert.That(result.LibraryCommitted, Is.False);
            Assert.That(m_save.SourceValid, Is.EqualTo(!jsonWritten));
            Assert.That(m_save.LoadedSource, Is.SameAs(m_source));
            Assert.That(JsonUtility.ToJson(m_draft.Snapshot()), Is.EqualTo(before));
            Assert.That(JsonUtility.ToJson(m_draft.GetSavedLevel()), Is.EqualTo(baseline));
            Assert.That(m_draft.Draft.TemplateEdits, Has.Count.EqualTo(1));
            Assert.That(m_session.GetPendingTemplates(k_guid), Is.Empty);
        }

        [Test]
        public void SavePublishesBaselineBeforeTemplateCommitAndAddsNoLevelPiece()
        {
            LevelNamedPiece named = PrepareTemplateAndNewPiece();
            LevelSavePlan plan = Plan();
            List<string> order = new List<string>();
            LevelSaveResult result = m_save.Save(plan, "Fixture.json", levels =>
                {
                    order.Add("write");
                    Assert.That(levels, Has.Count.EqualTo(1));
                    Assert.That(m_draft.Draft.TemplateEdits, Has.Count.EqualTo(1));
                    Assert.That(m_session.GetPendingTemplates(k_guid), Is.Empty);
                    return Written();
                }, edits =>
                {
                    order.Add("templates");
                    Assert.That(edits, Has.Count.EqualTo(1));
                    Assert.That(m_draft.Draft.TemplateEdits, Is.Empty);
                    Assert.That(m_session.GetPendingTemplates(k_guid), Has.Count.EqualTo(1));
                    Assert.That(JsonUtility.ToJson(m_draft.GetSavedLevel()), Is.EqualTo(JsonUtility.ToJson(plan.Level)));
                    return null;
                }, progress =>
                {
                    if (progress.Stage == LevelSaveStage.LevelWritten)
                    {
                        order.Add("saved view");
                        Assert.That(m_draft.HasChanges(m_pieces.Catalog), Is.False);
                    }
                });
            // A level piece stays level-only (DEC-180): Save Level adds nothing to the piece library for it.
            Assert.That(order, Is.EqualTo(new[] { "write", "saved view", "templates" }));
            Assert.That(result.File.Reloaded, Is.True);
            Assert.That(result.LibraryCommitted, Is.True);
            Assert.That(m_session.NamedPieces, Is.EqualTo(new[] { named }), "The level piece stays in the session.");
            Assert.That(LevelPieceNames.Find(m_draft.GetSavedLevel(), named.Key), Is.EqualTo(named.DisplayName));
        }

        [Test]
        public void TemplateFailureKeepsSavedStateUntilRetry()
        {
            PrepareTemplateAndNewPiece();
            LevelSaveResult first = m_save.Save(Plan(), "Fixture.json", _ => Written(), _ => "Template unavailable",
                null);
            Assert.That(first.File.Reloaded, Is.True);
            Assert.That(first.LibraryCommitted, Is.False);
            Assert.That(m_draft.Draft.TemplateEdits, Is.Empty);
            Assert.That(m_session.GetPendingTemplates(k_guid), Has.Count.EqualTo(1));
            Assert.That(m_session.NamedPieces, Has.Count.EqualTo(1));

            LevelSavePlan retry = Plan();
            Assert.That(retry.Templates.Unfit, Is.Empty);
            Assert.That(retry.Templates.Restamped, Is.Empty, "Already written levels are not re-stamped again.");
            LevelSaveResult second = m_save.Save(retry, "Fixture.json", _ => Written(), _ => null, null);
            Assert.That(second.LibraryCommitted, Is.True);
            Assert.That(m_session.GetPendingTemplates(k_guid), Is.Empty);
            Assert.That(m_session.NamedPieces, Has.Count.EqualTo(1), "The level piece stays level-only.");
        }

        [Test]
        public void ReloadDropsOnlyUnheldPendingFootprintsOfItsDatabase()
        {
            LevelTemplateEdit edit = new LevelTemplateEdit(s_cornerKey, k_templateName, s_corner, s_square);
            m_session.SetPendingTemplate(k_guid, edit);
            m_session.SetPendingTemplate("other-database", edit);
            string note = m_save.ForgetUnheldTemplates();
            Assert.That(note, Does.Contain("no level").IgnoreCase);
            Assert.That(m_session.GetPendingTemplates(k_guid), Is.Empty);
            Assert.That(m_session.GetPendingTemplates("other-database"), Has.Count.EqualTo(1));
            Assert.That(m_save.LoadSource(null, Array.Empty<LevelDatabaseAddressables.Entry>(), out string error), Is.False);
            Assert.That(error, Is.EqualTo("The active level database is missing. Select a database or create one."));
            Assert.That(m_save.SourceValid, Is.False);
            Assert.That(m_save.LoadedSource, Is.Null);
            Assert.That(m_save.LoadedGuid, Is.Null);
        }

        private LevelNamedPiece PrepareTemplateAndNewPiece()
        {
            LevelNamedPiece named = m_session.AddNamedPiece(LevelPieceSession.GetLevelScope(k_guid, "save-level"),
                "New domino");
            named.SetCells(s_domino);
            LevelData changed = Level("save-level", Block("b1", Vector2Int.one, s_square),
                Block("b2", new Vector2Int(4, 1), s_domino));
            m_draft.Apply("Edit template and add piece", changed,
                draft => draft.SetTemplateEdit(new LevelTemplateEdit(s_cornerKey, k_templateName, s_corner, s_square)));
            return named;
        }

        private LevelSavePlan Plan()
        {
            LevelSaveValidation validation = m_save.Validate(0);
            Assert.That(validation.Error, Is.Null);
            Assert.That(validation.Candidate, Is.Not.Null);
            LevelSavePlan plan = m_save.Prepare(0, validation.Candidate);
            if (plan.Templates != null)
            {
                Assert.That(plan.Templates.Refusals, Is.Empty);
            }

            return plan;
        }

        private void SetSourceField(string name, object value)
        {
            typeof(LevelSaveController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(m_save, value);
        }

        private static LevelFileWriteResult Written()
        {
            return new LevelFileWriteResult(true, true, null);
        }

        private static LevelBlockData Block(string id, Vector2Int origin, Vector2Int[] cells)
        {
            return new LevelBlockData(id, "Red", origin, cells);
        }

        private static LevelData Level(string id, params LevelBlockData[] blocks)
        {
            return new LevelData(id, "Save fixture", 7, 7, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red") }, blocks);
        }
    }
}
