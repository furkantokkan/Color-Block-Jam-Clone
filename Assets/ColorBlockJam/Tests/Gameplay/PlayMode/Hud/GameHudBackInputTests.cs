using System.Collections;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Hud.Menu;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.Gameplay.PlayMode.Hud
{
    /// <summary>
    /// Android Back (keyboard Escape) in the real Game scene with a simulated keyboard (contract AC-6, Game HUD design
    /// 5): during Playing without targeting the router's fallback opens the pause menu, the next Escape resumes through
    /// the overlay Back action, and one press is handled once; with targeting, Escape only cancels it. InputTestFixture
    /// isolates the Input System. In a play-mode [UnityTest] the fixture only queues key events, so each press or
    /// release is processed by the player loop's input update of a later frame, which the gameplay tick then polls.
    /// </summary>
    public sealed class GameHudBackInputTests : InputTestFixture
    {
        private Keyboard m_keyboard;
        private GameHudScene m_scene;
        private bool m_sceneLoaded;
        private HudStockArrangement m_stock;

        public override void Setup()
        {
            base.Setup();
            m_keyboard = InputSystem.AddDevice<Keyboard>();
            m_scene = null;
            m_sceneLoaded = false;
            m_stock = new HudStockArrangement();
        }

        [UnityTearDown]
        public IEnumerator UnloadGameScene()
        {
            m_stock.Restore();
            if (m_sceneLoaded)
            {
                m_sceneLoaded = false;
                yield return GameHudScene.Unload();
            }

            m_scene = null;
        }

        [UnityTest]
        public IEnumerator EscapeOpensThePauseMenuAndTheNextEscapeClosesItOnce()
        {
            yield return Load();
            LevelSession session = m_scene.Session;

            Press(m_keyboard.escapeKey, queueEventOnly: true);
            yield return GameHudScene.WaitUntil(() => session.State.Value == SessionState.Paused);

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Paused), "Back without targeting pauses");
            Assert.That(session.PauseReason, Is.EqualTo(PauseReason.User));
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.PauseMenu));
            Assert.That(GameHudScene.IsOverlayBackEnabled(), Is.True);
            yield return null;
            Assert.That(m_scene.Root.Q("settings-screen").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.PauseMenu),
                "the press that opened the menu does not also close it");

            // InputTestFixture forces queueEventOnly in a play-mode [UnityTest], so the release and the next press reach
            // the overlay action only in a later player-loop input update.
            Release(m_keyboard.escapeKey, queueEventOnly: true);
            yield return null;
            Assert.That(session.State.Value, Is.EqualTo(SessionState.Paused), "releasing the opening press closes nothing");
            Press(m_keyboard.escapeKey, queueEventOnly: true);
            yield return GameHudScene.WaitUntil(() => session.State.Value == SessionState.Playing);

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing), "overlay Back resumes");
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.None));
            Assert.That(GameHudScene.IsOverlayBackEnabled(), Is.False);

            Release(m_keyboard.escapeKey, queueEventOnly: true);
            yield return null;
            yield return null;

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing),
                "the resuming press does not reach the gameplay Back as well");
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.None));
        }

        [UnityTest]
        public IEnumerator EscapeDuringTargetingOnlyCancelsTheTargeting()
        {
            yield return Load();
            m_stock.Arrange(m_scene.Resolve<PlayerSaveService>(), BoosterId.Rocket, 1);
            yield return null;
            BoosterTargeting targeting = m_scene.Scope.Container.Resolve<BoosterTargeting>();
            BoosterUseCoordinator coordinator = m_scene.Scope.Container.Resolve<BoosterUseCoordinator>();
            Assert.That(coordinator.Press(BoosterId.Rocket), Is.EqualTo(BoosterPressResult.TargetingStarted));

            Press(m_keyboard.escapeKey, queueEventOnly: true);
            yield return GameHudScene.WaitUntil(() => targeting.Mode.Value == BoosterId.None);
            Release(m_keyboard.escapeKey);
            yield return null;

            Assert.That(targeting.Mode.Value, Is.EqualTo(BoosterId.None), "Back cancelled the targeting");
            Assert.That(m_scene.Session.State.Value, Is.EqualTo(SessionState.Playing), "and opened no menu");
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.None));
        }

        private IEnumerator Load()
        {
            m_sceneLoaded = true;
            yield return GameHudScene.Load(scene => m_scene = scene);
            Assert.That(m_scene.Session.State.Value, Is.EqualTo(SessionState.Playing), "direct Play auto-plays (DEC-116)");
        }
    }
}
