using System;
using UnityEngine;

namespace ColorBlockJam.Hub.PreLevel
{
    /// <summary>The look of one starting-booster selector (references 09 and 10).</summary>
    public enum PreLevelSelectorState
    {
        /// <summary>The blue rounded square (Btn_settings_normal) with the red stock count badge.</summary>
        Available = 0,

        /// <summary>The rounded square tinted green with the check badge.</summary>
        Selected = 1,

        /// <summary>The blue rounded square with the ic_plus badge; a tap opens the purchase popup (DEC-85).</summary>
        OutOfStock = 2
    }

    /// <summary>
    /// What <see cref="PreLevelPresenter"/> may do to the pre-level popup, without any VisualElement, so EditMode
    /// tests can use a fake view (design 6.3, 9.1).
    /// </summary>
    public interface IPreLevelView
    {
        /// <summary>Raised when the close button is released as a click.</summary>
        event Action ClosePressed;

        /// <summary>Raised when Play is released as a click.</summary>
        event Action PlayPressed;

        /// <summary>Raised with the selector slot when a selector is released as a click.</summary>
        event Action<int> SelectorPressed;

        /// <summary>Shows or hides the whole popup, scrim included.</summary>
        void SetVisible(bool visible);

        /// <summary>Sets the header text ("LEVEL 3").</summary>
        void SetTitle(string title);

        /// <summary>Adds one selector at the end of the row and returns its slot (0, 1, ...).</summary>
        int AddSelector(Sprite icon, string label);

        /// <summary>Removes every selector the view added.</summary>
        void ClearSelectors();

        /// <summary>Shows a selector's state; <paramref name="count"/> is the badge text for Available.</summary>
        void SetSelector(int slot, PreLevelSelectorState state, string count);

        void SetPlayInteractable(bool interactable);
    }
}
