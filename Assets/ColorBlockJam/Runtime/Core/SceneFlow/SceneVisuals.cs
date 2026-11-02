using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ColorBlockJam.Runtime.SceneFlow
{
    /// <summary>
    /// Hides a whole loaded scene in one frame (DEC-199): its cameras, audio listeners and lights stop and its UI
    /// documents stop drawing and taking presses. The level preloader uses it on the scene a started level replaces,
    /// right before that scene unloads, so the two never show together.
    /// </summary>
    public static class SceneVisuals
    {
        /// <summary>
        /// Hides <paramref name="scene"/>; an invalid or unloaded scene is ignored. It walks the scene's objects once,
        /// so it runs at a scene change, never in a Playing tick.
        /// </summary>
        public static void Hide(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                GameObject root = roots[index];
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    camera.enabled = false;
                }

                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                {
                    listener.enabled = false;
                }

                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    light.enabled = false;
                }

                foreach (UIDocument document in root.GetComponentsInChildren<UIDocument>(true))
                {
                    if (document.rootVisualElement != null)
                    {
                        document.rootVisualElement.style.display = DisplayStyle.None;
                    }
                }
            }
        }
    }
}
