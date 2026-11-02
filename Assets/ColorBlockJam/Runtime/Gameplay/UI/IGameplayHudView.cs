using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam.Gameplay.UI
{
    /// <summary>
    /// The gameplay HUD as its presenters see it (B-22, B-23): booster slots, the targeting banner, the main timer, the
    /// Time Freeze counter and the opening icons. It only writes text, classes, icons and enabled states and forwards
    /// presses, so EditMode tests drive the presenters with a fake.
    /// </summary>
    public interface IGameplayHudView
    {
        /// <summary>The booster buttons the layout has, left to right.</summary>
        int SlotCount { get; }

        /// <summary>A booster button was clicked; the argument is its slot.</summary>
        event Action<int> SlotPressed;

        /// <summary>The targeting banner's close button was clicked.</summary>
        event Action BannerClosed;

        /// <summary>Shows a slot with its booster's icon.</summary>
        void ShowSlot(int slot, Sprite icon);

        /// <summary>Hides a slot the catalog leaves unused.</summary>
        void HideSlot(int slot);

        /// <summary>Writes the stock count; empty shows the plus badge instead of the count.</summary>
        void SetSlotStock(int slot, string count, bool empty);

        void SetSlotSelected(int slot, bool selected);

        void SetSlotEnabled(int slot, bool enabled);

        /// <summary>A press that could not start anything: the button flashes once.</summary>
        void PlaySlotBlocked(int slot);

        /// <summary>Shows the targeting banner with the booster's title, instruction and icon.</summary>
        void ShowBanner(string title, string instruction, Sprite icon);

        void HideBanner();

        /// <summary>Writes the main timer's "mm:ss" text.</summary>
        void SetTimer(string text);

        /// <summary>Shows or hides the Time Freeze counter and the icy timer style.</summary>
        void SetFrozen(bool frozen);

        /// <summary>Writes the Time Freeze counter's text and sets its bar fill, 0 to 1, at once; a running drain stops.</summary>
        void SetFreezeCounter(string text, float fill);

        /// <summary>Drains the Time Freeze bar linearly from its shown fill to <paramref name="fill"/> over the seconds.</summary>
        void DrainFreezeBar(float fill, float seconds);

        /// <summary>Suspend and Resume: holds or continues the Time Freeze bar's drain.</summary>
        void SetFreezeBarPaused(bool paused);

        /// <summary>
        /// Shows one opening icon per sprite, left to right, and hides the rest; any count fits. Called on Home only.
        /// The view reads the list during the call and keeps no reference to it.
        /// </summary>
        void SetOpeningIcons(IReadOnlyList<Sprite> icons);

        /// <summary>Shows or hides the opening layer (the dimmed screen with the icons).</summary>
        void SetOpening(bool opening);
    }
}
