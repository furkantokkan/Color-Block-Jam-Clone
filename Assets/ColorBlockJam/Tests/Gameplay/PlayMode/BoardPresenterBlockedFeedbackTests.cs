#if UNITY_EDITOR
using System.Collections;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Presentation.Boosters;
using ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures;
using NUnit.Framework;
using PrimeTween;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.Gameplay.PlayMode
{
    /// <summary>
    /// Refused-press feedback and the frost's place in the presenter's Suspend and Teardown steps (B-12, B-25): a block no
    /// drag gate holds, such as a one-cell block refused as a Rocket target, punches its root once and settles back on its
    /// cell, while a frozen block gets one punch of its root from the Ice view alone, which carries the shell and returns
    /// the root to its cell when it ends or Teardown stops it. The Game scene's presenter pauses and stops the Time Freeze
    /// frost's fade with the attempt's other tweens.
    /// </summary>
    public sealed class BoardPresenterBlockedFeedbackTests
    {
        private BoosterPresenterScene m_scene;
        private Material m_frostMaterial;
        private TimeFreezeFrostView m_frost;
        private BoardPresenter m_frostPresenter;

        [SetUp]
        public void SetUp()
        {
            m_scene = new BoosterPresenterScene();
        }

        [TearDown]
        public void TearDown()
        {
            m_frostPresenter?.Dispose();
            m_frost?.Dispose();
            if (m_frostMaterial != null)
            {
                Object.Destroy(m_frostMaterial);
            }

            m_scene.Dispose();
        }

        [UnityTest]
        public IEnumerator AnUngatedBlockPunchesItsRootOnceAndSettlesOnItsCell()
        {
            BoardSimulation simulation = m_scene.Build(BoardPresenterBoosterTests.HammerLevel(ice: false));
            Transform root = m_scene.Presenter.GetPiece(1).transform;
            Vector3 cell = root.localPosition;

            m_scene.Presenter.ShowBlockedDrag(1);

            Assert.That(Tween.GetTweensCount(root), Is.EqualTo(1), "a rejected Rocket target on a plain block is not silent");
            bool moved = false;
            float deadline = Time.realtimeSinceStartup + 1f;
            while (Tween.GetTweensCount(root) > 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                moved |= (root.localPosition - cell).sqrMagnitude > 1e-6f;
            }

            Assert.That(moved, Is.True, "the root moves while it punches");
            Assert.That(Tween.GetTweensCount(root), Is.Zero, "the punch ends by itself");
            Assert.That((root.localPosition - cell).sqrMagnitude, Is.LessThan(1e-8f), "it ends on its cell");
            Assert.That(simulation.Board.GetOrigin(1), Is.EqualTo(new ColorBlockJam.Domain.Board.Cell(3, 3)), "no domain change");
        }

        [UnityTest]
        public IEnumerator AFrozenBlockPunchesItsRootOnceThroughItsIceView()
        {
            BoardSimulation simulation = m_scene.Build(BoardPresenterBoosterTests.HammerLevel(ice: true));
            Transform root = m_scene.Presenter.GetPiece(0).transform;
            Assert.That(m_scene.Presenter.GetPiece(0).View.IsIced, Is.True, "the ice is on the block's own tiles (DEC-184)");
            Vector3 cell = root.localPosition;

            m_scene.Presenter.ShowBlockedDrag(0);

            // The Ice view's punch (BlockIceOverlay.Shake) is the root's only tween and moves along (0.045, 0, 0.015).
            // The presenter's own ungated punch would first stop every tween on the root and then move along X only,
            // so one tween plus a Z offset proves the presenter added none.
            Assert.That(Tween.GetTweensCount(root), Is.EqualTo(1), "one punch on the frozen root");
            bool movedAlongZ = false;
            float deadline = Time.realtimeSinceStartup + 1f;
            while (Tween.GetTweensCount(root) > 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                movedAlongZ |= Mathf.Abs((root.localPosition - cell).z) > 1e-4f;
            }

            Assert.That(movedAlongZ, Is.True, "the Ice view punches the frozen root; the presenter adds no punch");
            Assert.That(Tween.GetTweensCount(root), Is.Zero, "the punch ends by itself");
            Assert.That((root.localPosition - cell).sqrMagnitude, Is.LessThan(1e-8f), "it ends on its cell");

            // Teardown's StopTweens interrupts a second punch and puts the root back on its cell at once.
            m_scene.Presenter.ShowBlockedDrag(0);
            yield return null;
            m_scene.Presenter.StopTweens();

            Assert.That(Tween.GetTweensCount(root), Is.Zero);
            Assert.That((root.localPosition - cell).sqrMagnitude, Is.LessThan(1e-10f), "an interrupted punch returns the root");
            Assert.That(simulation.Board.GetOrigin(0), Is.EqualTo(new ColorBlockJam.Domain.Board.Cell(0, 0)), "no domain change");
        }

        [Test]
        public void SuspendPausesAndTeardownStopsTheBlockedPunch()
        {
            m_scene.Build(BoardPresenterBoosterTests.HammerLevel(ice: false));
            Transform root = m_scene.Presenter.GetPiece(1).transform;
            m_scene.Presenter.ShowBlockedDrag(1);

            m_scene.Presenter.SetTweensPaused(true);
            Assert.That(Tween.GetTweensCount(root), Is.EqualTo(1));

            m_scene.Presenter.StopTweens();
            Assert.That(Tween.GetTweensCount(root), Is.Zero);
        }

        [UnityTest]
        public IEnumerator TheScenePresenterPausesAndStopsTheFrostWithItsOtherTweens()
        {
            m_frostMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m_frost = new TimeFreezeFrostView(new TimeFreezeFrostContext(m_scene.Camera, m_frostMaterial));
            m_frostPresenter = new BoardPresenter(m_scene.Environment, m_scene.Art, m_scene.Effects,
                new BlockEffectViewContext(m_scene.Art, m_scene.Environment.BoardRoot, m_scene.Camera, m_scene.Document),
                m_scene.Catalog, m_frost);
            Assert.That(m_frost.Prepare(), Is.True);
            m_frost.SetFrozen(true);

            m_frostPresenter.SetTweensPaused(true);
            Assert.That(m_frost.IsPaused, Is.True, "Suspend holds the fade");
            m_frostPresenter.SetTweensPaused(false);
            Assert.That(m_frost.IsPaused, Is.False, "Resume continues it");
            yield return null;

            m_frostPresenter.StopTweens();

            Assert.That(m_frost.Amount, Is.Zero, "Teardown clears the frost at once");
            Assert.That(m_frost.IsVisible, Is.False);
            Assert.That(Tween.GetTweensCount(m_frost), Is.Zero);
        }
    }
}
#endif
