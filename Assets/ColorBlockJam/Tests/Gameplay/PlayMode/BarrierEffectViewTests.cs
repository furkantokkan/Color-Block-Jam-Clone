#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures;
using NUnit.Framework;
using PrimeTween;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ColorBlockJam.Tests.Gameplay.PlayMode
{
    /// <summary>
    /// Proves the Barrier module and view (contract TASK-20260925-103527585-3d1b0a34, acceptance 3, 5 and 6) with the
    /// real environment prefab and board piece catalog, composed as the Game scene composes them: the
    /// play area is built from the level first, then a BlockEffectContextSource over BarrierEffectModule prepares the
    /// level, the presenter builds, EffectAttemptHookSource creates each attempt and Place binds it. Binding snaps every
    /// barrier to its start state without a tween; each flip makes exactly one animated SetBarrierOpen in the presenter's
    /// Sync, a Vacuum of two blocks makes two and ends where it started; the presenter's Suspend pauses and its Teardown
    /// stops the barrier tweens; Restart snaps back to startsOpen; a play area built without the level's barriers
    /// rejects the level; and the flip dispatch plus Sync allocate nothing.
    /// </summary>
    public sealed class BarrierEffectViewTests
    {
        private const float k_closedHeight = 0f;
        private const float k_openHeight = -0.8f;
        // The environment's 0.25 s flip plus a frame of margin.
        private const float k_flipSeconds = 0.3f;
        private const int k_hammerTarget = 1;

        private static readonly Vector2Int[] s_wallCells = { new Vector2Int(2, 0), new Vector2Int(2, 1) };
        private static readonly Vector2Int[] s_pitCells = { new Vector2Int(0, 3) };
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };

        private PresenterScene m_scene;
        private LevelData m_data;
        private BoardLayout m_layout;
        private AttemptLevel m_level;
        private BlockEffectContextSource m_source;
        private EffectAttemptHookSource m_hooks;
        private BoardPresenter m_presenter;
        private BoardSimulation m_simulation;

        [SetUp]
        public void SetUp()
        {
            m_scene = new PresenterScene();
            m_data = CreateLevel();
            Assert.That(m_data.Validate(out string error), Is.True, error);
            Assert.That(BoardLayout.TryCreate(m_data, out m_layout, out error), Is.True, error);
            Assert.That(AttemptLevelConverter.TryConvert(m_data, m_layout, out m_level, out error), Is.True, error);
            m_source = new BlockEffectContextSource(new IBlockEffectModule[] { new BarrierEffectModule(m_scene.Environment) });
            m_hooks = new EffectAttemptHookSource(m_source);
            BoardArt art = PresenterScene.CreateArt();
            m_presenter = new BoardPresenter(m_scene.Environment, art, m_source,
                new BlockEffectViewContext(art, m_scene.Environment.BoardRoot, null, null));

            // GameAttemptLoader.TryBuild on Home: the play area from the level, the effect metadata, then the attempt.
            m_scene.Environment.Build(m_layout, m_data);
            m_source.Prepare(m_level, m_data);
            m_simulation = StartAttempt();
        }

        [TearDown]
        public void TearDown()
        {
            m_presenter.Dispose();
            m_scene.Dispose();
        }

        private BarrierEffectView View => (BarrierEffectView)m_presenter.GetEffectView("barrier");

        [Test]
        public void TheModuleRegistersAFreshRuleAndBindingSnapsEveryBarrierWithoutATween()
        {
            BarrierEffectState state = (BarrierEffectState)m_source.CurrentAttempt.GetAt(0);
            Assert.That(state.Rule.BarrierCount, Is.EqualTo(2));
            Assert.That(m_simulation.Hooks.AllowsCell(m_simulation.Board, 0, new Cell(2, 0)), Is.False,
                "The closed wall is a registered cell gate.");
            Assert.That(MoveRules.CanOccupy(m_simulation.Board, m_simulation.Hooks, 0, new Cell(2, 0)), Is.False);

            AssertBarrier(s_wallCells, k_closedHeight, true);
            AssertBarrier(s_pitCells, k_openHeight, false);
            AssertTweenCount(FindPieces(s_wallCells), 0);
            Assert.That(View.AnimatedFlipCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator EveryFlipMakesOneAnimatedCallInTheViewSyncPhase()
        {
            Execute(GameCommand.RemoveBlock(k_hammerTarget, m_simulation.Board.GetOrigin(k_hammerTarget)));

            Assert.That(View.AnimatedFlipCount, Is.EqualTo(2), "one call for each of the two barriers");
            AssertTweenCount(FindPieces(s_wallCells), 1);
            AssertTweenCount(FindPieces(s_pitCells), 1);
            yield return new WaitForSeconds(k_flipSeconds);
            yield return null;

            AssertBarrier(s_wallCells, k_openHeight, false);
            AssertBarrier(s_pitCells, k_closedHeight, true);
            // The next tick clears its event log before its command phase.
            m_simulation.Events.Clear();
            m_presenter.Sync(m_simulation, default);
            Assert.That(View.AnimatedFlipCount, Is.EqualTo(2), "A sync without a flip calls nothing.");
        }

        [Test]
        public void AVacuumOfTwoBlocksMakesTwoCallsAndEndsWhereItStarted()
        {
            BlockColor blue = m_simulation.Board.GetFootprint(k_hammerTarget).Color;
            Execute(GameCommand.RemoveColor(k_hammerTarget, m_simulation.Board.GetOrigin(k_hammerTarget), blue));

            Assert.That(m_simulation.Events.Count, Is.EqualTo(2), "two blue blocks removed");
            Assert.That(View.AnimatedFlipCount, Is.EqualTo(4), "one call per flip of each of the two barriers (DEC-MECH-3)");
            AssertBarrier(s_wallCells, k_closedHeight, true);
            AssertBarrier(s_pitCells, k_openHeight, false);
            AssertTweenCount(FindPieces(s_wallCells), 0, "The second call started from where the first left the pieces.");
        }

        [UnityTest]
        public IEnumerator ThePresentersSuspendPausesAndItsTeardownStopsTheBarrierTweens()
        {
            Execute(GameCommand.RemoveBlock(k_hammerTarget, m_simulation.Board.GetOrigin(k_hammerTarget)));
            List<Transform> wall = FindPieces(s_wallCells);
            m_presenter.SetTweensPaused(true);
            float[] paused = ReadHeights(wall);
            yield return new WaitForSeconds(0.4f);

            Assert.That(ReadHeights(wall), Is.EqualTo(paused), "Suspend holds every barrier piece.");
            AssertTweenCount(wall, 1);

            m_presenter.SetTweensPaused(false);
            yield return new WaitForSeconds(0.1f);
            m_presenter.StopTweens();
            AssertTweenCount(wall, 0);
            float[] stopped = ReadHeights(wall);
            yield return null;
            Assert.That(ReadHeights(wall), Is.EqualTo(stopped), "Teardown leaves the pieces where they stopped.");
        }

        [UnityTest]
        public IEnumerator RestartSnapsEveryBarrierBackToItsStartStateWithoutAnimating()
        {
            Execute(GameCommand.RemoveBlock(k_hammerTarget, m_simulation.Board.GetOrigin(k_hammerTarget)));
            yield return new WaitForSeconds(k_flipSeconds);
            yield return null;
            AssertBarrier(s_wallCells, k_openHeight, false);
            BarrierEffectState played = (BarrierEffectState)m_source.CurrentAttempt.GetAt(0);

            // LevelSession's Restart: Teardown, then a new attempt of the same level built and placed on Home.
            m_presenter.StopTweens();
            m_presenter.ReturnPieces();
            m_simulation = StartAttempt();

            BarrierEffectState restarted = (BarrierEffectState)m_source.CurrentAttempt.GetAt(0);
            Assert.That(restarted.Rule, Is.Not.SameAs(played.Rule));
            Assert.That(restarted.Rule.IsOpen(0), Is.False);
            AssertBarrier(s_wallCells, k_closedHeight, true);
            AssertBarrier(s_pitCells, k_openHeight, false);
            AssertTweenCount(FindPieces(s_wallCells), 0, "Restart snaps without a tween.");
            Assert.That(View.AnimatedFlipCount, Is.Zero);
        }

        [Test]
        public void APlayAreaBuiltWithoutTheLevelsBarriersRejectsTheLevel()
        {
            m_presenter.StopTweens();
            m_presenter.ReturnPieces();
            m_scene.Environment.Build(m_layout);
            // A newly converted level, as the loader builds one: the presenter reuses a level it already built.
            Assert.That(AttemptLevelConverter.TryConvert(m_data, m_layout, out AttemptLevel other, out string error),
                Is.True, error);
            m_source.Prepare(other, m_data);

            Assert.That(m_presenter.TryBuild(other, out error), Is.False);
            Assert.That(error, Does.Contain("barriers"));
        }

        [Test]
        public void TheFlipDispatchAndTheViewSyncAllocateNothing()
        {
            // The first measurement in a domain also measures the recorder's own setup.
            Assert.That(() => { }, Is.Not.AllocatingGCMemory());
            FlipAndSync();

            Assert.That(() => FlipAndSync(), Is.Not.AllocatingGCMemory());
            Assert.That(View.AnimatedFlipCount, Is.EqualTo(8), "two runs of two flips, one call per flip of each barrier");
        }

        // One clear raised through the attempt's hooks, as a command handler raises it, then the view-sync phase and
        // a Suspend/Resume/Teardown round of the barrier tweens.
        private void FlipAndSync()
        {
            m_simulation.Hooks.Publish(new BlockRemovedEvent(k_hammerTarget));
            View.Sync(m_simulation.Preview);
            View.SetPaused(true);
            View.SetPaused(false);
            m_simulation.Hooks.Publish(new BlockExitedEvent(0, 0, Side.Top, Cell.Zero, ExitOutcome.Leave));
            View.Sync(m_simulation.Preview);
            View.StopTweens();
        }

        // One attempt as LevelSession starts it: fresh hooks published for the level, its simulation, then Place.
        private BoardSimulation StartAttempt()
        {
            BoardSimulation simulation = new BoardSimulation(m_level.Model, m_hooks.Create(m_level), DragSettings.Default);
            Assert.That(m_presenter.TryBuild(m_level, out string error), Is.True, error);
            m_presenter.Place(simulation.Preview);
            return simulation;
        }

        // One tick's command phase and view-sync phase, as the session runs them.
        private void Execute(GameCommand command)
        {
            m_simulation.Events.Clear();
            Assert.That(m_simulation.Commands.Execute(command), Is.EqualTo(CommandOutcome.Applied), command.ToString());
            m_presenter.Sync(m_simulation, default);
        }

        // A 5 x 4 board: block 0 a red single at (0, 0), blocks 1 and 2 blue singles at (4, 3) and (4, 2), block 3 an
        // orange single at (4, 0). The closed "wall" barrier covers (2, 0) and (2, 1); the open "pit" covers (0, 3).
        private static LevelData CreateLevel()
        {
            return new LevelData("barrier-view", "Barrier view", 5, 4, 60, Array.Empty<Vector2Int>(),
                Array.Empty<LevelDoorData>(),
                new[]
                {
                    new LevelBlockData("red", "Red", Vector2Int.zero, s_single),
                    new LevelBlockData("blue-top", "Blue", new Vector2Int(4, 3), s_single),
                    new LevelBlockData("blue-low", "Blue", new Vector2Int(4, 2), s_single),
                    new LevelBlockData("orange", "Orange", new Vector2Int(4, 0), s_single)
                },
                new[] { Barrier("wall", false, s_wallCells), Barrier("pit", true, s_pitCells) });
        }

        private static LevelCellObjectData Barrier(string id, bool startsOpen, Vector2Int[] cells)
        {
            return new LevelCellObjectData(id, cells, new LevelEffectData("barrier",
                new[] { new LevelEffectParamData("startsOpen", startsOpen ? "true" : "false") }));
        }

        // Barrier pieces are the play-area pieces on the barrier's floor cells other than the floor tile and the floor
        // base under them; play-area pieces are direct BoardRoot children, so block tiles never count.
        private List<Transform> FindPieces(Vector2Int[] cells)
        {
            Transform boardRoot = m_scene.Environment.BoardRoot;
            List<Transform> pieces = new List<Transform>();
            foreach (BoardPieceView piece in boardRoot.GetComponentsInChildren<BoardPieceView>(false))
            {
                Transform transform = piece.transform;
                if (transform.parent != boardRoot || piece.name.StartsWith("Grid", StringComparison.Ordinal)
                    || piece.name.StartsWith("FloorBase", StringComparison.Ordinal))
                {
                    continue;
                }

                Vector3 position = transform.localPosition;
                if (Array.IndexOf(cells, new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z))) >= 0)
                {
                    pieces.Add(transform);
                }
            }

            return pieces;
        }

        private void AssertBarrier(Vector2Int[] cells, float height, bool rendered)
        {
            List<Transform> pieces = FindPieces(cells);
            Assert.That(pieces, Is.Not.Empty, "The play area shows the barrier's pieces.");
            foreach (Transform piece in pieces)
            {
                Assert.That(piece.localPosition.y, Is.EqualTo(height).Within(1e-4f), piece.name);
                Assert.That(piece.GetComponent<BoardPieceView>().Renderer.enabled, Is.EqualTo(rendered), piece.name);
            }
        }

        private static void AssertTweenCount(List<Transform> pieces, int count, string message = null)
        {
            Assert.That(pieces, Is.Not.Empty);
            foreach (Transform piece in pieces)
            {
                Assert.That(Tween.GetTweensCount(piece), Is.EqualTo(count), message ?? piece.name);
            }
        }

        private static float[] ReadHeights(List<Transform> pieces)
        {
            float[] heights = new float[pieces.Count];
            for (int index = 0; index < heights.Length; index++)
            {
                heights[index] = pieces[index].localPosition.y;
            }

            return heights;
        }
    }
}
#endif
