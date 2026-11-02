using System;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>A draft owner reads its selection and applies commands with its own Undo history.</summary>
    internal interface ILevelDraftEditor
    {
        bool IsAvailable { get; }
        LevelCanvasSelection CanvasSelection { get; }
        LevelData GetDraftLevel();
        bool TryApplyEditRequest(string undoName, LevelEditRequest request, out string issue);
    }

    /// <summary>Discovery and selection notifications for draft owners, independent of the effect view.</summary>
    internal interface ILevelDraftEditorSource : IDisposable
    {
        event Action<ILevelDraftEditor> Changed;
        ILevelDraftEditor Find(ILevelDraftEditor excluded);
    }

    /// <summary>The Unity window adapter; LevelDatabaseWindow remains the only writer of its draft.</summary>
    internal sealed class LevelDraftEditor : ILevelDraftEditor
    {
        internal LevelDatabaseWindow Window { get; }

        internal LevelDraftEditor(LevelDatabaseWindow window) => Window = window;

        public bool IsAvailable => Window != null;
        public LevelCanvasSelection CanvasSelection => IsAvailable ? Window.CanvasSelection : LevelCanvasSelection.None;
        public LevelData GetDraftLevel() => IsAvailable ? Window.GetDraftLevel() : null;

        public bool TryApplyEditRequest(string undoName, LevelEditRequest request, out string issue)
        {
            if (!IsAvailable)
            {
                issue = "The Level Editor is closed.";
                return false;
            }

            return Window.TryApplyEditRequest(undoName, request, out issue);
        }
    }

    /// <summary>Owns the Unity window query and static event subscription; disposed with its effect window.</summary>
    internal sealed class LevelDraftEditorSource : ILevelDraftEditorSource
    {
        public event Action<ILevelDraftEditor> Changed;

        internal LevelDraftEditorSource() => LevelDatabaseWindow.SelectionChanged += OnSelectionChanged;

        public ILevelDraftEditor Find(ILevelDraftEditor excluded)
        {
            LevelDatabaseWindow excludedWindow = (excluded as LevelDraftEditor)?.Window;
            LevelDatabaseWindow[] windows = Resources.FindObjectsOfTypeAll<LevelDatabaseWindow>();
            LevelDatabaseWindow fallback = null;
            for (int index = 0; index < windows.Length; index++)
            {
                LevelDatabaseWindow window = windows[index];
                if (window == null || window == excludedWindow)
                {
                    continue;
                }

                if (window.GetDraftLevel() != null)
                {
                    return new LevelDraftEditor(window);
                }

                fallback = fallback != null ? fallback : window;
            }

            return fallback != null ? new LevelDraftEditor(fallback) : null;
        }

        public void Dispose() => LevelDatabaseWindow.SelectionChanged -= OnSelectionChanged;

        private void OnSelectionChanged(LevelDatabaseWindow window)
        {
            if (window != null)
            {
                Changed?.Invoke(new LevelDraftEditor(window));
            }
        }
    }
}
