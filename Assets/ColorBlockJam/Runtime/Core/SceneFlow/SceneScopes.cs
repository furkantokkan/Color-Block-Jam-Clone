using Onity.Unity.Contexts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorBlockJam.Runtime.SceneFlow
{
    /// <summary>
    /// Finds the Onity SceneContext of a loaded scene, because Onity keeps no lookup by scene. The startup loader and
    /// the scene transition use it to wait for a scene's build.
    /// </summary>
    public static class SceneScopes
    {
        /// <summary>
        /// The scene's SceneContext, or null for an unloaded scene or one without a context. It walks the scene's root
        /// objects, so it runs once per scene change, never in a Playing tick.
        /// </summary>
        public static SceneContext Find(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                SceneContext scope = roots[index].GetComponentInChildren<SceneContext>(true);
                if (scope != null)
                {
                    return scope;
                }
            }

            return null;
        }
    }
}
