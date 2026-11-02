using System;
using ColorBlockJam.Gameplay.Hud.Result;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.Gameplay.Hud
{
    /// <summary>
    /// The Level Complete host over a clone of GameplayHud.uxml (reference 05, DEC-173): the HUD holds the popup, every
    /// element the view needs exists, there is no separate Home button, and the one button's label, reward row and the
    /// note follow the flow's writes.
    /// </summary>
    public sealed class LevelCompleteHostTests
    {
        private const string k_hudPath = "Assets/ColorBlockJam/UI/Game/GameplayHud.uxml";

        private LevelCompleteHost m_host;
        private VisualElement m_tree;

        [SetUp]
        public void SetUp()
        {
            VisualTreeAsset hud = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_hudPath);
            Assert.That(hud, Is.Not.Null, k_hudPath);
            m_tree = hud.CloneTree();
            m_host = new LevelCompleteHost(m_tree);
        }

        [TearDown]
        public void TearDown()
        {
            m_host.Dispose();
        }

        [Test]
        public void TheHudHoldsThePopupAndItStartsHidden()
        {
            VisualElement screen = m_tree.Q(LevelCompleteView.ScreenName);

            Assert.That(screen, Is.Not.Null, "GameplayHud.uxml holds LevelCompletePopup.uxml");
            Assert.That(screen.Q(LevelCompleteView.ConfettiName), Is.Not.Null, "the confetti surface");
            Assert.That(screen.Q(LevelCompleteView.CoinsName), Is.Not.Null, "the coin pile the confetti bursts from");
            Assert.That(screen.Q("level-complete-home"), Is.Null, "reference 05 has no separate Home button");
            Assert.That(m_host.IsShowing, Is.False);
            Assert.That(m_host.Confetti, Is.Null, "a host over a bare tree plays no confetti");
        }

        [Test]
        public void ShowBindsTheViewAndWritesTheReward()
        {
            Assert.That(m_host.Show("10"), Is.True);

            LevelCompleteView view = m_host.View;
            Assert.That(view, Is.Not.Null);
            Assert.That(m_tree.Q(LevelCompleteView.ScreenName).style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(m_tree.Q<Label>(LevelCompleteView.RewardLabelName).text, Is.EqualTo("10"));
            Assert.That(m_tree.Q<Label>(LevelCompleteView.RewardLabelBackName).text, Is.EqualTo("10"));
            Assert.That(m_tree.Q(LevelCompleteView.RewardName).style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.PrimaryText, Is.EqualTo(LevelCompleteView.ContinueText));
        }

        [Test]
        public void TheButtonReadsContinueRetryOrHomeAndOnlyRetryHidesTheReward()
        {
            m_host.Show("10");
            VisualElement reward = m_tree.Q(LevelCompleteView.RewardName);

            m_host.SetPrimary(LevelCompleteAction.Retry, true);
            Assert.That(m_host.View.PrimaryText, Is.EqualTo(LevelCompleteView.RetryText));
            Assert.That(m_tree.Q<Label>(LevelCompleteView.ContinueLabelBackName).text, Is.EqualTo(LevelCompleteView.RetryText));
            Assert.That(reward.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(m_host.View.IsPrimaryEnabled, Is.True);

            m_host.SetPrimary(LevelCompleteAction.Home, false);
            Assert.That(m_host.View.PrimaryText, Is.EqualTo(LevelCompleteView.HomeText));
            Assert.That(reward.style.display.value, Is.EqualTo(DisplayStyle.Flex), "Home still collects the reward");
            Assert.That(m_host.View.IsPrimaryEnabled, Is.False);

            m_host.SetPrimary(LevelCompleteAction.Continue, true);
            Assert.That(m_host.View.PrimaryText, Is.EqualTo(LevelCompleteView.ContinueText));
            Assert.That(reward.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(m_host.View.IsPrimaryEnabled, Is.True);
        }

        [Test]
        public void AWinWithoutARewardShowsNoRewardRow()
        {
            m_host.Show(null);
            m_host.SetPrimary(LevelCompleteAction.Home, true);

            Assert.That(m_tree.Q(LevelCompleteView.RewardName).style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(m_host.View.RewardText, Is.Empty);
        }

        [Test]
        public void TheNoteShowsOnlyWithText()
        {
            m_host.Show("10");
            VisualElement box = m_tree.Q(LevelCompleteView.MessageBoxName);
            Assert.That(box.style.display.value, Is.EqualTo(DisplayStyle.None));

            m_host.SetMessage("More levels coming soon");
            Assert.That(box.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(m_host.View.Message, Is.EqualTo("More levels coming soon"));
            Assert.That(m_tree.Q<Label>(LevelCompleteView.MessageBackName).text, Is.EqualTo("More levels coming soon"));

            m_host.SetMessage(null);
            Assert.That(box.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheHudRootCarriesTheResultClassOnlyWhileThePopupShows()
        {
            Assert.That(m_tree.ClassListContains(LevelCompleteHost.ResultClassName), Is.False);

            m_host.Show("10");
            Assert.That(m_tree.ClassListContains(LevelCompleteHost.ResultClassName), Is.True,
                "GameplayHud.uss fades the booster bar under the result");

            m_host.Hide();
            Assert.That(m_tree.ClassListContains(LevelCompleteHost.ResultClassName), Is.False);
        }

        [Test]
        public void HideAndDisposeLeaveThePopupHidden()
        {
            int presses = 0;
            Action onPrimary = () => presses++;
            m_host.PrimaryRequested += onPrimary;
            m_host.Show("10");

            m_host.Hide();
            Assert.That(m_host.IsShowing, Is.False);
            Assert.That(m_tree.Q(LevelCompleteView.ScreenName).style.display.value, Is.EqualTo(DisplayStyle.None));

            m_host.Dispose();
            Assert.That(m_host.Show("10"), Is.False);
            Assert.That(presses, Is.EqualTo(0));
        }
    }
}
