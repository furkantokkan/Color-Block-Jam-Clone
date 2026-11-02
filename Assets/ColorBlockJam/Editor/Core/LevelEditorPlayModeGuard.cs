using ColorBlockJam.Editor.Levels;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the authoring-only LevelEditor scene out of Play Mode. The Level Editor's Test Level passes: it makes the Game
/// scene the Play Mode start scene, so the LevelEditor scene does not run (DEC-127).
/// </summary>
[InitializeOnLoad]
internal static class LevelEditorPlayModeGuard
{
    private const string k_levelEditorScenePath =
        "Assets/ColorBlockJam/Scenes/LevelEditor.unity";

    static LevelEditorPlayModeGuard()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode || LevelTestPlay.IsActive)
        {
            return;
        }

        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);

            if (scene.path != k_levelEditorScenePath)
            {
                continue;
            }

            EditorApplication.isPlaying = false;
            Debug.LogWarning("LevelEditor is an edit-only scene. Close it before entering Play Mode.");
            return;
        }
    }
}
