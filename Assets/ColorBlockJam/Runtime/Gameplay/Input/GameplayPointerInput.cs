using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ColorBlockJam.Gameplay.Input
{
    /// <summary>
    /// Builds and owns the code-built "Gameplay" input action map and turns it into one primary
    /// pointer sample per tick (DEC-114). The Pointer bindings cover mouse, pen and the primary
    /// touch only, so a second finger never becomes the pointer (DEC-26). The same map holds the
    /// Android Back button (Escape), which the router uses to cancel targeting or hand to the pause
    /// fallback (B-12, DEC-154). It is polled, never called back, so reading it allocates nothing.
    /// </summary>
    public sealed class GameplayPointerInput : IPointerSource, IBackButtonSource, IDisposable
    {
        private readonly InputActionMap m_map;
        private readonly InputAction m_point;
        private readonly InputAction m_press;
        private readonly InputAction m_back;

        private bool m_held;
        private InputDevice m_device;
        private bool m_disposed;

        // A press released in the update it began (a tap shorter than a frame) reports Down, and the next
        // Read reports this release, so the tap still ends in Up (B-12 resolves a target on release).
        private bool m_releaseQueued;
        private PointerPhase m_queuedRelease;

        // Owner: this object, bound AsSingle in the Game scene. Stops: Disable on Suspend/Teardown,
        // Dispose when the scene container is disposed.
        public GameplayPointerInput()
        {
            m_map = new InputActionMap("Gameplay");
            // PassThrough, not Value: a Value action keeps the bound position with the largest magnitude,
            // so a resting mouse could stand in for the touch that pressed.
            m_point = m_map.AddAction("Point", InputActionType.PassThrough, "<Pointer>/position", expectedControlLayout: "Vector2");
            m_press = m_map.AddAction("Press", InputActionType.Button, "<Pointer>/press");
            // Android maps its system Back button to the keyboard Escape key.
            m_back = m_map.AddAction("Back", InputActionType.Button, "<Keyboard>/escape");
        }

        /// <summary>
        /// Enables the map with no press latched. Suspend, Won and Teardown disable the map outside any
        /// Read, so a press held then would otherwise turn the first press after Resume into Held.
        /// </summary>
        public void Enable()
        {
            m_held = false;
            m_device = null;
            m_releaseQueued = false;
            m_map.Enable();
        }

        public void Disable()
        {
            m_map.Disable();
        }

        /// <summary>
        /// Reads the primary pointer. A press that stops without a release on its own device, a
        /// switch to another device and a map disabled under a held press all report Cancel. A press
        /// released within its own update reports Down, then Up on the next Read.
        /// WasPressedThisFrame ignores enablement, so the session state stays the real gate.
        /// </summary>
        public PointerSample Read()
        {
            if (!m_map.enabled)
            {
                PointerPhase released = m_held ? PointerPhase.Cancel : PointerPhase.None;
                m_held = false;
                m_releaseQueued = false;
                return new PointerSample(released, ReadPosition());
            }

            if (!m_held)
            {
                if (!m_press.WasPressedThisFrame())
                {
                    return new PointerSample(PointerPhase.None, ReadPosition());
                }

                m_held = true;
                InputControl pressed = m_press.activeControl;
                m_device = pressed != null ? pressed.device : null;

                // Not pressed any more although it was pressed this update: the tap already ended.
                m_releaseQueued = m_press.WasReleasedThisFrame() && !m_press.IsPressed();
                if (m_releaseQueued)
                {
                    m_queuedRelease = IsCanceledTouch() ? PointerPhase.Cancel : PointerPhase.Up;
                }

                return new PointerSample(PointerPhase.Down, ReadPosition());
            }

            Vector2 position = ReadPosition();

            if (m_releaseQueued)
            {
                m_releaseQueued = false;
                m_held = false;
                return new PointerSample(m_queuedRelease, position);
            }

            InputControl active = m_press.activeControl;
            if (active != null && active.device != m_device)
            {
                m_held = false;
                return new PointerSample(PointerPhase.Cancel, position);
            }

            if (m_press.WasReleasedThisFrame())
            {
                m_held = false;
                return new PointerSample(IsCanceledTouch() ? PointerPhase.Cancel : PointerPhase.Up, position);
            }

            if (m_press.IsPressed())
            {
                return new PointerSample(PointerPhase.Held, position);
            }

            // The press vanished without a release this frame (a release in an update no Read saw, or a device reset).
            m_held = false;
            return new PointerSample(PointerPhase.Cancel, position);
        }

        /// <summary>
        /// True only in the frame Back was pressed. WasPressedThisFrame ignores enablement, so a disabled
        /// map (Suspend, Won, Teardown) reports false here.
        /// </summary>
        public bool WasBackPressed()
        {
            return m_map.enabled && m_back.WasPressedThisFrame();
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_map.Disable();
            m_map.Dispose();
        }

        // The pressing device's own position, so the press and its position always come from one
        // device (RULE-9, AC-3.19); the PassThrough action serves only before any press has latched.
        private Vector2 ReadPosition()
        {
            return m_device is Pointer pointer ? pointer.position.ReadValue() : m_point.ReadValue<Vector2>();
        }

        // The pressing device's own touch phase tells an OS-cancelled touch from a lift; no extra action is bound.
        private bool IsCanceledTouch()
        {
            if (!(m_device is Touchscreen touchscreen))
            {
                return false;
            }

            TouchControl primary = touchscreen.primaryTouch;
            return primary.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled;
        }
    }
}
