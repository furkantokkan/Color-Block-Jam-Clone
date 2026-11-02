using System;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Runs the Level Editor's Test Level: it stores the request, makes the Game scene the Play Mode start scene and
    /// enters Play Mode; when Play Mode ends it puts the previous start scene back and clears the request, while Unity
    /// reopens the scenes that were open before (PRD 11.5, DEC-127).
    /// </summary>
    [InitializeOnLoad]
    internal static class LevelTestPlay
    {
        internal const string k_gameScenePath = "Assets/ColorBlockJam/Scenes/Game.unity";
        // Session storage survives a domain reload on entering Play Mode (RISK-12); an Editor restart clears it.
        private const string k_activeKey = "ColorBlockJam.TestPlay.Active";
        private const string k_previousStartSceneKey = "ColorBlockJam.TestPlay.PreviousStartScene";

        /// <summary>The Editor calls a test play makes, each replaceable, so tests enter no Play Mode.</summary>
        internal sealed class Steps
        {
            public Func<bool> IsPlaying { get; set; } = () => EditorApplication.isPlayingOrWillChangePlaymode;
            public Func<bool> IsCompiling { get; set; } = () => EditorApplication.isCompiling;
            public Func<bool> HasCompileErrors { get; set; } = () => EditorUtility.scriptCompilationFailed;
            public Func<string, SceneAsset> LoadScene { get; set; } = AssetDatabase.LoadAssetAtPath<SceneAsset>;
            public Func<SceneAsset> GetStartScene { get; set; } = () => EditorSceneManager.playModeStartScene;
            public Action<SceneAsset> SetStartScene { get; set; } = scene => EditorSceneManager.playModeStartScene = scene;
            public Action EnterPlayMode { get; set; } = EditorApplication.EnterPlaymode;
        }

        static LevelTestPlay()
        {
            // Owner: this class for the loaded domain; a domain reload subscribes it again. Stop point: the domain unloads.
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// True from a started test play until Play Mode ends; the LevelEditor Play Mode guard lets only this through,
        /// because the Game scene, not the LevelEditor scene, runs.
        /// </summary>
        internal static bool IsActive => SessionState.GetBool(k_activeKey, false);

        /// <summary>
        /// Starts a test play of the request and returns null, or returns why nothing started. Play Mode begins on the
        /// next Editor update.
        /// </summary>
        internal static string Start(LevelTestPlayRequest request, Steps steps)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            if (steps.IsPlaying())
            {
                return "Stop Play Mode before testing a level.";
            }

            if (steps.IsCompiling())
            {
                return "Scripts are compiling. Press Test Level again when they finish.";
            }

            if (steps.HasCompileErrors())
            {
                return "Fix the script compile errors in the Console before testing a level.";
            }

            SceneAsset game = steps.LoadScene(k_gameScenePath);
            if (game == null)
            {
                return $"Test Level needs the Game scene at {k_gameScenePath}.";
            }

            // A start scene chosen elsewhere, such as in Onity's scene flow window, comes back when the test play ends.
            SceneAsset previous = steps.GetStartScene();
            SessionState.SetString(k_previousStartSceneKey,
                previous != null ? AssetDatabase.GetAssetPath(previous) : string.Empty);
            SessionState.SetBool(k_activeKey, true);
            request.Write();
            steps.SetStartScene(game);
            steps.EnterPlayMode();
            return null;
        }

        /// <summary>Puts the previous Play Mode start scene back and clears the request.</summary>
        internal static void Finish(Steps steps)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            string previousPath = SessionState.GetString(k_previousStartSceneKey, string.Empty);
            steps.SetStartScene(previousPath.Length > 0 ? steps.LoadScene(previousPath) : null);
            SessionState.EraseString(k_previousStartSceneKey);
            SessionState.EraseBool(k_activeKey);
            LevelTestPlayRequest.Clear();
        }

        // A test play that could not begin keeps its request until the next Play Mode ends, and ends then.
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && IsActive)
            {
                Finish(new Steps());
            }
        }
    }
}
