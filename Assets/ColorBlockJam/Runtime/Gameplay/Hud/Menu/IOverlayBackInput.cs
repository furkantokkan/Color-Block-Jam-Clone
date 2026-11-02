using System;

namespace ColorBlockJam.Gameplay.Hud.Menu
{
    /// <summary>Android Back while an in-game overlay shows (Game HUD design 5); a seam for EditMode fakes.</summary>
    public interface IOverlayBackInput
    {
        /// <summary>Raised once per Back press while enabled.</summary>
        event Action Pressed;

        bool IsEnabled { get; }

        void Enable();

        void Disable();
    }
}
