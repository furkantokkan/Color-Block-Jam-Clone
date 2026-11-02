using System;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Owns one transient draft, its binding object and its saved/synchronized baselines. The window owns bindings,
    /// subscriptions and repainting; this owner makes every accepted edit use the same Undo target.
    /// </summary>
    internal sealed class LevelDraftController : IDisposable
    {
        private LevelDraft m_draft;
        private SerializedObject m_draftObject;
        private string m_savedDraftJson;
        private string m_syncedDraftJson;

        internal LevelDraft Draft => m_draft;
        internal SerializedObject SerializedDraft => m_draftObject;

        internal void Load(LevelData level)
        {
            Clear();
            m_draft = ScriptableObject.CreateInstance<LevelDraft>();
            m_draft.hideFlags = HideFlags.HideAndDontSave;
            m_draft.Load(level);
            SetSavedBaseline(m_draft.Snapshot());
            m_draftObject = new SerializedObject(m_draft);
        }

        internal LevelData Snapshot()
        {
            return m_draft != null ? m_draft.Snapshot() : null;
        }

        internal LevelData GetSavedLevel()
        {
            return m_savedDraftJson != null ? JsonUtility.FromJson<LevelData>(m_savedDraftJson) : null;
        }

        internal void SetSavedBaseline(LevelData level)
        {
            m_savedDraftJson = level != null ? JsonUtility.ToJson(level) : null;
        }

        /// <summary>
        /// The level as Save Level wrote it becomes the draft and its saved baseline, without an undo step: the saved
        /// level differs from the draft only by the piece names Save Level resolved (DEC-193), so the draft reads clean
        /// after the save while its undo history stays.
        /// </summary>
        internal void SetSavedLevel(LevelData level)
        {
            if (m_draft == null || level == null)
            {
                return;
            }

            m_draft.Replace(level);
            m_draftObject?.Update();
            SetSavedBaseline(level);
        }

        internal bool HasChanges(LevelShapeCatalog library)
        {
            if (m_draft == null || m_savedDraftJson == null)
            {
                return false;
            }

            if (!string.Equals(JsonUtility.ToJson(m_draft.Snapshot()), m_savedDraftJson, StringComparison.Ordinal))
            {
                return true;
            }

            // Colour order alone changes editor presentation, not the authored JSON. An uncommitted template does.
            for (int index = 0; index < m_draft.TemplateEdits.Count; index++)
            {
                LevelTemplateEdit edit = m_draft.TemplateEdits[index];
                int record = library.IndexOfTemplate(edit.TemplateKey, edit.DisplayName);
                if (record < 0 || library.Get(record).Key != edit.Key)
                {
                    return true;
                }
            }

            return false;
        }

        internal bool IsOutOfSync()
        {
            if (m_draftObject == null)
            {
                return false;
            }

            m_draftObject.Update();
            return !string.Equals(GetSignature(m_draft.Snapshot()), m_syncedDraftJson, StringComparison.Ordinal);
        }

        internal LevelData ReadAndMarkSynchronized()
        {
            m_draftObject?.Update();
            LevelData level = Snapshot();
            m_syncedDraftJson = GetSignature(level);
            return level;
        }

        private string GetSignature(LevelData level)
        {
            return level != null
                ? JsonUtility.ToJson(level) + m_draft.GetTemplateEditsText() + m_draft.GetColorOrderText()
                : null;
        }

        internal void SetInt(string propertyPath, int value, string undoName)
        {
            if (m_draftObject == null)
            {
                return;
            }

            m_draftObject.Update();
            m_draftObject.FindProperty(propertyPath).intValue = value;
            ApplyFields(undoName);
        }

        internal void SetString(string propertyPath, string value, string undoName)
        {
            if (m_draftObject == null)
            {
                return;
            }

            m_draftObject.Update();
            m_draftObject.FindProperty(propertyPath).stringValue = value;
            ApplyFields(undoName);
        }

        private void ApplyFields(string undoName)
        {
            if (m_draftObject.ApplyModifiedProperties())
            {
                Undo.SetCurrentGroupName(undoName);
            }
        }

        /// <summary>One independent gesture, with both the level and its template/colour metadata in one Undo.</summary>
        internal bool Apply(string undoName, LevelData level, Action<LevelDraft> alsoApply = null)
        {
            if (m_draft == null)
            {
                return false;
            }

            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(m_draft, undoName);
            m_draft.Replace(level);
            alsoApply?.Invoke(m_draft);
            Undo.SetCurrentGroupName(undoName);
            Undo.IncrementCurrentGroup();
            return true;
        }

        /// <summary>Joins the Piece Editor's open Undo group; never opens or closes a group of its own.</summary>
        internal bool ApplyJoined(LevelData current, LevelData result)
        {
            if (m_draft == null || result == null
                || string.Equals(JsonUtility.ToJson(result), JsonUtility.ToJson(current), StringComparison.Ordinal))
            {
                return false;
            }

            Undo.RecordObject(m_draft, "Edit Level Piece");
            m_draft.Replace(result);
            return true;
        }

        internal void KeepForReload(LevelDraftReloadContext context)
        {
            LevelData draft = m_draft.Snapshot();
            LevelDraft.KeepForReload(context.WindowId, new LevelDraft.ReloadCopy(
                context.DatabaseGuid, context.LevelIndex, context.LevelId, context.SourceHash, m_savedDraftJson,
                JsonUtility.ToJson(draft), m_draft.TemplateEdits, m_draft.ColorOrder.ToValues()));
            Debug.Log($"Level Editor: kept the unsaved changes to {draft?.Name} through the editor reload. " +
                      "Save Level writes them to the JSON source.");
        }

        /// <summary>Consumes this window's reload copy. A mismatch keeps the loaded level and returns its reason.</summary>
        internal string RestoreAfterReload(LevelDraftReloadContext context)
        {
            if (!LevelDraft.TryTakeReloadCopy(context.WindowId, out LevelDraft.ReloadCopy copy))
            {
                return null;
            }

            string mismatch = context.GetMismatch(copy);
            if (mismatch != null)
            {
                string message = $"{mismatch}, so the unsaved level changes kept through it were discarded.";
                Debug.LogWarning($"Level Editor: {message}");
                return message;
            }

            m_draft.Load(JsonUtility.FromJson<LevelData>(copy.DraftJson));
            m_draft.SetTemplateEdits(copy.TemplateEdits);
            m_draft.SetColorOrder(LevelColorOrder.TryCreate(copy.ColorOrder, out LevelColorOrder order)
                ? order
                : LevelColorOrder.Default);
            m_savedDraftJson = copy.SavedJson;
            m_draftObject.Update();
            return null;
        }

        /// <summary>The window unbinds its tracker before clearing; no Undo record may retain a destroyed draft.</summary>
        internal void Clear()
        {
            m_draftObject?.Dispose();
            m_draftObject = null;
            if (m_draft != null)
            {
                Undo.ClearUndo(m_draft);
                UnityEngine.Object.DestroyImmediate(m_draft);
                m_draft = null;
            }

            m_savedDraftJson = null;
            m_syncedDraftJson = null;
        }

        public void Dispose()
        {
            Clear();
        }
    }

    /// <summary>The identity of the saved source a reload copy belongs to; it owns no database or draft state.</summary>
    internal readonly struct LevelDraftReloadContext
    {
        internal int WindowId { get; }
        internal string DatabaseGuid { get; }
        internal int LevelIndex { get; }
        internal string LevelId { get; }
        internal string SourceHash { get; }

        /// <param name="sourceHash">The loaded database's content hash (LevelDatabaseSnapshot.ContentHash), or null.</param>
        internal LevelDraftReloadContext(int windowId, string databaseGuid, int levelIndex, string levelId,
            string sourceHash)
        {
            WindowId = windowId;
            DatabaseGuid = databaseGuid;
            LevelIndex = levelIndex;
            LevelId = levelId;
            SourceHash = sourceHash ?? string.Empty;
        }

        internal string GetMismatch(LevelDraft.ReloadCopy copy)
        {
            if (!string.Equals(copy.DatabaseGuid, DatabaseGuid, StringComparison.Ordinal))
            {
                return "Another level database is active after the editor reload";
            }

            if (copy.LevelIndex != LevelIndex || !string.Equals(copy.LevelId, LevelId, StringComparison.Ordinal))
            {
                return "Another level is selected after the editor reload";
            }

            return string.Equals(copy.SourceHash, SourceHash, StringComparison.Ordinal)
                ? null
                : "The level JSON changed on disk during the editor reload";
        }
    }
}
