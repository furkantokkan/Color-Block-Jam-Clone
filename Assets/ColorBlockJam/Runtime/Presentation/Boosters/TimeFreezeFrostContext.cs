using System;
using UnityEngine;

namespace ColorBlockJam.Presentation.Boosters
{
    /// <summary>
    /// The scene objects the Time Freeze frost draws with (B-25): the gameplay camera its quad sits in front of and
    /// the shared frost material. It holds no state; a missing material leaves the frost inert.
    /// </summary>
    public sealed class TimeFreezeFrostContext
    {
        /// <param name="camera">The gameplay camera; required.</param>
        /// <param name="material">The TimeFreezeOverlay material, or null while the scene has none.</param>
        public TimeFreezeFrostContext(Camera camera, Material material)
        {
            Camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            Material = material;
        }

        public Camera Camera { get; }

        /// <summary>The frost material, or null: the frost then creates and draws nothing.</summary>
        public Material Material { get; }
    }
}
