using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using ColorBlockJam.Hub.Launch;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Hub.PreLevel;
using ColorBlockJam.Runtime.Boosters;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.Hub
{
    /// <summary>
    /// Proves the pre-level popup behaviour of design 9.1 and 9.3 with a fake view, stock and launcher and the real
    /// navigator (contract scenarios 1, 2, 3, 5, 6 and 8; AC-1.1, AC-1.1a, AC-1.9).
    /// </summary>
    public sealed class PreLevelPresenterTests
    {
        private PreLevelTestFixtures m_fixtures;
        private BoosterDefinition m_timeFreeze;
        private BoosterDefinition m_extraTime;
        private BoosterDefinition m_rocket;
        private BoosterCatalog m_catalog;
        private FakeInventory m_inventory;
        private FakeProgress m_progress;
        private FakeLauncher m_launcher;
        private HubNavigator m_navigator;
        private FakePreLevelView m_view;
        private PreLevelPresenter m_presenter;
        private List<BoosterDefinition> m_purchaseRequests;

        [SetUp]
        public void SetUp()
        {
            m_fixtures = new PreLevelTestFixtures();
            m_timeFreeze = m_fixtures.CreateBooster(BoosterId.TimeFreeze, "Time Freeze", BoosterUseContext.InGame);
            m_extraTime = m_fixtures.CreateBooster(BoosterId.ExtraTime, "Extra Time", BoosterUseContext.PreLevel);
            m_rocket = m_fixtures.CreateBooster(BoosterId.StartingRocket, "Rocket", BoosterUseContext.PreLevel);
            m_catalog = m_fixtures.CreateCatalog(m_extraTime, m_timeFreeze, m_rocket);
            m_inventory = new FakeInventory();
            m_inventory.Set(BoosterId.TimeFreeze, 4);
            m_inventory.Set(BoosterId.ExtraTime, 2);
            m_inventory.Set(BoosterId.StartingRocket, 1);
            m_progress = new FakeProgress();
            m_launcher = new FakeLauncher();
            m_navigator = new HubNavigator();
            m_view = new FakePreLevelView();
            m_presenter = new PreLevelPresenter(m_progress, m_inventory, m_catalog, m_navigator, m_launcher);
            m_purchaseRequests = new List<BoosterDefinition>();
            m_presenter.PurchaseRequested += m_purchaseRequests.Add;
        }

        [TearDown]
        public void TearDown()
        {
            m_presenter.Dispose();
            m_navigator.Dispose();
            m_inventory.Dispose();
            m_progress.Dispose();
            m_fixtures.Dispose();
        }

        [Test]
        public void Bind_BuildsOneSelectorPerPreLevelBoosterInCatalogOrder_AndStaysHidden()
        {
            m_presenter.Bind(m_view, 5, CancellationToken.None);

            Assert.That(m_view.Labels, Is.EqualTo(new[] { "Extra Time", "Rocket" }), "Time Freeze is in-game only");
            Assert.That(m_view.Icons[0], Is.SameAs(m_extraTime.Icon));
            Assert.That(m_view.Icons[1], Is.SameAs(m_rocket.Icon));
            Assert.That(m_view.IsShown, Is.False, "binding does not open the popup");
            Assert.That(m_presenter.SelectorCount, Is.EqualTo(2));
        }

        [Test]
        public void Open_ShowsTheLevelTitle_UnselectedCounts_AndPlay()
        {
            m_progress.Index.Value = 2;
            Open(5);

            Assert.That(m_view.IsShown, Is.True);
            Assert.That(m_view.Title, Is.EqualTo("LEVEL 3"));
            Assert.That(m_view.States, Is.EqualTo(new[] { PreLevelSelectorState.Available, PreLevelSelectorState.Available }));
            Assert.That(m_view.Counts, Is.EqualTo(new[] { "2", "1" }));
            Assert.That(m_view.PlayInteractable, Is.True);
            Assert.That(m_inventory.GetStock(BoosterId.ExtraTime).Value, Is.EqualTo(2), "opening spends nothing");
        }

        [Test]
        public void Open_SavedIndexPastTheLastLevel_KeepsCounting()
        {
            m_progress.Index.Value = 9;
            Open(5);

            Assert.That(m_view.Title, Is.EqualTo("LEVEL 10"), "Level 10 replays the fifth level (DEC-173)");
        }

        [Test]
        public void SelectorTaps_ToggleIndependently_AndSpendNothing()
        {
            Open(5);

            m_view.PressSelector(0);
            Assert.That(m_view.States[0], Is.EqualTo(PreLevelSelectorState.Selected));
            Assert.That(m_view.States[1], Is.EqualTo(PreLevelSelectorState.Available));

            m_view.PressSelector(1);
            Assert.That(m_view.States[1], Is.EqualTo(PreLevelSelectorState.Selected), "both may be selected");

            m_view.PressSelector(0);
            Assert.That(m_view.States[0], Is.EqualTo(PreLevelSelectorState.Available));
            Assert.That(m_view.Counts[0], Is.EqualTo("2"), "a second tap shows the count again");
            Assert.That(m_inventory.GetStock(BoosterId.ExtraTime).Value, Is.EqualTo(2));
            Assert.That(m_inventory.GetStock(BoosterId.StartingRocket).Value, Is.EqualTo(1));
            Assert.That(m_purchaseRequests, Is.Empty);
        }

        [Test]
        public void EmptyStock_ShowsPlus_AndATapRequestsPurchaseWithoutSelecting()
        {
            m_inventory.Set(BoosterId.StartingRocket, 0);
            Open(5);

            Assert.That(m_view.States[1], Is.EqualTo(PreLevelSelectorState.OutOfStock));

            m_view.PressSelector(1);

            Assert.That(m_purchaseRequests, Is.EqualTo(new[] { m_rocket }));
            Assert.That(m_presenter.IsSelected(1), Is.False);
            Assert.That(m_view.States[1], Is.EqualTo(PreLevelSelectorState.OutOfStock));
        }

        [Test]
        public void CommittedPurchase_RefreshesTheSelector_WithoutSelectingIt()
        {
            m_inventory.Set(BoosterId.StartingRocket, 0);
            Open(5);

            m_inventory.Set(BoosterId.StartingRocket, 1);

            Assert.That(m_view.States[1], Is.EqualTo(PreLevelSelectorState.Available));
            Assert.That(m_view.Counts[1], Is.EqualTo("1"));
            Assert.That(m_presenter.IsSelected(1), Is.False, "a purchase never auto-selects");
        }

        [Test]
        public void SelectorRedraws_OnlyWhenItsLookChanges()
        {
            Open(5);
            int calls = m_view.SetSelectorCalls;

            m_inventory.Set(BoosterId.ExtraTime, 2);

            Assert.That(m_view.SetSelectorCalls, Is.EqualTo(calls));
        }

        [Test]
        public void CloseButton_ClosesThePopup_AndClearsTheSelection()
        {
            Open(5);
            m_view.PressSelector(0);

            m_view.PressClose();

            Assert.That(m_navigator.IsOpen(HubOverlayId.PreLevel), Is.False);
            Assert.That(m_view.IsShown, Is.False);
            Assert.That(m_presenter.IsSelected(0), Is.False);

            m_navigator.OpenPreLevel();
            Assert.That(m_view.States[0], Is.EqualTo(PreLevelSelectorState.Available), "reopening starts unselected");
        }

        [Test]
        public void SetCovered_HidesAndShowsTheOpenPopup_KeepingTheSelection_AndDoesNothingWhenClosed()
        {
            m_presenter.SetCovered(true);
            Assert.That(m_presenter.IsCovered, Is.False, "a closed popup is never covered");
            Assert.That(m_view.IsShown, Is.False);

            Open(5);
            m_view.PressSelector(0);

            m_presenter.SetCovered(true);
            Assert.That(m_view.IsShown, Is.False);
            Assert.That(m_presenter.IsShown, Is.True, "covered is still open");

            m_presenter.SetCovered(false);
            Assert.That(m_view.IsShown, Is.True);
            Assert.That(m_presenter.IsSelected(0), Is.True);
            Assert.That(m_view.States[0], Is.EqualTo(PreLevelSelectorState.Selected));
        }

        [Test]
        public void Back_ClosesThePopup_AndClearsTheSelection()
        {
            Open(5);
            m_view.PressSelector(1);

            Assert.That(m_navigator.CloseTop(), Is.True);

            Assert.That(m_view.IsShown, Is.False);
            Assert.That(m_presenter.IsSelected(1), Is.False);
        }

        [Test]
        public void Play_LaunchesOnce_WithTheLevelAndTheSelectedBoostersInSelectorOrder()
        {
            m_progress.Index.Value = 3;
            Open(5);
            m_view.PressSelector(1);
            m_view.PressSelector(0);

            m_view.PressPlay();
            m_view.PressPlay();

            Assert.That(m_launcher.Requests.Count, Is.EqualTo(1), "a second Play does nothing");
            Assert.That(m_launcher.Requests[0].LevelIndex, Is.EqualTo(3));
            Assert.That(m_launcher.Requests[0].StartingBoosters, Is.EqualTo(new[] { BoosterId.ExtraTime, BoosterId.StartingRocket }),
                "selector order, not tap order");
            Assert.That(m_view.PlayInteractable, Is.False);
            Assert.That(m_inventory.GetStock(BoosterId.ExtraTime).Value, Is.EqualTo(2), "Play spends nothing");
            Assert.That(m_inventory.GetStock(BoosterId.StartingRocket).Value, Is.EqualTo(1), "Play spends nothing");
        }

        [Test]
        public void Play_WithNoSelection_CarriesNoStartingBoosters()
        {
            Open(5);

            m_view.PressPlay();

            Assert.That(m_launcher.Requests[0].LevelIndex, Is.EqualTo(0));
            Assert.That(m_launcher.Requests[0].StartingBoosters, Is.Empty);
        }

        [Test]
        public void Play_WithNoLevels_DoesNothing()
        {
            Open(0);

            m_view.PressPlay();

            Assert.That(m_view.PlayInteractable, Is.False);
            Assert.That(m_launcher.Requests, Is.Empty);
        }

        [Test]
        public void SelectorTaps_WhileLaunching_AreIgnored()
        {
            Open(5);
            m_view.PressPlay();

            m_view.PressSelector(0);

            Assert.That(m_presenter.IsSelected(0), Is.False);
        }

        [UnityTest]
        public IEnumerator FailedLaunch_EnablesPlayAgain()
        {
            Open(5);
            m_view.PressPlay();

            m_launcher.Complete(LevelLaunchResult.Failed);
            yield return PreLevelTestFixtures.Await(m_presenter.PendingLaunch);

            Assert.That(m_view.PlayInteractable, Is.True);
            m_view.PressPlay();
            Assert.That(m_launcher.Requests.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator CompletedLaunch_KeepsPlayDisabled()
        {
            Open(5);
            m_view.PressPlay();

            m_launcher.Complete(LevelLaunchResult.Completed);
            yield return PreLevelTestFixtures.Await(m_presenter.PendingLaunch);

            Assert.That(m_view.PlayInteractable, Is.False);
        }

        [Test]
        public void Unbind_DisposesSubscriptions_DetachesTheView_AndRemovesTheSelectors()
        {
            Open(5);
            m_navigator.Unregister(HubOverlayId.PreLevel);

            m_presenter.Unbind();

            Assert.That(m_inventory.LiveSubscriptions(BoosterId.ExtraTime), Is.Zero);
            Assert.That(m_inventory.LiveSubscriptions(BoosterId.StartingRocket), Is.Zero);
            Assert.That(m_view.Handlers, Is.Zero);
            Assert.That(m_view.Labels, Is.Empty, "the selectors are removed");
            Assert.That(m_launcher.Requests, Is.Empty);
        }

        [Test]
        public void Bind_Twice_Throws()
        {
            m_presenter.Bind(m_view, 5, CancellationToken.None);

            Assert.Throws<InvalidOperationException>(() => m_presenter.Bind(new FakePreLevelView(), 5, CancellationToken.None));
        }

        private void Open(int levelCount)
        {
            m_presenter.Bind(m_view, levelCount, CancellationToken.None);
            m_navigator.Register(HubOverlayId.PreLevel, m_presenter);
            Assert.That(m_navigator.OpenPreLevel(), Is.True);
        }
    }
}
