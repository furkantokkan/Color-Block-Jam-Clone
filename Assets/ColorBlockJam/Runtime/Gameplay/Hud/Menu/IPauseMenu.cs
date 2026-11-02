using System;

namespace ColorBlockJam.Gameplay.Hud.Menu
{
    /// <summary>
    /// The in-game pause menu as the menu flow sees it: the shared Settings popup with HOME (DEC-135, DEC-136), without
    /// any VisualElement, so EditMode tests can use a fake.
    /// </summary>
    public interface IPauseMenu
    {
        /// <summary>The close button or a scrim tap.</summary>
        event Action CloseRequested;

        /// <summary>The HOME button.</summary>
        event Action HomeRequested;

        bool IsShowing { get; }

        /// <summary>Shows the menu; false when the HUD has no Settings popup (one error is logged).</summary>
        bool Show();

        void Hide();

        /// <summary>Enables or disables Close and HOME; off while a booster write or the attempt's life charge is
        /// saving (AC-5, AC-8, DEC-174).</summary>
        void SetActionsEnabled(bool enabled);
    }
}
