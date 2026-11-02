using System;

namespace ColorBlockJam.Gameplay.Hud.TopBar
{
    /// <summary>
    /// What the top bar presenter may do to the top bar (GameTopBar.uxml), without any VisualElement, so EditMode tests
    /// can use a fake view. The timer and freeze counter inside the bar stay with Booster's HUD view.
    /// </summary>
    public interface ITopBarView
    {
        /// <summary>The Retry button was released as a click.</summary>
        event Action RetryPressed;

        /// <summary>The Pause button was released as a click.</summary>
        event Action PausePressed;

        /// <summary>Writes the Level badge's number into both heavy-text layers.</summary>
        void SetLevelNumber(string text);

        /// <summary>Writes the coin amount; the holder is shown.</summary>
        void SetCoinAmount(string text);

        /// <summary>Hides the whole coin holder (no coin currency in the catalog).</summary>
        void HideCoin();

        void SetRetryEnabled(bool enabled);

        void SetPauseEnabled(bool enabled);
    }
}
