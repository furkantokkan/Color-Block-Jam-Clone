using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using SessionState = ColorBlockJam.Domain.Session.SessionState;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// The booster bar and targeting banner (B-12, B-22, B-23, AC-1, AC-2): slots follow the catalog's in-game boosters
    /// in order, show the shared stock as a count or a plus badge, select the targeting booster, show its banner copy,
    /// disable while the attempt cannot take a use and flash on a blocked press. Clicks only reach the use coordinator.
    /// </summary>
    public sealed class BoosterBarPresenterTests
    {
        // A two-cell block A and a one-cell block B.
        private static readonly string[] s_board =
        {
            "AA.",
            "...",
            "..B"
        };

        private readonly List<Object> m_created = new List<Object>();
        private SessionFixture m_fixture;
        private BoosterFixture m_boosters;
        private BoosterUseCoordinator m_coordinator;
        private BoosterBarPresenter m_presenter;
        private FakeGameplayHudView m_view;

        [TearDown]
        public void TearDown()
        {
            m_presenter?.Dispose();
            m_boosters?.Dispose();
            m_fixture?.Dispose();
            for (int index = m_created.Count - 1; index >= 0; index--)
            {
                if (m_created[index] != null)
                {
                    Object.DestroyImmediate(m_created[index]);
                }
            }

            m_created.Clear();
            m_presenter = null;
            m_boosters = null;
            m_fixture = null;
        }

        [Test]
        public void TheBarShowsTimeFreezeRocketHammerAndVacuumInCatalogOrder()
        {
            Create();

            Assert.That(m_presenter.SlotCount, Is.EqualTo(4));
            Assert.That(Slots(), Is.EqualTo(new[] { BoosterId.TimeFreeze, BoosterId.Rocket, BoosterId.Hammer, BoosterId.Vacuum }));
            Assert.That(m_view.SlotShown, Is.All.True);
        }

        [Test]
        public void APreLevelBoosterFirstInTheCatalogTakesNoSlot()
        {
            m_boosters = new BoosterFixture();
            BoosterCatalog catalog = CreateCatalog(m_boosters.ExtraTime, m_boosters.TimeFreeze, m_boosters.Rocket,
                m_boosters.Hammer, m_boosters.Vacuum);
            Create(catalog: catalog);

            Assert.That(Slots(), Is.EqualTo(new[] { BoosterId.TimeFreeze, BoosterId.Rocket, BoosterId.Hammer, BoosterId.Vacuum }));
        }

        [Test]
        public void SlotsFollowTheCatalogOrderAndHideWhatItLeavesUnused()
        {
            m_boosters = new BoosterFixture();
            BoosterCatalog catalog = CreateCatalog(m_boosters.Vacuum, m_boosters.ExtraTime, m_boosters.TimeFreeze, m_boosters.Rocket);
            Create(catalog: catalog);

            Assert.That(Slots(), Is.EqualTo(new[] { BoosterId.Vacuum, BoosterId.TimeFreeze, BoosterId.Rocket }));
            Assert.That(m_view.SlotShown, Is.EqualTo(new[] { true, true, true, false }));
        }

        [Test]
        public void EachSlotShowsItsSharedStockOrThePlusBadge()
        {
            Create();

            Assert.That(m_view.SlotCounts, Is.EqualTo(new[] { "1", "1", "1", "1" }));
            Assert.That(m_view.SlotEmpty, Is.All.False);

            m_boosters.Store.SetStock(BoosterId.Rocket, 0);
            Assert.That(m_view.SlotEmpty[1], Is.True, "zero stock shows the plus badge");
            Assert.That(m_view.SlotCounts[1], Is.EqualTo("0"));

            m_boosters.Store.SetStock(BoosterId.Rocket, 12);
            Assert.That(m_view.SlotEmpty[1], Is.False);
            Assert.That(m_view.SlotCounts[1], Is.EqualTo("12"));

            m_boosters.Store.SetStock(BoosterId.Hammer, 150);
            Assert.That(m_view.SlotCounts[2], Is.EqualTo("99"), "the text table ends at the stock limit");
        }

        [Test]
        public void SlotsTakePressesOnlyWhilePlayingAndNoWriteIsPending()
        {
            Create(play: false);
            Assert.That(m_view.SlotEnabled, Is.All.False, "Home");

            Assert.That(m_fixture.Session.Play(), Is.True);
            Assert.That(m_view.SlotEnabled, Is.All.True, "Playing");

            m_boosters.Store.SetSaving(true);
            Assert.That(m_view.SlotEnabled, Is.All.False, "a player-data write is pending");
            m_boosters.Store.SetSaving(false);
            Assert.That(m_view.SlotEnabled, Is.All.True);

            Assert.That(m_fixture.Session.Pause(PauseReason.User), Is.True);
            Assert.That(m_view.SlotEnabled, Is.All.False, "Paused");
            Assert.That(m_fixture.Session.Resume(), Is.True);
            Assert.That(m_view.SlotEnabled, Is.All.True);
        }

        [Test]
        public void TheOpeningHoldDisablesTheBar()
        {
            Create(play: false);
            Assert.That(m_fixture.Session.Play(new OpeningPlan(true, 10, false, default)), Is.True);
            Assert.That(m_view.SlotEnabled, Is.All.False, "the hold keeps HUD booster input off (B-16)");

            m_fixture.TickIdle(OpeningPlan.k_defaultIntroSeconds);
            m_fixture.TickIdle();
            m_fixture.TickIdle(OpeningPlan.k_defaultHoldSeconds + 0.1f);

            Assert.That(m_fixture.Session.IsOpening.Value, Is.False);
            Assert.That(m_view.SlotEnabled, Is.All.True);
        }

        [UnityTest]
        public IEnumerator TheBarWaitsForTheDebitAndTimeFreezeWaitsForTheFreezeToEnd()
        {
            Create();
            m_boosters.Store.Gate = new TaskCompletionSource<bool>();

            m_view.Click(0);

            Assert.That(m_coordinator.IsBusy.Value, Is.True);
            Assert.That(m_fixture.Session.PauseReason, Is.EqualTo(PauseReason.Saving));
            Assert.That(m_view.SlotEnabled, Is.All.False, "no second use while the debit is written");

            m_boosters.Store.Gate.SetResult(true);
            yield return BoosterFixture.WaitForSave(m_coordinator);
            m_fixture.TickIdle();

            Assert.That(m_fixture.Session.IsFrozen.Value, Is.True);
            Assert.That(m_view.SlotEnabled, Is.EqualTo(new[] { false, true, true, true }), "only Time Freeze waits");
            Assert.That(m_view.SlotCounts[0], Is.EqualTo("0"));
            Assert.That(m_view.SlotEmpty[0], Is.True);
        }

        [Test]
        public void TargetingSelectsItsSlotAndShowsItsBannerCopy()
        {
            m_boosters = new BoosterFixture();
            Sprite rocketIcon = SetIcon(m_boosters.Rocket);
            Sprite hammerIcon = SetIcon(m_boosters.Hammer);
            Sprite vacuumIcon = SetIcon(m_boosters.Vacuum);
            Create();

            m_view.Click(1);
            Assert.That(m_view.SlotSelected, Is.EqualTo(new[] { false, true, false, false }));
            Assert.That(m_view.BannerShown, Is.True);
            Assert.That(m_view.BannerTitle, Is.EqualTo("ROCKET"));
            Assert.That(m_view.BannerInstruction, Is.EqualTo("Tap and destroy one unit of a block!"));
            Assert.That(m_view.BannerIcon, Is.SameAs(rocketIcon), "the banner shows the booster's own icon (AC-2)");

            m_view.Click(2);
            Assert.That(m_view.SlotSelected, Is.EqualTo(new[] { false, false, true, false }), "switching is free");
            Assert.That(m_view.BannerTitle, Is.EqualTo("HAMMER"));
            Assert.That(m_view.BannerInstruction, Is.EqualTo("Tap any block to destroy it!"));
            Assert.That(m_view.BannerIcon, Is.SameAs(hammerIcon));

            m_view.Click(3);
            Assert.That(m_view.BannerTitle, Is.EqualTo("VACUUM"));
            Assert.That(m_view.BannerInstruction, Is.EqualTo("Tap and vacuum blocks with the same color!"));
            Assert.That(m_view.BannerIcon, Is.SameAs(vacuumIcon));

            m_view.Click(3);
            Assert.That(m_view.SlotSelected, Is.All.False, "the same button cancels");
            Assert.That(m_view.BannerShown, Is.False);
            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.None));
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.Zero, "a click never changes the board");
            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero, "selection is free");
        }

        [Test]
        public void TheBannerCloseButtonCancelsTargetingForFree()
        {
            Create();
            m_view.Click(2);

            m_view.Close();

            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.None));
            Assert.That(m_view.BannerShown, Is.False);
            Assert.That(m_view.SlotSelected, Is.All.False);
            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero);
        }

        [Test]
        public void SuspendEndsTheSelection()
        {
            Create();
            m_view.Click(1);

            Assert.That(m_fixture.Session.Pause(PauseReason.User), Is.True);

            Assert.That(m_view.SlotSelected, Is.All.False);
            Assert.That(m_view.BannerShown, Is.False);
        }

        [Test]
        public void AZeroStockPressWithoutAPurchaseFlashesAndPausesNothing()
        {
            Create(stock: 0);
            m_boosters.Host.Available = false;

            m_view.Click(2);

            Assert.That(m_view.BlockedCounts, Is.EqualTo(new[] { 0, 0, 1, 0 }));
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.None));
            Assert.That(m_boosters.Store.ConsumeCount, Is.Zero);
        }

        [Test]
        public void APressOnHomeDoesNothing()
        {
            Create(play: false);

            m_view.Click(1);

            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.None));
            Assert.That(m_view.BlockedCounts, Is.All.EqualTo(0));
        }

        [Test]
        public void UnbindDetachesTheViewAndItsSubscriptions()
        {
            Create();

            m_presenter.Unbind();
            m_boosters.Store.SetStock(BoosterId.Rocket, 7);

            Assert.That(m_view.HasPressListeners, Is.False);
            Assert.That(m_view.SlotCounts[1], Is.EqualTo("1"), "no write after unbind");
            Assert.That(m_presenter.IsBound, Is.False);
        }

        private void Create(bool play = true, int stock = 1, BoosterCatalog catalog = null)
        {
            m_fixture = new SessionFixture(s_board, new DoorSpec[0], clock: new AttemptCountdown(), withTargeting: true);
            m_boosters ??= new BoosterFixture(stock);
            m_coordinator = m_boosters.CreateCoordinator(m_fixture);
            m_presenter = new BoosterBarPresenter(m_fixture.Session, m_fixture.Targeting, m_coordinator,
                catalog != null ? catalog : m_boosters.Catalog, m_boosters.Store, m_boosters.Store);
            m_view = new FakeGameplayHudView();
            Assert.That(m_fixture.Session.TryPrepare(m_fixture.Level, out string error), Is.True, error);
            m_presenter.Bind(m_view, new HudText(99));
            if (play)
            {
                Assert.That(m_fixture.Session.Play(), Is.True);
            }
        }

        private BoosterId[] Slots()
        {
            BoosterId[] slots = new BoosterId[m_presenter.SlotCount];
            for (int slot = 0; slot < slots.Length; slot++)
            {
                slots[slot] = m_presenter.GetSlotBooster(slot);
            }

            return slots;
        }

        // A distinct throwaway sprite on the definition's icon, so a banner showing another booster's icon fails.
        private Sprite SetIcon(BoosterDefinition definition)
        {
            Texture2D texture = new Texture2D(4, 4);
            m_created.Add(texture);
            Sprite icon = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            icon.name = definition.name + " icon";
            m_created.Add(icon);
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("m_icon").objectReferenceValue = icon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return icon;
        }

        private BoosterCatalog CreateCatalog(params BoosterDefinition[] definitions)
        {
            BoosterCatalog catalog = ScriptableObject.CreateInstance<BoosterCatalog>();
            m_created.Add(catalog);
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty list = serialized.FindProperty("m_definitions");
            list.arraySize = definitions.Length;
            for (int index = 0; index < definitions.Length; index++)
            {
                list.GetArrayElementAtIndex(index).objectReferenceValue = definitions[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }
    }
}
