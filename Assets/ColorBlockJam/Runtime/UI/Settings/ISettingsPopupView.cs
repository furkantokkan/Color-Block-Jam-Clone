using System;
using ColorBlockJam.Player;

namespace ColorBlockJam.UI.Settings
{
    /// <summary>
    /// What <see cref="SettingsPopupPresenter"/> may do to the Settings popup, without any VisualElement, so EditMode
    /// tests can use a fake view (Hub-And-Player-Data-Design 8, DEC-136).
    /// </summary>
    public interface ISettingsPopupView
    {
        /// <summary>Raised when a toggle is released as a click.</summary>
        event Action<PlayerSettingId> TogglePressed;

        /// <summary>Raised when the close button or the scrim around the popup is released as a click.</summary>
        event Action CloseRequested;

        /// <summary>Raised when the HOME button is released as a click; it can be pressed only while shown.</summary>
        event Action HomePressed;

        /// <summary>Shows or hides the whole popup, scrim included.</summary>
        void SetVisible(bool visible);

        /// <summary>
        /// Shows one toggle ON (btn_toggle_on knob at the right of a dark green track) or OFF (light grey knob at the
        /// left of a dark navy track), DEC-134.
        /// </summary>
        void SetToggle(PlayerSettingId id, bool isOn);

        /// <summary>Shows a short message over the popup's bottom edge; an empty text hides it.</summary>
        void SetMessage(string text);

        /// <summary>Shows or hides the green HOME button below the placeholder buttons (reference 20, DEC-135).</summary>
        void SetHomeVisible(bool visible);
    }
}
