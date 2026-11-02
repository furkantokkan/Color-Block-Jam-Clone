using System;
using System.IO;
using ColorBlockJam.Editor.BoardKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The Color Block Jam/Level Editor workspace launcher: switches to the LevelEditor scene without writing it, lays
    /// the Editor out from the project's saved LevelEditorLayout.wlt and focuses the Level Editor
    /// (docs/Level-Editor-Design.md, Workspace and Effect Editor). Its scene step also serves a designer's Level Editor
    /// after a canvas press (<see cref="OpenScene(Steps)"/>).
    /// </summary>
    internal static class LevelEditorWorkspace
    {
        internal const string k_scenePath = "Assets/ColorBlockJam/Scenes/LevelEditor.unity";

        /// <summary>
        /// The window layout the user saved for level editing on 2026-09-28, relative to the project folder: Game,
        /// Scene, Piece Editor, Level Editor, Inspector, Effect Editor, Project and Console.
        /// </summary>
        internal const string k_layoutPath = "LevelEditorLayout.wlt";

        internal const string k_playModeMessage =
            "Exit Play Mode to open the LevelEditor scene. The editor windows opened without it.";

        internal const string k_missingLayoutMessage =
            "LevelEditorLayout.wlt is missing from the project folder, so only the Level Editor opened. Save it with " +
            "Window > Layouts > Save Layout to File.";

        /// <summary>What the launcher did with the scene.</summary>
        internal enum SceneOutcome
        {
            Cancelled = 0,
            Opened = 1,
            AlreadyActive = 2,
            RefusedInPlayMode = 3
        }

        /// <summary>
        /// The editor calls the launcher makes, each replaceable, so tests switch no scene, load no layout and open
        /// no window.
        /// </summary>
        internal sealed class Steps
        {
            public Func<bool> IsPlaying { get; set; } = () => EditorApplication.isPlayingOrWillChangePlaymode;
            public Func<string> GetActiveScenePath { get; set; } = () => SceneManager.GetActiveScene().path;

            /// <summary>Unity's save prompt for modified scenes; false when the designer cancels.</summary>
            public Func<bool> SaveModifiedScenes { get; set; } = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo;

            /// <summary>
            /// Asks every open window with unsaved changes to save or discard them before the layout replaces it;
            /// false when the designer cancels or a save fails.
            /// </summary>
            public Func<bool> SaveOrDiscardUnsavedWindows { get; set; } = AskUnsavedWindows;

            public Action<string> OpenSceneSingle { get; set; } =
                path => EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            /// <summary>Loads a window layout file; false when it is missing or Unity does not load it.</summary>
            public Func<string, bool> LoadLayout { get; set; } = LoadLayoutFile;

            /// <summary>
            /// Clears the state the layout file restored in the Level Editor and the Piece Editor: level pieces and a
            /// draft from the windows the layout was saved from.
            /// </summary>
            public Action ForgetLayoutState { get; set; } = ForgetLayoutWindowState;

            public Action<string> ShowMessage { get; set; } = ShowOnFocusedWindow;

            /// <summary>Opens or focuses the Level Editor; the layout's own Level Editor when the layout loaded.</summary>
            public Action OpenLevelEditor { get; set; } = LevelDatabaseWindow.Open;
        }

        [MenuItem("Color Block Jam/Level Editor")]
        public static void Open()
        {
            Open(new Steps());
        }

        /// <summary>
        /// Asks Unity's save prompt for modified scenes unless the LevelEditor scene is already active, then asks every
        /// window with unsaved changes to save or discard them; Cancel at either stops everything, and Play Mode
        /// refuses only the scene step. Then opens the LevelEditor scene single, loads the saved layout and opens the
        /// Level Editor through <see cref="LevelDatabaseWindow.Open"/>, which turns on the layout window's designer
        /// preferences (Validate Before Save and Auto Play from the designer's stored choices, both on by default) and
        /// its Effect Editor opening on a click. It never saves the LevelEditor scene.
        /// </summary>
        internal static SceneOutcome Open(Steps steps)
        {
            // Loading a layout closes every window, so a window's unsaved changes are settled after the scene save
            // prompt and before anything changes.
            SceneOutcome outcome = OpenScene(steps, steps.SaveOrDiscardUnsavedWindows);
            if (outcome == SceneOutcome.Cancelled)
            {
                return outcome;
            }

            bool laidOut = steps.LoadLayout(k_layoutPath);
            if (laidOut)
            {
                // The windows' unsaved work was settled above, so whatever state the layout brought is stale.
                steps.ForgetLayoutState();
            }

            steps.OpenLevelEditor();
            if (!laidOut)
            {
                steps.ShowMessage(k_missingLayoutMessage);
            }

            if (outcome == SceneOutcome.RefusedInPlayMode)
            {
                steps.ShowMessage(k_playModeMessage);
            }

            return outcome;
        }

        /// <summary>
        /// The launcher's scene step alone, which a designer's Level Editor runs after a canvas press while the
        /// LevelEditor scene is closed (TASK-20260928-091128541-cb48c84a, contract Revision 1): unless the LevelEditor
        /// scene is already active, and outside Play Mode, it asks Unity's save prompt for modified scenes and opens the
        /// scene single. It never saves the LevelEditor scene, loads no layout and opens no window.
        /// </summary>
        internal static SceneOutcome OpenScene(Steps steps)
        {
            return OpenScene(steps, null);
        }

        // The scene step both callers share. beforeChange runs after the save prompt and before the scene changes, also
        // when no scene opens (already active, or refused in Play Mode); its false cancels. The Level Editor's own call
        // has none.
        private static SceneOutcome OpenScene(Steps steps, Func<bool> beforeChange)
        {
            SceneOutcome outcome;
            if (string.Equals(steps.GetActiveScenePath(), k_scenePath, StringComparison.Ordinal))
            {
                outcome = SceneOutcome.AlreadyActive;
            }
            else if (steps.IsPlaying())
            {
                outcome = SceneOutcome.RefusedInPlayMode;
            }
            else if (!steps.SaveModifiedScenes())
            {
                return SceneOutcome.Cancelled;
            }
            else
            {
                outcome = SceneOutcome.Opened;
            }

            if (beforeChange != null && !beforeChange())
            {
                return SceneOutcome.Cancelled;
            }

            if (outcome == SceneOutcome.Opened)
            {
                steps.OpenSceneSingle(k_scenePath);
            }

            return outcome;
        }

        // The same three answers Unity gives a closing window with unsaved changes.
        private static bool AskUnsavedWindows()
        {
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (!window.hasUnsavedChanges)
                {
                    continue;
                }

                int answer = EditorUtility.DisplayDialogComplex("Unsaved Changes in " + window.titleContent.text,
                    string.IsNullOrEmpty(window.saveChangesMessage)
                        ? "This window has unsaved changes."
                        : window.saveChangesMessage,
                    "Save", "Cancel", "Discard");
                if (answer == 1)
                {
                    return false;
                }

                if (answer == 0)
                {
                    window.SaveChanges();
                }
                else
                {
                    window.DiscardChanges();
                }

                // A failed save keeps its changes, and the layout would close the window over them.
                if (window.hasUnsavedChanges)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool LoadLayoutFile(string path)
        {
            string fullPath = Path.GetFullPath(path);
            return File.Exists(fullPath) && EditorUtility.LoadWindowLayout(fullPath);
        }

        private static void ForgetLayoutWindowState()
        {
            foreach (LevelDatabaseWindow window in Resources.FindObjectsOfTypeAll<LevelDatabaseWindow>())
            {
                window.ForgetLayoutSession();
            }

            foreach (PieceEditorWindow window in Resources.FindObjectsOfTypeAll<PieceEditorWindow>())
            {
                window.ForgetLayoutPiece();
            }
        }

        private static void ShowOnFocusedWindow(string message)
        {
            Debug.LogWarning("Level Editor: " + message);
            EditorWindow focused = EditorWindow.focusedWindow;
            if (focused != null)
            {
                focused.ShowNotification(new GUIContent(message));
            }
        }
    }
}
