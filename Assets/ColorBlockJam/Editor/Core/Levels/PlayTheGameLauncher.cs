using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The Color Block Jam/Play The Game menu item: opens the Init scene and enters Play Mode, so the game runs its real
    /// startup flow (DEC-140). It is here because the other launchers leave the LevelEditor or Game scene open. It is
    /// enabled only outside Play Mode and while no Level Editor plays a solution.
    /// </summary>
    internal static class PlayTheGameLauncher
    {
        internal const string k_initScenePath = "Assets/ColorBlockJam/Scenes/Init.unity";
        private const string k_menuPath = "Color Block Jam/Play The Game";

        [MenuItem(k_menuPath, false, 2000)]
        private static void Play()
        {
            // Unity's own save prompt; Cancel starts nothing.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(k_initScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(k_menuPath, true)]
        private static bool CanPlay()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !IsAnySolutionPlaying();
        }

        private static bool IsAnySolutionPlaying()
        {
            foreach (LevelDatabaseWindow window in Resources.FindObjectsOfTypeAll<LevelDatabaseWindow>())
            {
                if (window.SolutionPlayback != null && window.SolutionPlayback.IsActive)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
