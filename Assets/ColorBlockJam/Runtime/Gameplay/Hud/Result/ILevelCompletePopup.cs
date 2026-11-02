using System;

namespace ColorBlockJam.Gameplay.Hud.Result
{
    /// <summary>
    /// The Level Complete popup as <see cref="LevelCompleteFlow"/> sees it: it shows and hides, takes its one button's
    /// action and a note, and reports presses; it decides nothing. EditMode tests fake it.
    /// </summary>
    public interface ILevelCompletePopup
    {
        /// <summary>The one button: Continue, Retry or Home, as last set.</summary>
        event Action PrimaryRequested;

        bool IsShowing { get; }

        /// <summary>
        /// Shows the popup with the reward text on its button ("10"), or without the reward row for null or empty; false
        /// when the HUD has no popup to show.
        /// </summary>
        bool Show(string rewardText);

        void SetPrimary(LevelCompleteAction action, bool enabled);

        /// <summary>The note under the coins; null or empty hides it.</summary>
        void SetMessage(string text);

        void Hide();
    }
}
