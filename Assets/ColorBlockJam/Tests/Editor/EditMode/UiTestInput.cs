using System;
using System.Reflection;
using ColorBlockJam.Editor.Controls;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// One shared set of UI Toolkit test senders for the Level Editor's window and canvas fixtures (Tests-Editor-1-F2,
    /// Tests-Editor-1-F13, Tests-Editor-2-F2, Tests-Editor-3-F1): a real panel layout pass, pointer down/move/up,
    /// button presses, key presses and the shared coloured-icon-button assertion.
    /// </summary>
    internal static class UiTestInput
    {
        private const BindingFlags k_panelMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Runs the panel's layout pass now instead of waiting for the next repaint (batch mode never repaints).</summary>
        internal static void ValidateLayout(VisualElement root)
        {
            object panel = root.panel;
            panel.GetType().GetMethod("ValidateLayout", k_panelMembers).Invoke(panel, null);
        }

        /// <summary>A left click (button 0) unless told otherwise: pointer down then up at the same position.</summary>
        internal static void Click(VisualElement root, Vector2 position, int button = 0,
            EventModifiers modifiers = EventModifiers.None)
        {
            SendPointer(root, EventType.MouseDown, position, button, modifiers);
            SendPointer(root, EventType.MouseUp, position, button, modifiers);
        }

        /// <summary>Button 1 is a right click: it removes a whole gate or ends a Barrier erase stroke, for example.</summary>
        internal static void RightClick(VisualElement root, Vector2 position)
        {
            Click(root, position, 1);
        }

        /// <summary>Submits a button the way a click does, wherever the tools view is scrolled.</summary>
        internal static void Press(Button button)
        {
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        /// <summary>
        /// Sends one pointer event. MouseDown, MouseUp, MouseMove and MouseDrag are mapped explicitly; any other
        /// EventType throws instead of silently falling back to one of them (Tests-Editor-1-F2, Tests-Editor-2-F2).
        /// </summary>
        internal static void SendPointer(VisualElement root, EventType type, Vector2 position, int button = 0,
            EventModifiers modifiers = EventModifiers.None)
        {
            Event mouse = new Event
            {
                type = type, mousePosition = position, button = button, clickCount = 1, modifiers = modifiers
            };
            EventBase pointer;
            switch (type)
            {
                case EventType.MouseDown:
                    pointer = PointerDownEvent.GetPooled(mouse);
                    break;
                case EventType.MouseUp:
                    pointer = PointerUpEvent.GetPooled(mouse);
                    break;
                case EventType.MouseMove:
                case EventType.MouseDrag:
                    pointer = PointerMoveEvent.GetPooled(mouse);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type,
                        "SendPointer maps only MouseDown, MouseUp, MouseMove and MouseDrag.");
            }

            using (pointer)
            {
                root.SendEvent(pointer);
            }
        }

        /// <summary>UI Toolkit routes keyboard events to the focused element, so the target takes focus first.</summary>
        internal static void SendKey(VisualElement target, KeyCode key, EventModifiers modifiers = EventModifiers.None)
        {
            target.Focus();
            using (KeyDownEvent down = KeyDownEvent.GetPooled('\0', key, modifiers))
            {
                down.target = target;
                target.SendEvent(down);
            }
        }

        /// <summary>
        /// The coloured icon replaces the glyph and is the whole button: no box or hover square shows around it, and
        /// its fixed size keeps it from stretching the row to 64 px.
        /// </summary>
        internal static void AssertShowsIcon(Button button, string iconName)
        {
            Image icon = button.Q<Image>(ActionIcons.k_imageName);
            Assert.That(icon, Is.Not.Null, $"{button.name} shows an icon.");
            Assert.That(icon.image, Is.Not.Null.And.SameAs(ActionIcons.Load(iconName)));
            Assert.That(button.text, Is.Empty, "No + or × glyph is drawn beside the icon.");
            Assert.That(icon.style.height.value.value, Is.LessThan(icon.image.height));
            Assert.That(button.style.backgroundColor.value.a, Is.Zero, "No button box shows around the round icon.");
            Assert.That(button.style.borderLeftWidth.value, Is.Zero);
            Assert.That(button.style.width.value.value, Is.EqualTo(icon.style.width.value.value), "No gap beside the icon.");
        }
    }
}
