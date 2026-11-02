using System.Collections;
using System.Threading;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Hub.PreLevel;
using ColorBlockJam.Hub.Purchase;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Currencies;
using ColorBlockJam.UI.Purchase;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.Hub
{
    /// <summary>
    /// Proves the Hub adapter of the shared purchase popup with the real navigator and pre-level presenter (contract
    /// scenarios 3, 4, 5 and 12; DEC-136): it opens above the pre-level popup for one booster and covers it, so the
    /// purchase popup shows alone (user feedback 2026-09-27); close and Back show the pre-level popup again with its
    /// selection, and a committed purchase refreshes the selector without selecting it.
    /// </summary>
    public sealed class BoosterPurchaseOverlayTests
    {
        private PreLevelTestFixtures m_fixtures;
        private CurrencyDefinition m_coin;
        private BoosterDefinition m_extraTime;
        private BoosterDefinition m_rocket;
        private FakeInventory m_inventory;
        private FakeShop m_shop;
        private FakeProgress m_progress;
        private FakeLauncher m_launcher;
        private HubNavigator m_navigator;
        private BoosterPurchasePresenter m_purchasePresenter;
        private BoosterPurchaseOverlay m_overlay;
        private FakePurchaseView m_purchaseView;
        private PreLevelPresenter m_preLevel;
        private FakePreLevelView m_preLevelView;

        [SetUp]
        public void SetUp()
        {
            m_fixtures = new PreLevelTestFixtures();
            m_coin = m_fixtures.CreateCoin();
            m_extraTime = m_fixtures.CreateBooster(BoosterId.ExtraTime, "Extra Time", BoosterUseContext.PreLevel, m_coin, 10);
            m_rocket = m_fixtures.CreateBooster(BoosterId.StartingRocket, "Rocket", BoosterUseContext.PreLevel, m_coin, 10);
            m_inventory = new FakeInventory();
            m_inventory.Set(BoosterId.ExtraTime, 2);
            m_inventory.Set(BoosterId.StartingRocket, 0);
            m_shop = new FakeShop(m_inventory);
            m_progress = new FakeProgress();
            m_launcher = new FakeLauncher();
            m_navigator = new HubNavigator();

            m_purchasePresenter = new BoosterPurchasePresenter(m_shop, m_inventory);
            m_overlay = new BoosterPurchaseOverlay(m_purchasePresenter, m_navigator);
            m_purchaseView = new FakePurchaseView();
            m_overlay.Bind(m_purchaseView, CancellationToken.None);
            m_navigator.Register(HubOverlayId.BoosterPurchase, m_overlay);

            // As HubScreen wires it: the pre-level purchase request opens this overlay.
            m_preLevel = new PreLevelPresenter(m_progress, m_inventory, m_fixtures.CreateCatalog(m_extraTime, m_rocket),
                m_navigator, m_launcher);
            m_preLevelView = new FakePreLevelView();
            m_preLevel.Bind(m_preLevelView, 5, CancellationToken.None);
            m_navigator.Register(HubOverlayId.PreLevel, m_preLevel);
            m_preLevel.PurchaseRequested += booster => m_overlay.Open(booster, m_preLevel);
            m_navigator.OpenPreLevel();
        }

        [TearDown]
        public void TearDown()
        {
            m_preLevel.Dispose();
            m_overlay.Dispose();
            m_purchasePresenter.Dispose();
            m_navigator.Dispose();
            m_shop.Dispose();
            m_progress.Dispose();
            m_inventory.Dispose();
            m_fixtures.Dispose();
        }

        [Test]
        public void EmptySelectorTap_OpensThePurchasePopupAlone_WithThePreLevelPopupCovered()
        {
            m_preLevelView.PressSelector(1);

            Assert.That(m_navigator.OpenCount, Is.EqualTo(2));
            Assert.That(m_navigator.IsOpen(HubOverlayId.BoosterPurchase), Is.True);
            Assert.That(m_purchaseView.IsVisible, Is.True);
            Assert.That(m_purchaseView.Title, Is.EqualTo("Rocket"));
            Assert.That(m_purchasePresenter.Booster, Is.SameAs(m_rocket));
            Assert.That(m_navigator.IsOpen(HubOverlayId.PreLevel), Is.True, "the pre-level popup stays open below");
            Assert.That(m_preLevel.IsShown && m_preLevel.IsCovered, Is.True);
            Assert.That(m_preLevelView.IsShown, Is.False, "the covered pre-level popup is hidden: no overlap");
            Assert.That(m_preLevel.IsSelected(1), Is.False);
        }

        [Test]
        public void Covered_KeepsTheSelection_AndIgnoresPlayAndSelectorPresses()
        {
            m_preLevelView.PressSelector(0);
            m_overlay.Open(m_rocket, m_preLevel);

            m_preLevelView.PressSelector(0);
            m_preLevelView.PressPlay();

            Assert.That(m_preLevel.IsSelected(0), Is.True, "covering keeps the selection");
            Assert.That(m_launcher.Requests.Count, Is.Zero, "a covered popup never launches");
        }

        [Test]
        public void UnregisteringThePurchasePopup_ShowsThePreLevelPopupAgain()
        {
            m_overlay.Open(m_rocket, m_preLevel);

            m_navigator.Unregister(HubOverlayId.BoosterPurchase);

            Assert.That(m_purchaseView.IsVisible, Is.False);
            Assert.That(m_preLevel.IsCovered, Is.False);
            Assert.That(m_preLevelView.IsShown, Is.True);
        }

        [Test]
        public void Open_Twice_OrWithNoBooster_ReturnsFalse()
        {
            Assert.That(m_overlay.Open(null, m_preLevel), Is.False);
            Assert.That(m_overlay.Open(m_rocket, m_preLevel), Is.True);
            Assert.That(m_overlay.Open(m_rocket, m_preLevel), Is.False);
            Assert.That(m_navigator.OpenCount, Is.EqualTo(2));
        }

        [Test]
        public void CloseButton_ReturnsToThePreLevelPopup()
        {
            m_overlay.Open(m_rocket, m_preLevel);

            m_purchaseView.PressClose();

            Assert.That(m_navigator.IsOpen(HubOverlayId.BoosterPurchase), Is.False);
            Assert.That(m_purchaseView.IsVisible, Is.False);
            Assert.That(m_navigator.IsOpen(HubOverlayId.PreLevel), Is.True);
            Assert.That(m_preLevelView.IsShown, Is.True, "the pre-level popup shows again");
            Assert.That(m_preLevel.IsCovered, Is.False);
            Assert.That(m_purchasePresenter.Booster, Is.Null);
        }

        [Test]
        public void Back_ClosesOnlyThePurchasePopup_ThenThePreLevelPopup()
        {
            m_preLevelView.PressSelector(0);
            m_overlay.Open(m_rocket, m_preLevel);
            Assert.That(m_preLevelView.IsShown, Is.False);

            Assert.That(m_navigator.CloseTop(), Is.True);
            Assert.That(m_purchaseView.IsVisible, Is.False);
            Assert.That(m_preLevelView.IsShown, Is.True);
            Assert.That(m_preLevel.IsSelected(0), Is.True, "closing the purchase popup keeps the pre-level selection");

            Assert.That(m_navigator.CloseTop(), Is.True);
            Assert.That(m_preLevelView.IsShown, Is.False);
            Assert.That(m_preLevel.IsSelected(0), Is.False, "closing the pre-level popup clears the selection");
        }

        [UnityTest]
        public IEnumerator Purchase_RefreshesTheSelectorCount_WithoutSelecting_AndStaysOpen()
        {
            m_preLevelView.PressSelector(1);

            m_purchaseView.PressBuy();
            Assert.That(m_shop.Calls, Is.EqualTo(1));
            m_shop.Complete(BoosterPurchaseResult.Purchased);
            yield return PreLevelTestFixtures.Await(m_purchasePresenter.PendingPurchase);

            Assert.That(m_navigator.IsOpen(HubOverlayId.BoosterPurchase), Is.True, "the popup stays open (AC-6.9)");
            Assert.That(m_purchaseView.Message, Is.EqualTo(BoosterPurchasePresenter.PurchasedPrefix + "1."));
            Assert.That(m_preLevelView.States[1], Is.EqualTo(PreLevelSelectorState.Available));
            Assert.That(m_preLevelView.Counts[1], Is.EqualTo("1"));
            Assert.That(m_preLevel.IsSelected(1), Is.False, "nothing is auto-selected");
        }

        [UnityTest]
        public IEnumerator NotEnoughCoins_ChangesNothing()
        {
            m_preLevelView.PressSelector(1);

            m_purchaseView.PressBuy();
            m_shop.Complete(BoosterPurchaseResult.InsufficientFunds);
            yield return PreLevelTestFixtures.Await(m_purchasePresenter.PendingPurchase);

            Assert.That(m_purchaseView.Message, Is.EqualTo("Not enough coins"));
            Assert.That(m_inventory.GetStock(BoosterId.StartingRocket).Value, Is.Zero);
            Assert.That(m_preLevelView.States[1], Is.EqualTo(PreLevelSelectorState.OutOfStock));
        }

        [Test]
        public void Unbind_DetachesTheCloseRequest_AndOpenReturnsFalse()
        {
            m_navigator.Unregister(HubOverlayId.BoosterPurchase);

            m_overlay.Unbind();
            m_purchaseView.PressClose();

            Assert.That(m_overlay.IsBound, Is.False);
            Assert.That(m_purchasePresenter.IsBound, Is.False);
            Assert.That(m_overlay.Open(m_rocket, m_preLevel), Is.False);
        }
    }
}
