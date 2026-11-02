using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies that the authoring scene cannot enter player Play Mode.
    /// </summary>
    public sealed class LevelEditorPlayModeGuardTests
    {
        [UnityTest]
        public IEnumerator LevelEditorCancelsPlayMode()
        {
            SceneSetup[] originalScenes = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                EditorSceneManager.OpenScene(
                    "Assets/ColorBlockJam/Scenes/LevelEditor.unity",
                    OpenSceneMode.Single);
                EditorApplication.isPlaying = true;

                for (int frame = 0; frame < 5; frame++)
                {
                    yield return null;
                }

                Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
            }
            finally
            {
                foreach (SceneSetup scene in originalScenes)
                {
                    if (scene.isLoaded && scene.isActive)
                    {
                        EditorSceneManager.RestoreSceneManagerSetup(originalScenes);
                        break;
                    }
                }
            }
        }
    }
}
