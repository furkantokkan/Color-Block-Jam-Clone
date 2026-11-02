using System;
using UnityEngine.InputSystem;

namespace ColorBlockJam.Gameplay.Hud.Menu
{
    /// <summary>
    /// Android Back while an in-game overlay shows (Game HUD design 5): one code-built <c>&lt;Keyboard&gt;/escape</c>
    /// action whose <c>performed</c> callback raises <see cref="Pressed"/>. The gameplay map and its polled Back are off
    /// under Paused, so an overlay needs its own action, as the purchase popup has.
    /// </summary>
    /// <remarks>
    /// Owner: whichever overlay flow needs it. The Game scene container binds one AsSingle instance (HUD branch) for
    /// <c>GameMenuFlow</c>, which enables it while its overlay shows and disables it on hide; <c>LevelCompleteFlow</c>
    /// and <c>InGamePurchaseHost</c> each construct and own a private instance instead (DEC-154's one-handler rule), so
    /// their Back press cannot fire the shared instance's handler. Stops: <see cref="Disable"/>, and
    /// <see cref="Dispose"/> removes the callback and disposes the action. An action enabled after a press does not see
    /// that press, so one Back is handled once. Nothing runs per frame.
    /// </remarks>
    public sealed class OverlayBackInput : IOverlayBackInput, IDisposable
    {
        private const string k_actionName = "GameOverlayBack";
        private const string k_binding = "<Keyboard>/escape";

        private readonly InputAction m_action;
        private readonly Action<InputAction.CallbackContext> m_onPerformed;
        private bool m_disposed;

        public OverlayBackInput()
        {
            m_action = new InputAction(k_actionName, InputActionType.Button, k_binding);
            m_onPerformed = OnPerformed;
            m_action.performed += m_onPerformed;
        }

        public event Action Pressed;

        public bool IsEnabled => !m_disposed && m_action.enabled;

        public void Enable()
        {
            if (!m_disposed)
            {
                m_action.Enable();
            }
        }

        public void Disable()
        {
            if (!m_disposed)
            {
                m_action.Disable();
            }
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_action.performed -= m_onPerformed;
            m_action.Disable();
            m_action.Dispose();
            Pressed = null;
        }

        private void OnPerformed(InputAction.CallbackContext context)
        {
            Pressed?.Invoke();
        }
    }
}
