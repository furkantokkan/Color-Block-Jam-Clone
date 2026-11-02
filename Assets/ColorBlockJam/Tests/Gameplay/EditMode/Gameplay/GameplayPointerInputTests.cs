using ColorBlockJam.Gameplay.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>Drives the code-built gameplay map with simulated devices; InputTestFixture isolates the Input System per test.</summary>
    public sealed class GameplayPointerInputTests : InputTestFixture
    {
        private static readonly Vector2 s_first = new Vector2(120f, 340f);
        // Farther from the screen origin than s_first, so its vector length is the larger one.
        private static readonly Vector2 s_second = new Vector2(600f, 900f);

        [Test]
        public void AMousePressReportsDownHeldAndUp()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            using (GameplayPointerInput input = CreateEnabledInput())
            {
                Move(mouse.position, s_first);
                Press(mouse.leftButton);
                PointerSample down = input.Read();
                Assert.That(down.Phase, Is.EqualTo(PointerPhase.Down));
                Assert.That(down.ScreenPosition, Is.EqualTo(s_first));

                InputSystem.Update();
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.Held));

                Release(mouse.leftButton);
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.Up));

                InputSystem.Update();
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.None));
            }
        }

        [Test]
        public void ATouchReportsDownHeldAndUp()
        {
            InputSystem.AddDevice<Touchscreen>();
            using (GameplayPointerInput input = CreateEnabledInput())
            {
                BeginTouch(1, s_first);
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.Down));

                InputSystem.Update();
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.Held));

                EndTouch(1, s_first);
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.Up));
            }
        }

        [Test]
        public void AMousePressReleasedWithinOneUpdateReportsDownThenUp()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            using (GameplayPointerInput input = CreateEnabledInput())
            {
                Move(mouse.position, s_first);
                Press(mouse.leftButton, queueEventOnly: true);
                Release(mouse.leftButton, queueEventOnly: true);
                InputSystem.Update();

                PointerSample down = input.Read();
                Assert.That(down.Phase, Is.EqualTo(PointerPhase.Down));
                Assert.That(down.ScreenPosition, Is.EqualTo(s_first));

                InputSystem.Update();
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.Up), "a tap shorter than a frame is a release");

                InputSystem.Update();
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.None));
            }
        }

        [Test]
        public void ATouchEndedWithinOneUpdateReportsDownThenUp()
        {
            InputSystem.AddDevice<Touchscreen>();
            using (GameplayPointerInput input = CreateEnabledInput())
            {
                BeginTouch(1, s_first, queueEventOnly: true);
                EndTouch(1, s_first, queueEventOnly: true);
                InputSystem.Update();

                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.Down));

                InputSystem.Update();
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.Up), "a tap shorter than a frame is a release");
            }
        }

        [Test]
        public void DisablingTheMapDropsAQueuedRelease()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            using (GameplayPointerInput input = CreateEnabledInput())
            {
                Press(mouse.leftButton, queueEventOnly: true);
                Release(mouse.leftButton, queueEventOnly: true);
                InputSystem.Update();
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.Down));

                // Suspend disables the map under the tap before its release was read.
                input.Disable();

                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.Cancel));
                input.Enable();
                InputSystem.Update();
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.None), "nothing from the dropped tap survives");
            }
        }

        [Test]
        public void ASecondTouchNeverBecomesThePointer()
        {
            InputSystem.AddDevice<Touchscreen>();
            using (GameplayPointerInput input = CreateEnabledInput())
            {
                BeginTouch(1, s_first);
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.Down));

                BeginTouch(2, s_second);
                PointerSample held = input.Read();

                Assert.That(held.Phase, Is.EqualTo(PointerPhase.Held));
                Assert.That(held.ScreenPosition, Is.EqualTo(s_first));
            }
        }

        [Test]
        public void ACancelledTouchReportsCancel()
        {
            InputSystem.AddDevice<Touchscreen>();
            using (GameplayPointerInput input = CreateEnabledInput())
            {
                BeginTouch(1, s_first);
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.Down));

                CancelTouch(1, s_first);

                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.Cancel));
            }
        }

        [Test]
        public void DisablingTheMapCancelsAHeldPointerOnce()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            using (GameplayPointerInput input = CreateEnabledInput())
            {
                Press(mouse.leftButton);
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.Down));

                input.Disable();

                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.Cancel));
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.None));
            }
        }

        [Test]
        public void ATouchDownReportsTheTouchPositionWhileAMouseRestsFartherOut()
        {
            // A Value action would keep the mouse position here: its magnitude is the larger one.
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.AddDevice<Touchscreen>();
            using (GameplayPointerInput input = CreateEnabledInput())
            {
                Move(mouse.position, s_second);
                BeginTouch(1, s_first);

                PointerSample down = input.Read();

                Assert.That(down.Phase, Is.EqualTo(PointerPhase.Down));
                Assert.That(down.ScreenPosition, Is.EqualTo(s_first));
            }
        }

        [Test]
        public void AFreshPressAfterDisableAndEnableUnderAHeldPressReportsDown()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            using (GameplayPointerInput input = CreateEnabledInput())
            {
                Press(mouse.leftButton);
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.Down));

                // Suspend or Won disables the map outside any Read, then the finger lifts while disabled.
                input.Disable();
                Release(mouse.leftButton);
                input.Enable();
                Move(mouse.position, s_second);
                Press(mouse.leftButton);

                PointerSample down = input.Read();

                Assert.That(down.Phase, Is.EqualTo(PointerPhase.Down), "the first press after Enable picks");
                Assert.That(down.ScreenPosition, Is.EqualTo(s_second));
            }
        }

        [Test]
        public void BackReportsOnePressOnlyInItsFrame()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            using (GameplayPointerInput input = CreateEnabledInput())
            {
                Assert.That(input.WasBackPressed(), Is.False);

                Press(keyboard.escapeKey);
                Assert.That(input.WasBackPressed(), Is.True);

                InputSystem.Update();
                Assert.That(input.WasBackPressed(), Is.False, "a held Back is not a new press");
                Assert.That(input.Read().Phase, Is.EqualTo(PointerPhase.None), "Back is no pointer press");
            }
        }

        [Test]
        public void BackIsIgnoredWhileTheMapIsDisabled()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            using (GameplayPointerInput input = CreateEnabledInput())
            {
                input.Disable();

                Press(keyboard.escapeKey);

                Assert.That(input.WasBackPressed(), Is.False);
            }
        }

        [Test]
        public void BackPressedThenDisabledInTheSameFrameIsIgnored()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            using (GameplayPointerInput input = CreateEnabledInput())
            {
                Press(keyboard.escapeKey);

                // Suspend disables the map in the frame of the press; the action still reports it pressed this frame.
                input.Disable();

                Assert.That(input.WasBackPressed(), Is.False);
            }
        }

        private static GameplayPointerInput CreateEnabledInput()
        {
            GameplayPointerInput input = new GameplayPointerInput();
            input.Enable();
            return input;
        }
    }
}
