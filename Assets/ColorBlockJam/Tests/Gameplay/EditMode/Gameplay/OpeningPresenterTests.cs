using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// The opening layer (B-16, reference 11, user request of 2026-09-28): one icon per starting booster the committed
    /// plan includes, in booster catalog order, and the dimmed layer only while the session's opening intro dims the
    /// screen, before the boosters apply; nothing for a plain Play.
    /// </summary>
    public sealed class OpeningPresenterTests
    {
        private static readonly string[] s_board =
        {
            "AA.",
            "..."
        };

        private SessionFixture m_fixture;
        private BoosterFixture m_boosters;
        private OpeningPresenter m_presenter;
        private FakeGameplayHudView m_view;
        private Texture2D m_texture;
        private Sprite m_rocketIcon;
        private Sprite m_extraTimeIcon;

        [SetUp]
        public void SetUp()
        {
            m_fixture = new SessionFixture(s_board, new DoorSpec[0], clock: new AttemptCountdown());
            m_boosters = new BoosterFixture();
            m_texture = new Texture2D(4, 4);
            m_rocketIcon = Sprite.Create(m_texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            m_extraTimeIcon = Sprite.Create(m_texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            SetIcon(m_boosters.StartingRocket, m_rocketIcon);
            SetIcon(m_boosters.ExtraTime, m_extraTimeIcon);
            m_presenter = new OpeningPresenter(m_fixture.Session, m_boosters.Catalog);
            m_view = new FakeGameplayHudView();
            Assert.That(m_fixture.Session.TryPrepare(m_fixture.Level, out string error), Is.True, error);
            m_presenter.Bind(m_view);
        }

        [TearDown]
        public void TearDown()
        {
            m_presenter.Dispose();
            m_boosters.Dispose();
            m_fixture.Dispose();
            Object.DestroyImmediate(m_rocketIcon);
            Object.DestroyImmediate(m_extraTimeIcon);
            Object.DestroyImmediate(m_texture);
        }

        [Test]
        public void TheDimShowsOnlyUntilItFadesBeforeTheBoostersApply()
        {
            IBoardReader board = m_fixture.Session.Attempt.Simulation.Board;
            OpeningPlan plan = new OpeningPlan(true, 10, true, GameCommand.RemoveCell(0, board.GetOrigin(0), new Cell(0, 1)));

            m_presenter.SetPlan(plan);
            Assert.That(m_view.Opening, Is.False, "Home shows no opening yet");

            Assert.That(m_fixture.Session.Play(plan), Is.True);
            Assert.That(m_view.Opening, Is.True);

            float dimmed = plan.IntroSeconds - OpeningPlan.k_dimFadeSeconds;
            m_fixture.TickIdle(dimmed - 0.1f);
            Assert.That(m_view.Opening, Is.True, "the intro still dims the screen");

            m_fixture.TickIdle(0.15f);
            Assert.That(m_view.Opening, Is.False, "the dim fades before the boosters apply");
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.Zero, "the Rocket waits for the fade");
            Assert.That(m_fixture.Session.IsOpening.Value, Is.True, "input stays off");
        }

        [Test]
        public void EveryIncludedBoosterGetsOneIconInCatalogOrder()
        {
            m_presenter.SetPlan(new OpeningPlan(true, 10, true, GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(0, 0))));

            // The fixture's catalog lists Starting Rocket before Extra Time.
            Assert.That(m_view.OpeningIcons, Is.EqualTo(new[] { m_rocketIcon, m_extraTimeIcon }));

            m_presenter.SetPlan(new OpeningPlan(true, 10, false, default));
            Assert.That(m_view.OpeningIcons, Is.EqualTo(new[] { m_extraTimeIcon }));
        }

        [Test]
        public void APlainPlayShowsNoOpening()
        {
            m_presenter.SetPlan(OpeningPlan.None);
            Assert.That(m_fixture.Session.Play(), Is.True);

            Assert.That(m_view.OpeningIcons, Is.Empty);
            Assert.That(m_view.Opening, Is.False);
        }

        [Test]
        public void TeardownEndsTheOpening()
        {
            Assert.That(m_fixture.Session.Play(new OpeningPlan(true, 10, false, default)), Is.True);
            Assert.That(m_fixture.Session.Pause(PauseReason.User), Is.True);
            Assert.That(m_view.Opening, Is.True, "pause keeps the intro");

            Assert.That(m_fixture.Session.GoHome(), Is.True);

            Assert.That(m_view.Opening, Is.False);
        }

        private static void SetIcon(BoosterDefinition booster, Sprite icon)
        {
            SerializedObject serialized = new SerializedObject(booster);
            serialized.FindProperty("m_icon").objectReferenceValue = icon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
