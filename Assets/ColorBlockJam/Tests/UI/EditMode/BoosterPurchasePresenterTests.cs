using System;
using System.Collections;
using System.Threading;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Currencies;
using ColorBlockJam.UI.Purchase;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.SharedUI
{
    /// <summary>
    /// Proves the shared purchase popup behaviour of design 9.2 with a fake view, shop and stock (contract scenarios 3,
    /// 4 and 12; AC-6.7 to AC-6.9, DEC-85): the offer shown, each BoosterPurchaseResult message, Buy disabled while any
    /// save is pending, no double submit, stale results ignored, and every subscription and wait released on Unbind.
    /// </summary>
    public sealed class BoosterPurchasePresenterTests
    {
        private PurchaseTestAssets m_assets;
        private CurrencyDefinition m_coin;
        private BoosterDefinition m_rocket;
        private FakeBoosterShop m_shop;
        private FakeBoosterPurchaseView m_view;
        private BoosterPurchasePresenter m_presenter;
        private int m_closeRequests;

        [SetUp]
        public void SetUp()
        {
            m_assets = new PurchaseTestAssets();
            m_coin = m_assets.CreateCoin();
            m_rocket = m_assets.CreateBooster(BoosterId.Rocket, "Rocket", m_coin, 10);
            m_shop = new FakeBoosterShop(0);
            m_view = new FakeBoosterPurchaseView();
            m_presenter = new BoosterPurchasePresenter(m_shop, m_shop);
            m_presenter.CloseRequested += () => m_closeRequests++;
            m_presenter.Bind(m_view, CancellationToken.None);
            m_closeRequests = 0;
        }

        [TearDown]
        public void TearDown()
        {
            m_presenter.Dispose();
            m_shop.Dispose();
            m_assets.Dispose();
        }

        [Test]
        public void Bind_StartsHidden_WithBuyDisabled()
        {
            Assert.That(m_view.IsVisible, Is.False);
            Assert.That(m_view.BuyInteractable, Is.False);
            Assert.That(m_presenter.IsVisible, Is.False);
        }

        [Test]
        public void Show_WritesTheOffer_NameIconAmountDescriptionCoinAndPrice()
        {
            Assert.That(m_presenter.Show(m_rocket), Is.True);

            Assert.That(m_view.IsVisible, Is.True);
            Assert.That(m_view.Title, Is.EqualTo("Rocket"));
            Assert.That(m_view.Icon, Is.SameAs(m_rocket.Icon));
            Assert.That(m_view.Amount, Is.EqualTo("x1"));
            Assert.That(m_view.Description, Is.EqualTo("Rocket description."));
            Assert.That(m_view.CurrencyIcon, Is.SameAs(m_coin.Icon));
            Assert.That(m_view.Price, Is.EqualTo("10"));
            Assert.That(m_view.Message, Is.Empty);
            Assert.That(m_view.BuyInteractable, Is.True);
            Assert.That(m_presenter.Booster, Is.SameAs(m_rocket));
        }

        [Test]
        public void Show_WithNoBooster_ReturnsFalse()
        {
            Assert.That(m_presenter.Show(null), Is.False);
            Assert.That(m_view.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator Buy_Purchased_StaysOpen_ShowsTheNewCount_AndSelectsNothing()
        {
            m_presenter.Show(m_rocket);

            m_view.PressBuy();
            Assert.That(m_shop.Calls, Is.EqualTo(1));
            Assert.That(m_shop.LastBooster, Is.SameAs(m_rocket));
            Assert.That(m_view.BuyInteractable, Is.False, "Buy is disabled while the purchase saves");

            m_shop.Complete(BoosterPurchaseResult.Purchased);
            yield return PurchaseTestAssets.Await(m_presenter.PendingPurchase);

            Assert.That(m_shop.Stock, Is.EqualTo(1));
            Assert.That(m_view.Message, Is.EqualTo(BoosterPurchasePresenter.PurchasedPrefix + "1."));
            Assert.That(m_view.IsVisible, Is.True, "the popup stays open (AC-6.9)");
            Assert.That(m_closeRequests, Is.Zero);
            Assert.That(m_view.BuyInteractable, Is.True, "another charge can be bought");
        }

        [UnityTest]
        public IEnumerator Buy_InsufficientFunds_ShowsNotEnoughCoins_AndChangesNothing()
        {
            yield return BuyAndComplete(BoosterPurchaseResult.InsufficientFunds);

            Assert.That(m_view.Message, Is.EqualTo("Not enough coins"));
        }

        [UnityTest]
        public IEnumerator Buy_StockFull_ShowsTheMaximumMessage()
        {
            yield return BuyAndComplete(BoosterPurchaseResult.StockFull);

            Assert.That(m_view.Message, Is.EqualTo(BoosterPurchasePresenter.StockFullMessage));
        }

        [UnityTest]
        public IEnumerator Buy_SaveFailed_ShowsTheRetryMessage()
        {
            yield return BuyAndComplete(BoosterPurchaseResult.SaveFailed);

            Assert.That(m_view.Message, Is.EqualTo(BoosterPurchasePresenter.SaveFailedMessage));
        }

        [UnityTest]
        public IEnumerator Buy_InvalidOffer_ShowsTheUnavailableMessage()
        {
            yield return BuyAndComplete(BoosterPurchaseResult.InvalidOffer);

            Assert.That(m_view.Message, Is.EqualTo(BoosterPurchasePresenter.InvalidOfferMessage));
        }

        [UnityTest]
        public IEnumerator Buy_Cancelled_ShowsNoMessage()
        {
            yield return BuyAndComplete(BoosterPurchaseResult.Cancelled);

            Assert.That(m_view.Message, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Buy_ShopThrows_LogsAndShowsTheRetryMessage()
        {
            m_presenter.Show(m_rocket);
            m_view.PressBuy();

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: disk gone");
            m_shop.Fail(new InvalidOperationException("disk gone"));
            yield return PurchaseTestAssets.Await(m_presenter.PendingPurchase);

            Assert.That(m_view.Message, Is.EqualTo(BoosterPurchasePresenter.SaveFailedMessage));
            Assert.That(m_view.BuyInteractable, Is.True);
        }

        [Test]
        public void Buy_IsDisabledAndIgnoredWhileAnySaveIsPending()
        {
            m_presenter.Show(m_rocket);

            m_shop.SetSaving(true);
            Assert.That(m_view.BuyInteractable, Is.False);
            m_view.PressBuy();
            Assert.That(m_shop.Calls, Is.Zero);

            m_shop.SetSaving(false);
            Assert.That(m_view.BuyInteractable, Is.True);
        }

        [Test]
        public void Buy_WhileItsOwnPurchaseIsPending_IsIgnored()
        {
            m_presenter.Show(m_rocket);

            m_view.PressBuy();
            m_view.PressBuy();

            Assert.That(m_shop.Calls, Is.EqualTo(1), "no double submit");
        }

        [Test]
        public void Buy_WhileHidden_DoesNothing()
        {
            m_view.PressBuy();

            Assert.That(m_shop.Calls, Is.Zero);
        }

        [Test]
        public void CloseButton_RaisesCloseRequested_AndHideClearsTheOffer()
        {
            m_presenter.Show(m_rocket);

            m_view.PressClose();
            Assert.That(m_closeRequests, Is.EqualTo(1), "the host decides what closing means");
            Assert.That(m_view.IsVisible, Is.True, "the presenter does not close itself");

            m_presenter.Hide();
            Assert.That(m_view.IsVisible, Is.False);
            Assert.That(m_view.BuyInteractable, Is.False);
            Assert.That(m_presenter.Booster, Is.Null);
        }

        [UnityTest]
        public IEnumerator ResultAfterHiding_ShowsNoMessage()
        {
            m_presenter.Show(m_rocket);
            m_view.PressBuy();
            m_presenter.Hide();

            m_shop.Complete(BoosterPurchaseResult.InsufficientFunds);
            yield return PurchaseTestAssets.Await(m_presenter.PendingPurchase);

            Assert.That(m_view.Message, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Unbind_CancelsThePendingWait_AndDetachesTheView()
        {
            m_presenter.Show(m_rocket);
            m_view.PressBuy();

            m_presenter.Unbind();
            yield return PurchaseTestAssets.Await(m_presenter.PendingPurchase);

            Assert.That(m_shop.LastToken.IsCancellationRequested, Is.True);
            Assert.That(m_view.BuyHandlers, Is.Zero);
            Assert.That(m_presenter.Show(m_rocket), Is.False, "an unbound popup cannot show");
        }

        [Test]
        public void HostTokenCancellation_CancelsThePurchaseWait()
        {
            m_presenter.Unbind();
            using (var host = new CancellationTokenSource())
            {
                m_presenter.Bind(m_view, host.Token);
                m_presenter.Show(m_rocket);
                m_view.PressBuy();

                host.Cancel();

                Assert.That(m_shop.LastToken.IsCancellationRequested, Is.True);
            }
        }

        [Test]
        public void Bind_Twice_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => m_presenter.Bind(new FakeBoosterPurchaseView(), CancellationToken.None));
        }

        private IEnumerator BuyAndComplete(BoosterPurchaseResult result)
        {
            m_presenter.Show(m_rocket);
            m_view.PressBuy();
            m_shop.Complete(result);
            yield return PurchaseTestAssets.Await(m_presenter.PendingPurchase);

            Assert.That(m_shop.Stock, Is.Zero, "nothing changes on " + result);
            Assert.That(m_view.IsVisible, Is.True);
            Assert.That(m_view.BuyInteractable, Is.True);
        }
    }
}
