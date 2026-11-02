using System;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.BoardKit
{
    // Native Undo stores JSON snapshots only in transient Editor memory.
    [InitializeOnLoad]
    internal sealed class ShapeLibraryUndoState : ScriptableObject
    {
        [SerializeField] private BlockShapeLibrary m_library;
        [SerializeField] private string m_json;
        [NonSerialized] private string m_appliedJson;

        static ShapeLibraryUndoState() { Undo.undoRedoPerformed += Replay; }
        private void OnEnable() { m_appliedJson = m_json; }

        public static void Record(BlockShapeLibrary library, string previous, string next, string label)
        {
            if (previous == next) return;
            ShapeLibraryUndoState state = null;
            foreach (var item in Resources.FindObjectsOfTypeAll<ShapeLibraryUndoState>())
                if (item.m_library == library) { state = item; break; }
            if (state == null)
            {
                state = CreateInstance<ShapeLibraryUndoState>();
                state.hideFlags = HideFlags.HideAndDontSave;
                state.m_library = library;
            }
            state.m_json = previous;
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(state, label);
            state.m_json = next;
            state.m_appliedJson = next;
            Undo.IncrementCurrentGroup();
        }

        public static void Replay()
        {
            foreach (var state in Resources.FindObjectsOfTypeAll<ShapeLibraryUndoState>())
            {
                if (state.m_library == null) { DestroyImmediate(state); continue; }
                if (state.m_json == state.m_appliedJson) continue;
                try
                {
                    ShapeLibraryAuthoring.Commit(state.m_library, BlockShapeLibraryJson.Read(state.m_json),
                        "Restore piece JSON", false, state.m_appliedJson);
                    state.m_json = state.m_library.LoadedJson;
                    state.m_appliedJson = state.m_json;
                }
                catch (Exception error)
                {
                    state.m_json = state.m_appliedJson;
                    Debug.LogError("Could not restore piece JSON: " + error.Message);
                }
            }
        }

        public static void Clear(BlockShapeLibrary library)
        {
            foreach (var state in Resources.FindObjectsOfTypeAll<ShapeLibraryUndoState>())
                if (state.m_library == library) { Undo.ClearUndo(state); DestroyImmediate(state); }
        }
    }
}
