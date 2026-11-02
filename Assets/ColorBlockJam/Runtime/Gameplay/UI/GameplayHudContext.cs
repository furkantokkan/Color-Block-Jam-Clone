using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Gameplay.UI
{
    /// <summary>
    /// The scene objects the gameplay HUD draws with (B-21, B-24): its UIDocument (GameplayHud.uxml on the board overlay
    /// panel) and the supplied Rocket reticle texture its white copy is made from. It holds no state.
    /// </summary>
    public sealed class GameplayHudContext
    {
        /// <param name="document">The HUD's UIDocument; required.</param>
        /// <param name="reticleTexture">Art/UI/Supplied/Icons/Gameplay/ic_target.png with Read/Write on, or null.</param>
        public GameplayHudContext(UIDocument document, Texture2D reticleTexture)
        {
            Document = document != null ? document : throw new ArgumentNullException(nameof(document));
            ReticleTexture = reticleTexture;
        }

        public UIDocument Document { get; }

        /// <summary>The reticle source, or null: the reticles then show only their black outline.</summary>
        public Texture2D ReticleTexture { get; }
    }
}
