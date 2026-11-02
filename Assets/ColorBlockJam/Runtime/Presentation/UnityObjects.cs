using UnityEngine;

namespace ColorBlockJam.Presentation
{
    /// <summary>Destroys a Unity object the way the current mode allows: Destroy in Play Mode, DestroyImmediate in the Editor.</summary>
    internal static class UnityObjects
    {
        /// <summary>Destroys target; a null or already destroyed target is skipped.</summary>
        public static void Destroy(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
