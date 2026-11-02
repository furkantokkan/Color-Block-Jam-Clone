using System.Collections;
using ColorBlockJam.Runtime.SceneFlow;
using Onity.Unity.Contexts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorBlockJam.Tests.Gameplay
{
    /// <summary>
    /// Test cleanup for the hot level (DEC-199): a won level prepares the next one in a hidden Game scene, so a test
    /// that wins waits for that preparation to end and then unloads every Game scene, not only the first one with the
    /// name, leaving no hidden scene for the next test.
    /// </summary>
    internal static class PreparedLevelScenes
    {
        private const string k_gameScene = "Game";
        private const float k_timeoutSeconds = 20f;

        public static IEnumerator UnloadAllGameScenes()
        {
            if (ProjectContext.Instance != null && ProjectContext.Instance.Container != null
                && ProjectContext.Instance.Container.TryResolve(out LevelPreloader preloader))
            {
                float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
                while (preloader.IsPreparing && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
            }

            for (int index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.name == k_gameScene && scene.isLoaded && SceneManager.sceneCount > 1)
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }
        }
    }
}
