using System;
using ColorBlockJam.Haptics;
using UnityEngine.UIElements;

namespace ColorBlockJam.UI.Haptics
{
    /// <summary>
    /// Vibrates when a button marked with a haptic USS class is clicked under one UI root, so UXML decides which
    /// buttons vibrate and how strongly, without a call in every presenter (DEC-156).
    /// </summary>
    /// <remarks>
    /// Owner: the view host that calls <see cref="Attach"/>; its one PointerUpEvent callback lives until
    /// <see cref="Detach"/>, which the host calls in OnDisable. It listens to the release, not ClickEvent: UI Toolkit
    /// sends ClickEvent only after the button's own handler ran and only if the same button is still under the
    /// finger, so a button that closes its popup, covers itself or changes scene (Play, close, Level) never got one
    /// on the phone. The release trickles down from the root before the button's Clickable acts, and it counts only
    /// inside the element, as Clickable does. A disabled button plays nothing. The walk up from the released element
    /// allocates nothing. The Vibration setting gate lives in <see cref="IHaptics"/>.
    /// </remarks>
    public sealed class ButtonHaptics
    {
        /// <summary>A light tick for ordinary buttons: close, toggles, selectors.</summary>
        public const string TapClass = "cbj-haptic-tap";

        /// <summary>A firmer pulse for primary actions: Level, Play, Buy, Continue.</summary>
        public const string ConfirmClass = "cbj-haptic-confirm";

        private readonly IHaptics m_haptics;
        private readonly EventCallback<PointerUpEvent> m_onPointerUp;
        private VisualElement m_root;

        public ButtonHaptics(IHaptics haptics)
        {
            m_haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));
            m_onPointerUp = OnPointerUp;
        }

        public void Attach(VisualElement root)
        {
            Detach();
            m_root = root ?? throw new ArgumentNullException(nameof(root));
            m_root.RegisterCallback(m_onPointerUp, TrickleDown.TrickleDown);
        }

        public void Detach()
        {
            if (m_root == null)
            {
                return;
            }

            m_root.UnregisterCallback(m_onPointerUp, TrickleDown.TrickleDown);
            m_root = null;
        }

        /// <summary>
        /// Plays the haptic of the nearest marked ancestor of <paramref name="clicked"/>, itself included, up to the
        /// attached root. Returns whether one played.
        /// </summary>
        public bool PlayFor(VisualElement clicked)
        {
            for (VisualElement element = clicked; element != null; element = element.parent)
            {
                if (element.ClassListContains(ConfirmClass))
                {
                    return Play(element, HapticFeedback.ButtonConfirm);
                }

                if (element.ClassListContains(TapClass))
                {
                    return Play(element, HapticFeedback.ButtonTap);
                }

                if (element == m_root)
                {
                    return false;
                }
            }

            return false;
        }

        private bool Play(VisualElement button, HapticFeedback feedback)
        {
            if (!button.enabledInHierarchy)
            {
                return false;
            }

            m_haptics.Play(feedback);
            return true;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            // The pressed button holds the pointer capture, so it is the target even when the finger slid off;
            // a release outside it is no click.
            if (evt.target is VisualElement released && released.worldBound.Contains(evt.position))
            {
                PlayFor(released);
            }
        }
    }
}
