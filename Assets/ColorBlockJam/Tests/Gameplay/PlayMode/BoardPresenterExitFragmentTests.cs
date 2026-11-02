#if UNITY_EDITOR
using System.Collections;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ColorBlockJam.Tests.Gameplay.PlayMode
{
    /// <summary>
    /// Proves the presenter's gate-exit fragments (DEC-131, AC-3.18 fragment part) with the real
    /// environment and shape prefabs: a valid exit plays one burst on the exit clip plane's door line in
    /// its block's colour, a move or a Stay outcome plays none, Suspend holds the burst, Teardown stops
    /// and returns it, Restart keeps the warmed emitters, and the exit sync allocates nothing.
    /// </summary>
    public sealed class BoardPresenterExitFragmentTests
    {
        private static readonly int s_colorId = Shader.PropertyToID("_PaletteUV");
        private static readonly int s_clipPlaneId = Shader.PropertyToID("_ClipPlane");

        // The exit pointer of BoardPresenterLifecycleTests: 0.4 cell above the exiting block (DEC-164).
        private static readonly BoardPoint s_pushOut =
            new BoardPoint(PresenterScene.ExitBlockOrigin.X, PresenterScene.ExitBlockOrigin.Y + 0.4f);

        private PresenterScene m_scene;
        private AttemptLevel m_level;
        private BoardSimulation m_simulation;

        // The first allocation assertion in a domain also measures the GC.Alloc recorder's own setup
        // (ZeroAllocationTests), so one empty measurement runs first.
        [OneTimeSetUp]
        public void WarmUpTheAllocationRecorder()
        {
            Assert.That(() => { }, Is.Not.AllocatingGCMemory());
        }

        [SetUp]
        public void SetUp()
        {
            m_scene = new PresenterScene();
            m_level = PresenterScene.CreateLevel(withSecondBlock: true);
            m_simulation = new BoardSimulation(m_level.Model, AttemptHooks.CreateEmpty(), DragSettings.Default);
            Assert.That(m_scene.Presenter.TryBuild(m_level, out string error), Is.True, error);
            m_scene.Presenter.Place(m_simulation.Preview);
        }

        [TearDown]
        public void TearDown()
        {
            m_scene.Dispose();
        }

        [Test]
        public void TryBuildWarmsHiddenEmittersOnHome()
        {
            BoardPresenter presenter = m_scene.Presenter;

            Assert.That(presenter.PreparedExitFragmentCount, Is.EqualTo(m_level.BlockCount), "two blocks stay under the cap");
            Assert.That(presenter.ActiveExitFragmentCount, Is.Zero);
            Assert.That(FindActiveBursts(), Is.Empty);
        }

        [Test]
        public void AValidExitPlaysOneBurstOnTheDoorLineInItsBlocksColour()
        {
            BoardPresenter presenter = m_scene.Presenter;
            MeshRenderer tile = presenter.GetPiece(0).GetComponentInChildren<MeshRenderer>(false);
            ExitBlock(m_simulation);

            presenter.Sync(m_simulation, s_pushOut);

            Assert.That(presenter.ActiveExitFragmentCount, Is.EqualTo(1));
            ParticleSystem[] bursts = FindActiveBursts();
            Assert.That(bursts.Length, Is.EqualTo(1));
            ParticleSystem burst = bursts[0];
            Assert.That(burst.isPlaying, Is.True);

            // Column 1 on the top door line of the 3 x 3 board, emitting out of the top edge.
            Vector3 local = burst.transform.localPosition;
            Assert.That(local.x, Is.EqualTo(PresenterScene.ExitBlockOrigin.X).Within(1e-4f));
            Assert.That(local.z, Is.EqualTo(2.5f + ExitMotionSettings.k_clipOffsetCells).Within(1e-4f));
            Assert.That(Vector3.Distance(burst.transform.localRotation * Vector3.forward, Vector3.forward), Is.LessThan(1e-4f));

            // It starts on the clip plane that hides the block, in the block's own colour.
            MaterialPropertyBlock tileProperties = new MaterialPropertyBlock();
            tile.GetPropertyBlock(tileProperties);
            Vector4 plane = tileProperties.GetVector(s_clipPlaneId);
            Vector3 world = burst.transform.position;
            Assert.That(Vector4.Dot(plane, new Vector4(world.x, world.y, world.z, 1f)), Is.EqualTo(0f).Within(1e-3f));

            MaterialPropertyBlock burstProperties = new MaterialPropertyBlock();
            burst.GetComponent<ParticleSystemRenderer>().GetPropertyBlock(burstProperties);
            Assert.That(burstProperties.GetVector(s_colorId), Is.EqualTo(tileProperties.GetVector(s_colorId)));
        }

        [Test]
        public void AMoveOrAStayExitPlaysNoBurst()
        {
            BoardPresenter presenter = m_scene.Presenter;
            Assert.That(m_simulation.Commands.Execute(GameCommand.Move(1, new Cell(0, 0), new Cell(1, 0))),
                Is.EqualTo(CommandOutcome.Applied));

            presenter.Sync(m_simulation, default);

            Assert.That(presenter.ActiveExitFragmentCount, Is.Zero, "a move settles without fragments");

            // A Stay outcome keeps the block on the board, so nothing breaks off.
            m_simulation.Events.Clear();
            m_simulation.Events.Add(BoardEvent.Exited(
                new BlockExitedEvent(0, 0, Side.Top, PresenterScene.ExitBlockOrigin, ExitOutcome.Stay)));
            presenter.Sync(m_simulation, default);

            Assert.That(presenter.ActiveExitFragmentCount, Is.Zero, "a Stay outcome emits nothing");
            Assert.That(FindActiveBursts(), Is.Empty);
        }

        [Test]
        public void ARefusedExitCommandPlaysNoBurst()
        {
            BoardPresenter presenter = m_scene.Presenter;

            // The blue pair has no door below it, so the domain refuses the exit and changes nothing.
            Assert.That(m_simulation.Commands.Execute(GameCommand.MoveAndExit(1, new Cell(0, 0), new Cell(0, 0), Side.Bottom)),
                Is.Not.EqualTo(CommandOutcome.Applied));
            presenter.Sync(m_simulation, default);

            Assert.That(presenter.ActiveExitFragmentCount, Is.Zero);
            Assert.That(FindActiveBursts(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator SuspendHoldsTheBurstAndTeardownStopsAndReturnsIt()
        {
            BoardPresenter presenter = m_scene.Presenter;
            ExitBlock(m_simulation);
            presenter.Sync(m_simulation, s_pushOut);
            ParticleSystem burst = FindActiveBursts()[0];
            yield return null;

            presenter.SetTweensPaused(true);
            float held = burst.time;
            yield return new WaitForSeconds(0.1f);

            Assert.That(burst.isPaused, Is.True, "Suspend step 3 holds the burst");
            Assert.That(burst.time, Is.EqualTo(held));

            presenter.SetTweensPaused(false);
            Assert.That(burst.isPaused, Is.False);

            // Teardown steps 3 and 6.
            presenter.StopTweens();
            Assert.That(burst.particleCount, Is.Zero);
            presenter.ReturnPieces();

            Assert.That(presenter.ActiveExitFragmentCount, Is.Zero);
            Assert.That(burst.gameObject.activeSelf, Is.False, "the emitter is back in its pool");
        }

        [UnityTest]
        public IEnumerator TheBurstReturnsInSyncOnceItsLastFragmentDies()
        {
            BoardPresenter presenter = m_scene.Presenter;
            ExitBlock(m_simulation);
            presenter.Sync(m_simulation, s_pushOut);
            m_simulation.Events.Clear();

            float deadline = Time.realtimeSinceStartup + ExitMotionSettings.k_maxSeconds
                + ExitFragmentSettings.k_lifetimeSeconds + 1f;
            while (presenter.ActiveExitFragmentCount > 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                presenter.Sync(m_simulation, s_pushOut);
            }

            Assert.That(presenter.ActiveExitFragmentCount, Is.Zero);
            Assert.That(presenter.GetPiece(0), Is.Null, "the exited root returned as well");
        }

        [Test]
        public void RestartKeepsTheWarmedEmittersAndCreatesNothing()
        {
            BoardPresenter presenter = m_scene.Presenter;
            Transform blocksRoot = presenter.GetPiece(0).transform.parent;
            ExitBlock(m_simulation);
            presenter.Sync(m_simulation, s_pushOut);
            int prepared = presenter.PreparedExitFragmentCount;
            int objects = blocksRoot.GetComponentsInChildren<Transform>(true).Length;

            // Teardown, then a new attempt of the same immutable level.
            presenter.StopTweens();
            presenter.ReturnPieces();
            BoardSimulation restarted = new BoardSimulation(m_level.Model, AttemptHooks.CreateEmpty(), DragSettings.Default);
            Assert.That(presenter.TryBuild(m_level, out string error), Is.True, error);
            presenter.Place(restarted.Preview);

            Assert.That(presenter.PreparedExitFragmentCount, Is.EqualTo(prepared));
            Assert.That(presenter.ActiveExitFragmentCount, Is.Zero);
            Assert.That(blocksRoot.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
        }

        [Test]
        public void AnExitSyncAfterRestartAllocatesNothing()
        {
            // The first attempt warms every path (JIT, tween and particle state); Restart then replays it.
            BoardPresenter presenter = m_scene.Presenter;
            ExitBlock(m_simulation);
            presenter.Sync(m_simulation, s_pushOut);
            presenter.StopTweens();
            presenter.ReturnPieces();
            BoardSimulation restarted = new BoardSimulation(m_level.Model, AttemptHooks.CreateEmpty(), DragSettings.Default);
            Assert.That(presenter.TryBuild(m_level, out string error), Is.True, error);
            presenter.Place(restarted.Preview);
            ExitBlock(restarted);

            Assert.That(() => presenter.Sync(restarted, s_pushOut), Is.Not.AllocatingGCMemory());
            Assert.That(presenter.ActiveExitFragmentCount, Is.EqualTo(1));
        }

        // Drags block 0 upward past the exit threshold and runs the exit command, as the pointer router does.
        private static void ExitBlock(BoardSimulation simulation)
        {
            BoardPoint press = BoardPoint.FromCell(PresenterScene.ExitBlockOrigin);
            Assert.That(simulation.Drag.TryBegin(0, press), Is.True);
            Assert.That(simulation.Drag.Update(s_pushOut, out GameCommand exit), Is.EqualTo(DragResult.ExitReady));
            Assert.That(simulation.Commands.Execute(exit), Is.EqualTo(CommandOutcome.Applied));
        }

        // Pooled emitters are inactive, so only bursts in flight are found.
        private ParticleSystem[] FindActiveBursts()
        {
            return m_scene.Environment.BoardRoot.GetComponentsInChildren<ParticleSystem>(false);
        }
    }
}
#endif
