using System;
using UnityEngine.InputSystem;

namespace ColorBlockJam.Hub.Navigation
{
    /// <summary>
    /// Turns the system Back key (Escape, which Android Back maps to) into one "close the top overlay"
    /// request through a code-built InputAction that exists only while Hub is shown (design 6.4).
    /// </summary>
    /// <remarks>
    /// Owner: <c>HubScreen</c>, which calls <see cref="Enable"/> on bind and <see cref="Disable"/> in
    /// <c>OnDisable</c>; the Hub scene container disposes it (AsSingle) as the last stop point. The
    /// <c>performed</c> callback runs only while the action exists, so nothing polls per frame.
    /// </remarks>
    public sealed class HubBackInput : IDisposable
    {
        private const string k_actionName = "HubBack";
        private const string k_backBinding = "<Keyboard>/escape";

        private readonly IHubNavigator m_navigator;
        private readonly Action<InputAction.CallbackContext> m_onPerformed;
        private InputAction m_action;

        public HubBackInput(IHubNavigator navigator)
        {
            m_navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            m_onPerformed = OnPerformed;
        }

        /// <summary>True while the Back action exists and is enabled.</summary>
        public bool IsEnabled => m_action != null && m_action.enabled;

        /// <summary>True while the Back action exists; false after <see cref="Disable"/> disposed it.</summary>
        public bool HasAction => m_action != null;

        /// <summary>Creates and enables the Back action; does nothing when it already exists.</summary>
        public void Enable()
        {
            if (m_action != null)
            {
                return;
            }

            m_action = new InputAction(k_actionName, InputActionType.Button, k_backBinding);
            m_action.performed += m_onPerformed;
            m_action.Enable();
        }

        /// <summary>Disables and disposes the Back action; safe to call more than once.</summary>
        public void Disable()
        {
            if (m_action == null)
            {
                return;
            }

            m_action.performed -= m_onPerformed;
            m_action.Disable();
            m_action.Dispose();
            m_action = null;
        }

        /// <summary>Closes the top overlay; with no overlay open it does nothing and returns false.</summary>
        public bool Back()
        {
            return m_navigator.CloseTop();
        }

        public void Dispose()
        {
            Disable();
        }

        private void OnPerformed(InputAction.CallbackContext context)
        {
            Back();
        }
    }
}
