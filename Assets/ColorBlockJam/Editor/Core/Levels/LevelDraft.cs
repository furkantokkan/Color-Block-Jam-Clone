using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Unsaved editor copy of one database entry (its level piece names included, DEC-193), its pending template
    /// footprints and its colour order, the single undo target of the Level Editor; an unsaved copy can be kept in the
    /// editor session across a script or domain reload.
    /// </summary>
    internal sealed class LevelDraft : ScriptableObject
    {
        // One key per Level Editor window: its instance ID survives a domain reload, and SessionState survives a
        // domain reload but not an Editor restart.
        private const string k_reloadKeyPrefix = "ColorBlockJam.LevelEditor.ReloadDraft.";

        [SerializeField] private LevelData m_level;
        // Pen template edits not saved yet (DEC-125). They sit on the undo target, so one undo step moves a stroke's
        // blocks and its pending footprint together.
        [SerializeField] private List<LevelTemplateEdit> m_templateEdits = new List<LevelTemplateEdit>();
        // The colour order (TASK-20260926-145804549-da16f02f) sits on the undo target too, so one undo step moves a
        // recolour and its order together. Empty means palette order; it is never written to the level JSON.
        [SerializeField] private int[] m_colorOrder = Array.Empty<int>();

        /// <summary>The pending template footprints of this draft, one per template key.</summary>
        public IReadOnlyList<LevelTemplateEdit> TemplateEdits => m_templateEdits;

        /// <summary>The draft's colour order; palette order until Color Shuffle or Color Switch changes it.</summary>
        public LevelColorOrder ColorOrder =>
            LevelColorOrder.TryCreate(m_colorOrder, out LevelColorOrder order) ? order : LevelColorOrder.Default;

        /// <summary>
        /// Loads a saved level; a loaded level has no pending template footprint and starts in palette order.
        /// </summary>
        public void Load(LevelData level)
        {
            m_level = level?.Clone();
            m_templateEdits.Clear();
            m_colorOrder = Array.Empty<int>();
        }

        /// <summary>Sets the colour order. The caller records the undo step on this object first, as for Replace.</summary>
        internal void SetColorOrder(LevelColorOrder order)
        {
            m_colorOrder = order == null || order.IsDefault ? Array.Empty<int>() : order.ToValues();
        }

        /// <summary>The colour order as text, so a change of it alone counts as a draft change; empty in palette order.</summary>
        internal string GetColorOrderText()
        {
            return m_colorOrder.Length == 0 ? string.Empty : "|order:" + string.Join(",", m_colorOrder);
        }

        /// <summary>The pending footprint of the template with this key, or null.</summary>
        public LevelTemplateEdit FindTemplateEdit(ShapeKey templateKey)
        {
            int index = IndexOfTemplateEdit(templateKey);
            return index >= 0 ? m_templateEdits[index] : null;
        }

        /// <summary>
        /// The pending footprint of a template: the edit with its library key, else the one with its name. A Save Level
        /// that wrote an edit's footprint gave the record new cells, so a new key, and an undo past that save brings
        /// back the edit keyed by the old one (DEC-193), as LevelShapeCatalog.IndexOfTemplate finds the record.
        /// </summary>
        public LevelTemplateEdit FindTemplateEdit(ShapeKey templateKey, string displayName)
        {
            LevelTemplateEdit edit = FindTemplateEdit(templateKey);
            for (int index = 0; edit == null && !string.IsNullOrWhiteSpace(displayName)
                                && index < m_templateEdits.Count; index++)
            {
                if (m_templateEdits[index] != null && string.Equals(m_templateEdits[index].DisplayName, displayName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    edit = m_templateEdits[index];
                }
            }

            return edit;
        }

        /// <summary>
        /// Adds or replaces the pending footprint of the edit's template. The caller records the undo step on this
        /// object first, as for <see cref="Replace"/>.
        /// </summary>
        internal void SetTemplateEdit(LevelTemplateEdit edit)
        {
            int index = IndexOfTemplateEdit(edit.TemplateKey);
            if (index >= 0)
            {
                m_templateEdits[index] = edit;
            }
            else
            {
                m_templateEdits.Add(edit);
            }
        }

        internal void RemoveTemplateEdit(ShapeKey templateKey)
        {
            int index = IndexOfTemplateEdit(templateKey);
            if (index >= 0)
            {
                m_templateEdits.RemoveAt(index);
            }
        }

        /// <summary>Replaces every pending footprint; Save Level moves them to the window's session this way.</summary>
        internal void SetTemplateEdits(IReadOnlyList<LevelTemplateEdit> edits)
        {
            m_templateEdits.Clear();
            for (int index = 0; edits != null && index < edits.Count; index++)
            {
                if (edits[index] != null && edits[index].TemplateKey.IsValid)
                {
                    m_templateEdits.Add(edits[index]);
                }
            }
        }

        /// <summary>The pending footprints as text, so a change of them alone counts as a draft change.</summary>
        internal string GetTemplateEditsText()
        {
            if (m_templateEdits.Count == 0)
            {
                return string.Empty;
            }

            string[] parts = new string[m_templateEdits.Count];
            for (int index = 0; index < parts.Length; index++)
            {
                parts[index] = "|" + JsonUtility.ToJson(m_templateEdits[index]);
            }

            return string.Concat(parts);
        }

        private int IndexOfTemplateEdit(ShapeKey templateKey)
        {
            for (int index = 0; templateKey.IsValid && index < m_templateEdits.Count; index++)
            {
                if (m_templateEdits[index] != null && m_templateEdits[index].TemplateKey == templateKey)
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Swaps in the result of an edit command. The caller records the undo step on this object first
        /// (docs/Level-Editor-Design.md section 4).
        /// </summary>
        internal void Replace(LevelData level)
        {
            m_level = level?.Clone();
        }

        public LevelData Snapshot()
        {
            return m_level?.Clone();
        }

        /// <summary>Keeps a window's unsaved draft until that window takes it back after the reload.</summary>
        internal static void KeepForReload(int windowId, ReloadCopy copy)
        {
            SessionState.SetString(k_reloadKeyPrefix + windowId, JsonUtility.ToJson(copy));
        }

        /// <summary>Returns and removes the draft a window kept before a reload; false when it kept none.</summary>
        internal static bool TryTakeReloadCopy(int windowId, out ReloadCopy copy)
        {
            string key = k_reloadKeyPrefix + windowId;
            string json = SessionState.GetString(key, string.Empty);
            copy = null;
            if (json.Length == 0)
            {
                return false;
            }

            SessionState.EraseString(key);
            copy = JsonUtility.FromJson<ReloadCopy>(json);
            return copy != null;
        }

        /// <summary>
        /// An unsaved draft kept across a reload, with what it was based on: the active database, the selected
        /// level, a hash of the JSON source text, the saved-baseline JSON of the dirty check, the draft's pending
        /// template footprints and its colour order.
        /// </summary>
        [Serializable]
        internal sealed class ReloadCopy
        {
            [SerializeField] private string m_databaseGuid;
            [SerializeField] private int m_levelIndex;
            [SerializeField] private string m_levelId;
            [SerializeField] private string m_sourceHash;
            [SerializeField] private string m_savedJson;
            [SerializeField] private string m_draftJson;
            [SerializeField] private List<LevelTemplateEdit> m_templateEdits = new List<LevelTemplateEdit>();
            // Empty in palette order and in a copy kept before the order existed.
            [SerializeField] private int[] m_colorOrder = Array.Empty<int>();

            public string DatabaseGuid => m_databaseGuid;
            public int LevelIndex => m_levelIndex;
            public string LevelId => m_levelId;
            public string SourceHash => m_sourceHash;
            public string SavedJson => m_savedJson;
            public string DraftJson => m_draftJson;
            public IReadOnlyList<LevelTemplateEdit> TemplateEdits => m_templateEdits;
            public IReadOnlyList<int> ColorOrder => m_colorOrder;

            // JsonUtility creates the copy through this constructor.
            public ReloadCopy()
            {
            }

            public ReloadCopy(
                string databaseGuid, int levelIndex, string levelId, string sourceHash, string savedJson,
                string draftJson, IReadOnlyList<LevelTemplateEdit> templateEdits = null,
                IReadOnlyList<int> colorOrder = null)
            {
                m_databaseGuid = databaseGuid;
                m_levelIndex = levelIndex;
                m_levelId = levelId;
                m_sourceHash = sourceHash;
                m_savedJson = savedJson;
                m_draftJson = draftJson;
                for (int index = 0; templateEdits != null && index < templateEdits.Count; index++)
                {
                    m_templateEdits.Add(templateEdits[index]);
                }

                m_colorOrder = new int[colorOrder?.Count ?? 0];
                for (int index = 0; index < m_colorOrder.Length; index++)
                {
                    m_colorOrder[index] = colorOrder[index];
                }
            }
        }
    }
}
