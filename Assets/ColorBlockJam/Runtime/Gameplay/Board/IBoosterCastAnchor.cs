using UnityEngine;

namespace ColorBlockJam.Gameplay.Board
{
    /// <summary>
    /// Tells the board presenter where on screen the booster casts enter: the top centre of the HUD's booster bar, so
    /// the Hammer, Rocket and Vacuum rise from just above the buttons that cast them (reference 13).
    /// </summary>
    public interface IBoosterCastAnchor
    {
        /// <summary>
        /// The top centre of the booster bar as a viewport point (0 to 1, origin bottom left); false while no bar is
        /// laid out. Called when a cast starts during Playing, so it must allocate nothing.
        /// </summary>
        bool TryGetLaunchViewportPoint(out Vector2 viewportPoint);
    }
}
