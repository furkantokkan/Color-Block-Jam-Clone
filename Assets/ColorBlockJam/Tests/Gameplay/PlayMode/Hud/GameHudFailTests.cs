using System.Collections;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Hud;
using ColorBlockJam.Gameplay.Hud.Menu;
using ColorBlockJam.Gameplay.Session;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.Gameplay.PlayMode.Hud
{
    /// <summary>
    /// The fail popup in the real Game scene composition (DEC-160): when the countdown reaches zero the session enters
    /// Failed, tears the attempt down and the Retry confirmation opens by itself with the level title; Pause is off
    /// behind it, and its Retry rebuilds the level with a full clock. The close-to-Home rule is covered by
    /// GameMenuFlowTests; the Hub transition itself by GameHudHomeTests.
    /// </summary>
    public sealed class GameHudFailTests
    {
        private GameHudScene m_scene;
        private bool m_sceneLoaded;

        // NUnit reuses the fixture instance, so the per-test scene handle starts empty.
        [SetUp]
        public void SetUp()
        {
            m_scene = null;
            m_sceneLoaded = false;
        }

        [UnityTearDown]
        public IEnumerator UnloadGameScene()
        {
            if (m_sceneLoaded)
            {
                m_sceneLoaded = false;
                yield return GameHudScene.Unload();
            }

            m_scene = null;
        }

        [UnityTest]
        public IEnumerator RunningOutOfTimeFailsAndOpensTheFailPopupWhoseRetryRestartsTheLevel()
        {
            m_sceneLoaded = true;
            yield return GameHudScene.Load(scene => m_scene = scene);
            LevelSession session = m_scene.Session;
            ICountdown countdown = m_scene.Resolve<ICountdown>();
            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing), "direct Play auto-plays (DEC-116)");
            float authored = countdown.Remaining;
            object before = session.Attempt;
            VisualElement screen = m_scene.Root.Q(RestartConfirmView.ScreenName);
            Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "hidden while playing");

            // The countdown's own path: one tick longer than the time left, as a frame at zero would be. The countdown
            // waits for a block press (DEC-189); the test stands in for it.
            session.Attempt.MarkBlockTouched();
            session.RunTick(countdown.Remaining + 1f);

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Failed), "the timer ran out");
            Assert.That(session.Attempt, Is.Null, "Failed tears the attempt down before the popup (RULE-19)");
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.RestartConfirm), "the fail popup opened by itself");
            yield return null;
            Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            string title = m_scene.Resolve<AttemptLevelNumber>().Title;
            Assert.That(title, Does.StartWith("Level "));
            Assert.That(screen.Q<Label>(RestartConfirmView.TitleName).text, Is.EqualTo(title));
            Assert.That(screen.Q<Label>(RestartConfirmView.MessageName).text, Is.EqualTo("You will lose 1 life!"));
            Assert.That(m_scene.Root.Q("settings-screen").resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                "one overlay at a time");
            Assert.That(m_scene.TopBar.View.PauseButton.enabledSelf, Is.False, "Pause is off behind the fail popup");

            GameHudScene.Click(screen.Q<Button>(RestartConfirmView.RetryButtonName));
            yield return GameHudScene.WaitUntil(() => session.State.Value == SessionState.Playing);

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing), "Retry plays the level again");
            Assert.That(session.Attempt, Is.Not.Null.And.Not.SameAs(before), "a new attempt from the authored level");
            // authored was read a few frames into the first attempt, so the new clock is at or above it.
            Assert.That(countdown.Remaining, Is.GreaterThan(authored - 1f), "a full clock");
            Assert.That(m_scene.Flow.Current, Is.EqualTo(GameOverlay.None));
            yield return null;
            Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "the popup closed");
        }
    }
}
