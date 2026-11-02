using System;

namespace ColorBlockJam.Gameplay.Hud.Menu
{
    /// <summary>
    /// The Retry confirmation popup as the menu flow sees it (Game HUD design 6), without any VisualElement, so
    /// EditMode tests can use a fake.
    /// </summary>
    public interface IRestartConfirm
    {
        /// <summary>The close button or a scrim tap.</summary>
        event Action CloseRequested;

        /// <summary>The green Retry or Leave button.</summary>
        event Action ConfirmRequested;

        bool IsShowing { get; }

        /// <summary>
        /// Shows the popup titled <paramref name="title"/> with its green button reading <paramref name="action"/> and
        /// enabled again; false when the HUD has no popup (one error is logged).
        /// </summary>
        bool Show(string title, LifeConfirmAction action);

        void Hide();

        /// <summary>Enables or disables Retry and Close; off while a booster write or the attempt's life charge is
        /// saving (AC-9, DEC-174).</summary>
        void SetActionsEnabled(bool enabled);

        /// <summary>
        /// Enables or disables the Retry button alone: off right after its press (one press, one Restart, design 4),
        /// on again when that Restart is refused or on the next <see cref="Show"/>.
        /// </summary>
        void SetConfirmEnabled(bool enabled);
    }
}
