using System;
using System.Collections;
using System.Reflection;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// One shared Level Editor window harness for its window-fixture tests (Tests-Editor-1-F1, Tests-Editor-1-F3,
    /// Tests-Editor-1-F5, Tests-Editor-2-F2, Tests-Editor-3-F1). It owns the window and the published-level snapshot,
    /// opens the window through a direct CreateGUI call or through Unity's own build, and loads known levels into the
    /// window's own catalog so a test window never depends on the real project's active level JSON. A caller creates
    /// one per test (a field set in <c>[SetUp]</c> or a local inside the test) and disposes it in <c>[TearDown]</c> or
    /// a <c>finally</c> block; Dispose discards any pending edit, closes the window and restores the snapshot.
    /// Live-file guard: <see cref="LoadDatabase"/> changes only the test window's catalog (DEC-192) and keeps the
    /// window's real loaded JSON GUID, so a test that calls Save still targets the designer's own file path. That
    /// write is refused: the save controller writes only the database its read snapshot (<c>m_loadedSource</c>) came
    /// from, and this fixture sets that snapshot to the fixture's own JSON at a temporary path (DEC-196), so the
    /// mismatch reports the database as externally changed. The refusal opens the window's <c>ShowError</c> modal
    /// (<c>EditorUtility.DisplayDialog</c>), which blocks in an interactive Editor session, so no test in this suite
    /// calls Save on a fixture-loaded database.
    /// </summary>
    internal sealed class LevelEditorWindowFixture : IDisposable
    {
        // Upper bound for a window build or a scheduled publish; both finish within a few editor updates.
        private const double k_waitSeconds = 5.0;
        private const float k_defaultWidth = 960f;
        private const float k_defaultHeight = 640f;

        private readonly LevelData m_previousPublished;
        private LevelDatabaseWindow m_window;
        private bool m_disposed;

        internal LevelDatabaseWindow Window => m_window;

        internal LevelEditorWindowFixture()
        {
            m_previousPublished = LevelDatabaseWindow.LastPublishedLevel;
        }

        /// <summary>
        /// Creates a plain window and calls CreateGUI directly, as most window tests do. <paramref name="checksSolvability"/>
        /// turns on the Save Level solvability check (off by default, as a test window keeps it); an
        /// <paramref name="isPieceEditorOpen"/> override stands in for a Piece Editor the designer has open, mutable or
        /// constant as the caller needs. A caller whose fixture needs its own fake seams (WriteLevels,
        /// ConfirmPieceDelete, and so on) builds and configures the window itself and calls the <see cref="Open"/>
        /// overload that takes it.
        /// </summary>
        internal VisualElement Open(float width = k_defaultWidth, float height = k_defaultHeight,
            bool checksSolvability = false, Func<bool> isPieceEditorOpen = null)
        {
            LevelDatabaseWindow window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            if (checksSolvability)
            {
                window.ChecksSolvabilityOnSave = true;
            }

            if (isPieceEditorOpen != null)
            {
                window.IsPieceEditorOpen = isPieceEditorOpen;
            }

            return Open(window, width, height);
        }

        /// <summary>Opens a window the caller already created and configured with its own fake seams.</summary>
        internal VisualElement Open(LevelDatabaseWindow window, float width = k_defaultWidth, float height = k_defaultHeight)
        {
            m_window = window;
            m_window.ShowUtility();
            m_window.position = new Rect(40f, 40f, width, height);
            // Tests call CreateGUI directly, as every window fixture does.
            LevelEditorTestAccess.Invoke(m_window, "CreateGUI", null);
            VisualElement root = m_window.rootVisualElement;
            UiTestInput.ValidateLayout(root);
            return root;
        }

        /// <summary>
        /// Creates a plain window and waits for Unity's own CreateGUI instead of calling it directly: a manual
        /// CreateGUI could be followed by Unity's own call, which rebuilds the draft and clears its undo records
        /// between an edit and its undo. See <see cref="Open(float,float,bool,Func{bool})"/> for the parameters.
        /// </summary>
        internal IEnumerator ShowBuiltByUnity(bool checksSolvability = false, Func<bool> isPieceEditorOpen = null)
        {
            LevelDatabaseWindow window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            if (checksSolvability)
            {
                window.ChecksSolvabilityOnSave = true;
            }

            if (isPieceEditorOpen != null)
            {
                window.IsPieceEditorOpen = isPieceEditorOpen;
            }

            yield return ShowBuiltByUnity(window);
        }

        /// <summary>Waits for Unity's own CreateGUI on a window the caller already created and configured.</summary>
        internal IEnumerator ShowBuiltByUnity(LevelDatabaseWindow window)
        {
            m_window = window;
            m_window.ShowUtility();
            m_window.position = new Rect(40f, 40f, k_defaultWidth, k_defaultHeight);
            yield return WaitUntil(() => m_window.rootVisualElement.Q<LevelCanvasElement>() != null);
            // Batch mode never repaints the window, so its layout (and the canvas geometry) stays NaN until validated.
            UiTestInput.ValidateLayout(m_window.rootVisualElement);
            Undo.IncrementCurrentGroup();
        }

        /// <summary>
        /// Loads exactly these levels into the window's own catalog and selects the first one, so a test window never
        /// depends on the real project's active level JSON (Tests-Editor-1-F3). With no levels it loads one minimal
        /// placeholder; most callers follow this with <see cref="LoadFixture"/> for their own content, so the
        /// placeholder's own cells never matter. Call it after <see cref="Open"/> or <see cref="ShowBuiltByUnity"/>:
        /// CreateGUI always reloads the window's levels from the real project JSON, so only a reload run afterwards,
        /// through the window's own LoadSelectedDraft, can replace them. Also rebuilds the Target Level choices
        /// (UpdateLevelChoices), so a test that reads them sees this fixture's levels instead of a count left over from
        /// the real project database. ProjectContext.prefab and any other Level Editor are untouched.
        /// </summary>
        internal void LoadDatabase(params LevelData[] levels)
        {
            if (levels == null || levels.Length == 0)
            {
                levels = new[] { CreatePlaceholderLevel() };
            }

            if (!LevelDatabaseJson.TryWrite(levels, out string json, out string writeError))
            {
                throw new InvalidOperationException(writeError);
            }

            LevelEditorTestAccess.LoadLevels(m_window, json);
            LevelEditorTestAccess.Set(m_window, "m_sourceValid", true);
            LevelEditorTestAccess.Set(m_window, "m_selectedIndex", 0);
            LevelEditorTestAccess.SetLoadedSource(m_window, json);
            LevelEditorTestAccess.Invoke(m_window, "LoadSelectedDraft", null);
            LevelEditorTestAccess.Invoke(m_window, "UpdateLevelChoices", null);
            UiTestInput.ValidateLayout(m_window.rootVisualElement);
        }

        // LevelData.Validate refuses a level with no blocks, so the placeholder holds one single-cell block.
        private static LevelData CreatePlaceholderLevel()
        {
            return new LevelData("fixture", "Fixture", 4, 4, 60, new LevelDoorData[0],
                new[] { new LevelBlockData("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero }) });
        }

        /// <summary>Replaces the window's draft with a known level, as a Reload would, and refreshes the canvas.</summary>
        internal void LoadFixture(VisualElement root, LevelData level)
        {
            LevelDraft draft = (LevelDraft)LevelEditorTestAccess.Get(m_window, "m_draft");
            draft.Replace(level);
            LevelEditorTestAccess.Invoke(m_window, "OnDraftChanged", null);
            UiTestInput.ValidateLayout(root);
        }

        internal LevelData GetDraft()
        {
            LevelDraft draft = (LevelDraft)LevelEditorTestAccess.Get(m_window, "m_draft");
            return draft.Snapshot();
        }

        internal LevelEditorToolState GetToolState()
        {
            return (LevelEditorToolState)LevelEditorTestAccess.Get(m_window, "m_toolState");
        }

        /// <summary>Level Name is enabled only while a level is loaded (SyncDraftFields).</summary>
        internal static bool IsLevelLoaded(VisualElement root)
        {
            return root.Q<TextField>("level-name")?.enabledSelf == true;
        }

        /// <summary>
        /// Puts back the snapshot a test window replaced, so an open LevelEditor scene preview shows the user's level
        /// again.
        /// </summary>
        internal static void RestorePublishedLevel(LevelData previous)
        {
            typeof(LevelDatabaseWindow).GetProperty(nameof(LevelDatabaseWindow.LastPublishedLevel)).SetValue(null, previous);
            Action<LevelData> listeners = (Action<LevelData>)typeof(LevelDatabaseWindow)
                .GetField(nameof(LevelDatabaseWindow.DraftLevelPublished), BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null);
            listeners?.Invoke(previous);
        }

        internal static IEnumerator WaitUntil(Func<bool> condition)
        {
            double end = EditorApplication.timeSinceStartup + k_waitSeconds;
            while (!condition() && EditorApplication.timeSinceStartup < end)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Discards any pending edit, closes the window if it is still open and restores the published-level snapshot.
        /// A window the test already closed itself is skipped, but the snapshot restore always runs. Idempotent: a
        /// second call (for example TearDown after a test's own <c>finally</c> already disposed the fixture and set its
        /// own published-level snapshot) does nothing, so it never overwrites a value the caller restored itself.
        /// </summary>
        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            // Unity's overloaded null is also true for a window the test already closed: only a live one is closed.
            if (m_window != null)
            {
                // A test never saves, so drop any pending edit instead of opening the Save/Discard prompt.
                m_window.DiscardChanges();
                m_window.Close();
            }

            m_window = null;
            RestorePublishedLevel(m_previousPublished);
        }
    }
}
