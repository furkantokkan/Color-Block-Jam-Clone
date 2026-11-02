using System;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>Exercises draft ownership directly, using only transient objects and this fixture's reload key.</summary>
    public sealed class LevelDraftControllerTests
    {
        private LevelDraftController m_controller;
        private int m_reloadId;

        [SetUp]
        public void LoadDraft()
        {
            m_controller = new LevelDraftController();
            m_controller.Load(Level(60));
            m_reloadId = m_controller.Draft.GetInstanceID();
        }

        [TearDown]
        public void ClearDraft()
        {
            LevelDraft.TryTakeReloadCopy(m_reloadId, out _);
            m_controller.Dispose();
        }

        [Test]
        public void LoadingAnotherLevelReplacesOnlyTheOwnedTransientTarget()
        {
            LevelDraft first = m_controller.Draft;
            LevelData authored = Level(90);
            m_controller.Load(authored);

            Assert.That(first == null, Is.True, "The old Unity object was destroyed.");
            Assert.That(m_controller.Draft.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
            Assert.That(m_controller.SerializedDraft.targetObject, Is.SameAs(m_controller.Draft));
            Assert.That(m_controller.Snapshot(), Is.Not.SameAs(authored));
            m_controller.SetString("m_level.m_name", "Edited", "Rename Level");
            Assert.That(authored.Name, Is.EqualTo("Draft fixture"), "The authored input is never mutated.");

            LevelDraft target = m_controller.Draft;
            m_controller.Clear();
            Assert.That(target == null, Is.True, "Clear destroys only this owner's transient target.");
            Assert.That(m_controller.Draft, Is.Null);
            Assert.That(m_controller.SerializedDraft, Is.Null);
            Assert.That(m_controller.GetSavedLevel(), Is.Null);
            Assert.That(m_controller.Snapshot(), Is.Null);
            Assert.That(m_controller.IsOutOfSync(), Is.False);
            Assert.That(m_controller.Apply("No target", Level(80)), Is.False);
            Assert.DoesNotThrow(m_controller.Dispose);
        }

        [Test]
        public void IndependentEditsAndTheirMetadataUndoOneGestureAtATime()
        {
            LevelTemplateEdit edit = TemplateEdit();
            m_controller.Apply("First gesture", Level(70), draft => draft.SetTemplateEdit(edit));
            m_controller.Apply("Second gesture", Level(80));

            Undo.PerformUndo();
            Assert.That(m_controller.Snapshot().TimerSeconds, Is.EqualTo(70));
            Assert.That(m_controller.Draft.TemplateEdits, Has.Count.EqualTo(1));
            Undo.PerformUndo();
            Assert.That(m_controller.Snapshot().TimerSeconds, Is.EqualTo(60));
            Assert.That(m_controller.Draft.TemplateEdits, Is.Empty);
            Undo.PerformRedo();
            Assert.That(m_controller.Snapshot().TimerSeconds, Is.EqualTo(70));
            Assert.That(m_controller.Draft.TemplateEdits, Has.Count.EqualTo(1));
        }

        [Test]
        public void MovingTheSavedBaselineKeepsTheDraftAndItsUndoHistory()
        {
            LevelDraft target = m_controller.Draft;
            SerializedObject binding = m_controller.SerializedDraft;
            m_controller.Apply("Saved edit", Level(70));
            m_controller.SetSavedBaseline(m_controller.Snapshot());

            Assert.That(m_controller.Draft, Is.SameAs(target));
            Assert.That(m_controller.SerializedDraft, Is.SameAs(binding));
            Assert.That(m_controller.HasChanges(LevelShapeCatalog.Missing), Is.False);
            Undo.PerformUndo();
            Assert.That(m_controller.Snapshot().TimerSeconds, Is.EqualTo(60));
            Assert.That(m_controller.GetSavedLevel().TimerSeconds, Is.EqualTo(70));
            Assert.That(m_controller.HasChanges(LevelShapeCatalog.Missing), Is.True);
        }

        [Test]
        public void JoinedEditUsesTheCallersGroupAndRevertsBothTargetsTogether()
        {
            LevelDraft other = ScriptableObject.CreateInstance<LevelDraft>();
            other.Load(Level(60));
            try
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.RecordObject(other, "Piece Editor edit");
                other.Replace(Level(90));
                Assert.That(m_controller.ApplyJoined(m_controller.Snapshot(), Level(70)), Is.True);
                Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(group));
                Assert.That(m_controller.ApplyJoined(m_controller.Snapshot(), m_controller.Snapshot()), Is.False,
                    "An unchanged Piece Editor result records nothing.");
                Assert.That(m_controller.ApplyJoined(m_controller.Snapshot(), null), Is.False);
                Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(group));
                Undo.FlushUndoRecordObjects();
                Undo.IncrementCurrentGroup();

                Undo.PerformUndo();
                Assert.That(other.Snapshot().TimerSeconds, Is.EqualTo(60));
                Assert.That(m_controller.Snapshot().TimerSeconds, Is.EqualTo(60));
            }
            finally
            {
                Undo.ClearUndo(other);
                UnityEngine.Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void ColourOrderNeedsASyncButDoesNotMakeTheJsonDirty()
        {
            m_controller.ReadAndMarkSynchronized();
            Assert.That(m_controller.IsOutOfSync(), Is.False);
            m_controller.Draft.SetColorOrder(LevelColorOrder.Default.Swap(PaletteColor.Red, PaletteColor.Blue));

            Assert.That(m_controller.IsOutOfSync(), Is.True);
            Assert.That(m_controller.HasChanges(LevelShapeCatalog.Missing), Is.False);
            m_controller.ReadAndMarkSynchronized();
            Assert.That(m_controller.IsOutOfSync(), Is.False);

            LevelTemplateEdit edit = TemplateEdit();
            m_controller.Draft.SetTemplateEdit(edit);
            Assert.That(m_controller.HasChanges(LevelShapeCatalog.Missing), Is.True,
                "An uncommitted footprint makes the unchanged level dirty.");
            LevelShapeCatalog library = LevelShapeCatalog.Create(new[]
            {
                LevelTestShapes.Template(edit.DisplayName, edit.Cells)
            });
            Assert.That(m_controller.HasChanges(library), Is.False,
                "A footprint the library already holds is not unsaved.");
        }

        [Test]
        public void MatchingReloadRestoresTheLevelMetadataAndSavedBaselineOnce()
        {
            LevelDraftReloadContext context = Context();
            m_controller.Apply("Before reload", Level(70), draft =>
            {
                draft.SetTemplateEdit(TemplateEdit());
                draft.SetColorOrder(LevelColorOrder.Default.Swap(PaletteColor.Red, PaletteColor.Blue));
            });
            m_controller.KeepForReload(context);
            m_controller.Load(Level(60));

            Assert.That(m_controller.RestoreAfterReload(context), Is.Null);
            Assert.That(m_controller.Snapshot().TimerSeconds, Is.EqualTo(70));
            Assert.That(m_controller.GetSavedLevel().TimerSeconds, Is.EqualTo(60));
            Assert.That(m_controller.Draft.TemplateEdits, Has.Count.EqualTo(1));
            Assert.That(m_controller.Draft.ColorOrder.IsDefault, Is.False);
            Assert.That(m_controller.SerializedDraft.FindProperty("m_level.m_timerSeconds").intValue, Is.EqualTo(70));
            Assert.That(LevelDraft.TryTakeReloadCopy(m_reloadId, out _), Is.False);
            m_controller.Apply("After reload", Level(80));
            Undo.PerformUndo();
            Assert.That(m_controller.Snapshot().TimerSeconds, Is.EqualTo(70));
        }

        [Test]
        public void NoOpFieldEditsStillRefreshTheWindowControls()
        {
            LevelDatabaseWindow window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            try
            {
                window.Show();
                LevelEditorTestAccess.Invoke(window, "CreateGUI");
                LevelDraftController owner = LevelEditorTestAccess.Get<LevelDraftController>(window, "m_draftController");
                LevelEditorTestAccess.Invoke(window, "DestroyDraft");
                owner.Load(Level(60));
                LevelEditorTestAccess.Invoke(window, "OnDraftChanged");
                TextField name = window.rootVisualElement.Q<TextField>("level-name");
                IntegerField timer = window.rootVisualElement.Q<IntegerField>("timer-seconds");
                name.SetValueWithoutNotify("Stale name");
                timer.SetValueWithoutNotify(999);

                LevelEditorTestAccess.Invoke(window, "SetDraftString", "m_level.m_name", "Draft fixture");
                Assert.That(name.value, Is.EqualTo("Draft fixture"));
                timer.SetValueWithoutNotify(999);
                LevelEditorTestAccess.Invoke(window, "SetDraftInt", "m_level.m_timerSeconds", 60, "Set Level Timer");
                Assert.That(timer.value, Is.EqualTo(60));
                Assert.That(owner.HasChanges(LevelShapeCatalog.Missing), Is.False);
            }
            finally
            {
                window.DiscardChanges();
                window.Close();
            }
        }

        private LevelDraftReloadContext Context()
        {
            return new LevelDraftReloadContext(m_reloadId, "fixture-guid", 0, "draft-fixture", "source-json");
        }

        private static LevelTemplateEdit TemplateEdit()
        {
            return new LevelTemplateEdit(LevelTestShapes.Key(new[] { Vector2Int.zero }), "Draft fixture piece",
                new[] { Vector2Int.zero }, new[] { Vector2Int.zero, Vector2Int.right });
        }

        private static LevelData Level(int seconds)
        {
            return new LevelData("draft-fixture", "Draft fixture", 3, 3, seconds, Array.Empty<LevelDoorData>(),
                new[] { new LevelBlockData("b1", "Red", Vector2Int.one, new[] { Vector2Int.zero }) });
        }
    }
}
