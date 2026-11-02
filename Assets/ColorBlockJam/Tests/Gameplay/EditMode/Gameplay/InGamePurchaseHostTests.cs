using System.Text.RegularExpressions;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using SessionState = ColorBlockJam.Domain.Session.SessionState;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// InGamePurchaseHost over a clone of GameplayHud.uxml with the real shared popup view, a session and the use
    /// coordinator on fake player ports (AC-12, B-18, B-32): a zero-stock press shows that booster's offer under
    /// Paused(Purchase) and never casts or selects; Buy follows the shop's saving flag; close and Android Back hide the
    /// popup and resume, a backgrounded close keeps the background pause, and Home, Restart, a foreground resume and
    /// Dispose hide it. Clicks on Buy, the close button and the scrim need a laid-out panel and are covered by the
    /// PlayMode host tests. InputTestFixture isolates the Input System for the Back action.
    /// </summary>
    public sealed class InGamePurchaseHostTests : InputTestFixture
    {
        private const string k_hudPath = "Assets/ColorBlockJam/UI/Game/GameplayHud.uxml";

        // The shared popup's element names (BoosterPurchaseView); this test assembly does not reference ColorBlockJam.UI.
        private const string k_screenName = "booster-purchase-screen";
        private const string k_titleName = "booster-purchase-title";
        private const string k_buyName = "booster-purchase-buy";
        private const string k_messageName = "booster-purchase-message";

        // A two-cell block A at (0, 1)-(1, 1) and a one-cell block B at (3, 0).
        private static readonly string[] s_board =
        {
            "....",
            "AA..",
            "...B"
        };

        private SessionFixture m_session;
        private BoosterFixture m_boosters;
        private VisualElement m_tree;
        private InGamePurchaseHost m_host;
        private BoosterUseCoordinator m_coordinator;

        public override void TearDown()
        {
            m_coordinator?.Dispose();
            m_host?.Dispose();
            m_boosters?.Dispose();
            m_session?.Dispose();
            m_coordinator = null;
            m_host = null;
            m_boosters = null;
            m_session = null;
            m_tree = null;
            base.TearDown();
        }

        [Test]
        public void AZeroStockPressShowsThatBoostersOfferUnderPausedPurchaseWithoutCastingOrSelecting()
        {
            Create();
            m_boosters.Store.SetStock(BoosterId.Rocket, 0);
            Assert.That(m_coordinator.Press(BoosterId.Hammer), Is.EqualTo(BoosterPressResult.TargetingStarted));

            Assert.That(m_coordinator.Press(BoosterId.Rocket), Is.EqualTo(BoosterPressResult.PurchaseOpened));

            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_session.Session.PauseReason, Is.EqualTo(PauseReason.Purchase));
            Assert.That(m_session.Targeting.Mode.Value, Is.EqualTo(BoosterId.None), "the empty press ended Hammer targeting");
            Assert.That(m_host.IsBound, Is.True, "the first show binds the HUD's popup instance");
            Assert.That(m_host.IsShowing, Is.True);
            Assert.That(m_host.Booster, Is.SameAs(m_boosters.Rocket));
            Assert.That(m_host.IsBackEnabled, Is.True, "Back closes the popup while it shows");
            Assert.That(PopupScreen().style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(m_tree.Q<Label>(k_titleName).text, Is.EqualTo(m_boosters.Rocket.DisplayName));
            Assert.That(m_tree.Q<Label>(k_messageName).style.visibility.value, Is.EqualTo(Visibility.Hidden),
                "a new offer shows no feedback");
            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero, "opening the purchase spends nothing");

            Assert.That(m_host.TryShow(m_boosters.Hammer), Is.False, "one offer at a time");
            Assert.That(m_host.Booster, Is.SameAs(m_boosters.Rocket));
        }

        [Test]
        public void BuyIsDisabledWhilePlayerDataIsSaving()
        {
            Create();
            OpenRocketPurchase();
            Button buy = m_tree.Q<Button>(k_buyName);
            Assert.That(buy.enabledSelf, Is.True);

            m_boosters.Store.SetSaving(true);
            Assert.That(buy.enabledSelf, Is.False, "a pending write refuses a second submission");

            m_boosters.Store.SetSaving(false);
            Assert.That(buy.enabledSelf, Is.True);
        }

        [Test]
        public void CloseHidesThePopupAndResumesPlayWithoutCastingOrSelecting()
        {
            Create();
            OpenRocketPurchase();

            Assert.That(m_host.Close(), Is.True);

            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_host.IsShowing, Is.False);
            Assert.That(m_host.IsBackEnabled, Is.False);
            Assert.That(PopupScreen().style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(m_session.Targeting.Mode.Value, Is.EqualTo(BoosterId.None), "no automatic selection");
            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero, "no automatic cast");
            Assert.That(m_boosters.Store.GetStock(BoosterId.Rocket).Value, Is.Zero);
            Assert.That(m_host.Close(), Is.False, "nothing is left to close");
        }

        [Test]
        public void AndroidBackClosesThePopupAndResumesPlay()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Create();
            OpenRocketPurchase();

            Press(keyboard.escapeKey);

            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_host.IsShowing, Is.False);
            Assert.That(m_host.IsBackEnabled, Is.False);

            Release(keyboard.escapeKey);
            Press(keyboard.escapeKey);
            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Playing), "Back without the popup does nothing here");
        }

        [Test]
        public void ClosingAfterABackgroundHidesThePopupAndKeepsTheBackgroundPause()
        {
            Create(resumesOnForeground: false);
            OpenRocketPurchase();
            m_session.Session.NotifyBackground(true);
            Assert.That(m_host.IsShowing, Is.True, "backgrounding keeps the popup");

            Assert.That(m_host.Close(), Is.False, "the ordinary background rules decide");

            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_session.Session.PauseReason, Is.EqualTo(PauseReason.Background));
            Assert.That(m_host.IsShowing, Is.False);
            m_session.Session.NotifyBackground(false);
            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Paused), "no automatic resume");
        }

        [Test]
        public void AForegroundResumeHidesThePopup()
        {
            Create(resumesOnForeground: true);
            OpenRocketPurchase();
            m_session.Session.NotifyBackground(true);

            // The interim overlay resumes a background pause on foreground (DEC-116).
            m_session.Session.NotifyBackground(false);

            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_host.IsShowing, Is.False, "no popup over a running game");
            Assert.That(m_host.IsBackEnabled, Is.False);
            Assert.That(PopupScreen().style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void RestartAndHomeHideThePopupAndKeepTheStock()
        {
            Create();
            OpenRocketPurchase();

            Assert.That(m_session.Session.Restart(), Is.True);
            Assert.That(m_host.IsShowing, Is.False, "Restart tears the attempt down");
            Assert.That(m_boosters.Store.GetStock(BoosterId.Rocket).Value, Is.Zero, "Restart never refills stock");

            Assert.That(m_coordinator.Press(BoosterId.Rocket), Is.EqualTo(BoosterPressResult.PurchaseOpened),
                "the rebuilt attempt offers the purchase again");
            Assert.That(m_session.Session.GoHome(), Is.True);

            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Home));
            Assert.That(m_host.IsShowing, Is.False);
            Assert.That(m_host.IsBackEnabled, Is.False);
            Assert.That(PopupScreen().style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void DisposeHidesThePopupAndRefusesLaterShows()
        {
            Create();
            OpenRocketPurchase();

            m_host.Dispose();

            Assert.That(m_host.IsShowing, Is.False);
            Assert.That(m_host.IsBackEnabled, Is.False);
            Assert.That(PopupScreen().style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(m_host.TryShow(m_boosters.Rocket), Is.False);
            Assert.That(m_host.Close(), Is.False);
            Assert.DoesNotThrow(() => m_host.Hide());
        }

        [Test]
        public void AHudWithoutThePopupBlocksTheZeroStockPressAndLogsOnce()
        {
            Create(tree: new VisualElement());
            m_boosters.Store.SetStock(BoosterId.Rocket, 0);
            LogAssert.Expect(LogType.Error, new Regex("no booster purchase popup"));

            Assert.That(m_coordinator.Press(BoosterId.Rocket), Is.EqualTo(BoosterPressResult.Blocked));
            Assert.That(m_coordinator.Press(BoosterId.Rocket), Is.EqualTo(BoosterPressResult.Blocked));

            Assert.That(m_session.Session.State.Value, Is.EqualTo(SessionState.Playing), "nothing pauses");
            Assert.That(m_host.IsBound, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private void Create(bool resumesOnForeground = true, VisualElement tree = null)
        {
            m_session = new SessionFixture(s_board, new DoorSpec[0], resumesOnForeground, withTargeting: true);
            m_boosters = new BoosterFixture();
            if (tree == null)
            {
                VisualTreeAsset hud = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_hudPath);
                Assert.That(hud, Is.Not.Null, k_hudPath);
                tree = hud.CloneTree();
                Assert.That(tree.Q(k_screenName), Is.Not.Null, "GameplayHud.uxml holds the shared purchase popup");
            }

            m_tree = tree;
            m_host = new InGamePurchaseHost(m_tree, m_session.Session, m_boosters.Store, m_boosters.Store);
            m_coordinator = new BoosterUseCoordinator(m_session.Session, m_boosters.Store, m_boosters.Store,
                m_boosters.Store, m_boosters.Catalog, m_session.Targeting, m_host);
            m_session.PrepareAndPlay();
        }

        private void OpenRocketPurchase()
        {
            m_boosters.Store.SetStock(BoosterId.Rocket, 0);
            Assert.That(m_coordinator.Press(BoosterId.Rocket), Is.EqualTo(BoosterPressResult.PurchaseOpened));
            Assert.That(m_host.IsShowing, Is.True);
        }

        private VisualElement PopupScreen()
        {
            VisualElement screen = m_tree.Q(k_screenName);
            Assert.That(screen, Is.Not.Null, k_screenName);
            return screen;
        }
    }
}
