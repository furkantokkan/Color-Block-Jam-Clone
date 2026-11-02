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
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures;
using NUnit.Framework;
using PrimeTween;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.Gameplay.PlayMode
{
    /// <summary>
    /// Proves the board presenter's pooling, placement, exit motion and tween cleanup with the real
    /// environment prefab and block root (AC-3.16, AC-3.18 motion part, NFR-L7, V-M1, V-M20 partial).
    /// </summary>
    public sealed class BoardPresenterLifecycleTests
    {
        private static readonly int s_clipEnabledId = Shader.PropertyToID("_ClipEnabled");

        // The exit pointer: 0.4 cell above the exiting block's centre, into the door it touches (DEC-164).
        private static readonly BoardPoint s_pushOut = new BoardPoint(PresenterScene.ExitBlockOrigin.X, PresenterScene.ExitBlockOrigin.Y + 0.4f);

        private PresenterScene m_scene;
        private AttemptLevel m_level;
        private BoardSimulation m_simulation;

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
        public void PlacePutsEveryRootOnItsCellUnderBoardRoot()
        {
            BoardPresenter presenter = m_scene.Presenter;

            Assert.That(presenter.ActivePieceCount, Is.EqualTo(2));
            for (int block = 0; block < m_level.BlockCount; block++)
            {
                Transform root = presenter.GetPiece(block).transform;
                Cell origin = m_simulation.Board.GetOrigin(block);
                Assert.That(root.parent.parent, Is.SameAs(m_scene.Environment.BoardRoot), "Block roots live under BoardRoot's Blocks node.");
                Assert.That(root.localPosition, Is.EqualTo(m_level.Layout.CellToLocal(new Vector2Int(origin.X, origin.Y))));
            }
        }

        [UnityTest]
        public IEnumerator AnExitPlaysAClippedTweenOnTheRootAndReturnsTheRootWhenItEnds()
        {
            BoardPresenter presenter = m_scene.Presenter;
            BlockPiece piece = presenter.GetPiece(0);
            Transform root = piece.transform;
            MeshRenderer[] tiles = piece.GetComponentsInChildren<MeshRenderer>(false);
            Assert.That(tiles, Is.Not.Empty, "The placed block shows its tiles.");
            ExitBlock();

            presenter.Sync(m_simulation, s_pushOut);

            Assert.That(Tween.GetTweensCount(root), Is.EqualTo(1), "The exit tween targets the root Transform.");
            Assert.That(IsClipped(tiles), Is.True, "Every tile clips at the door line.");
            Assert.That(presenter.FinishSeconds, Is.GreaterThan(0f).And.LessThanOrEqualTo(ExitMotionSettings.k_maxSeconds));

            yield return WaitForTweens(root);

            // Travel is depth + 1 cells: from (1, 2) through the top door to local z 4.
            Assert.That(root.localPosition.z, Is.EqualTo(PresenterScene.ExitBlockOrigin.Y + 2f).Within(1e-4f));
            m_simulation.Events.Clear();
            presenter.Sync(m_simulation, s_pushOut);

            Assert.That(presenter.GetPiece(0), Is.Null);
            Assert.That(presenter.ActivePieceCount, Is.EqualTo(1));
            Assert.That(root.gameObject.activeSelf, Is.False, "The pool deactivates a returned root.");
            // Release makes no GPU write (DEC-123); the next checkout's colour write clears the clip plane,
            // which BoardKitLifecycleTests.PooledTileTakesItsOwnersColourAndReleaseMakesNoPropertyBlockWrite covers.
        }

        [Test]
        public void StopTweensAndReturnPiecesLeaveNoTweenAndNoCheckedOutRoot()
        {
            BoardPresenter presenter = m_scene.Presenter;
            Transform exiting = presenter.GetPiece(0).transform;
            Transform settling = presenter.GetPiece(1).transform;
            ExitBlock();
            presenter.Sync(m_simulation, s_pushOut);
            presenter.SetHeld(1);
            settling.localPosition += new Vector3(0.2f, 0f, 0f);
            presenter.SetHeld(-1);
            Assert.That(Tween.GetTweensCount(exiting), Is.EqualTo(1));
            Assert.That(Tween.GetTweensCount(settling), Is.EqualTo(1), "Releasing a block off its cell settles it with a tween.");

            presenter.StopTweens();
            presenter.ReturnPieces();

            Assert.That(Tween.GetTweensCount(exiting), Is.Zero);
            Assert.That(Tween.GetTweensCount(settling), Is.Zero);
            Assert.That(presenter.ActivePieceCount, Is.Zero);
            Assert.That(presenter.FinishSeconds, Is.Zero);
        }

        [Test]
        public void BuildingTheSameLevelAgainInstantiatesNothing()
        {
            BoardPresenter presenter = m_scene.Presenter;
            Transform blocksRoot = presenter.GetPiece(0).transform.parent;
            int prepared = presenter.PreparedPieceCount;
            int children = blocksRoot.childCount;
            presenter.ReturnPieces();

            // Restart: a new simulation of the same immutable level.
            BoardSimulation restarted = new BoardSimulation(m_level.Model, AttemptHooks.CreateEmpty(), DragSettings.Default);
            Assert.That(presenter.TryBuild(m_level, out string error), Is.True, error);
            presenter.Place(restarted.Preview);

            Assert.That(presenter.PreparedPieceCount, Is.EqualTo(prepared));
            Assert.That(blocksRoot.childCount, Is.EqualTo(children));
            Assert.That(presenter.ActivePieceCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ReleasingARootOnItsCellStartsNoTweenAndLogsNothing()
        {
            // A tap or a push into a wall: Sync leaves the held root exactly on its cell (zero lead).
            BoardPresenter presenter = m_scene.Presenter;
            Transform root = presenter.GetPiece(1).transform;
            Vector3 cell = root.localPosition;

            presenter.SetHeld(1);
            presenter.SetHeld(-1);

            Assert.That(Tween.GetTweensCount(root), Is.Zero, "No settle tween to the root's own position.");
            Assert.That(root.localPosition, Is.EqualTo(cell));

            // PrimeTween reports an equal end value on the tween's first update, so let frames run.
            yield return null;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PausedTweensHoldTheRootUntilResumed()
        {
            BoardPresenter presenter = m_scene.Presenter;
            Transform root = presenter.GetPiece(0).transform;
            ExitBlock();
            presenter.Sync(m_simulation, s_pushOut);

            presenter.SetTweensPaused(true);
            Vector3 held = root.localPosition;
            yield return new WaitForSeconds(0.1f);

            Assert.That(root.localPosition, Is.EqualTo(held));

            presenter.SetTweensPaused(false);
            yield return new WaitForSeconds(0.1f);

            Assert.That(root.localPosition.z, Is.GreaterThan(held.z));
        }

        [Test]
        public void AnInterruptedDragStaysWhereThePlayerLeftItUntilItsMoveRuns()
        {
            BoardPresenter presenter = m_scene.Presenter;
            Transform root = presenter.GetPiece(1).transform;
            Assert.That(m_simulation.Drag.TryBegin(1, new BoardPoint(0f, 0f)), Is.True);
            presenter.SetHeld(1);
            m_simulation.Drag.Update(new BoardPoint(1f, 0f), out GameCommand _);
            presenter.Sync(m_simulation, new BoardPoint(1f, 0f));
            Assert.That(root.localPosition, Is.EqualTo(CellPosition(new Cell(1, 0))), "the held root follows its preview cell");

            // Suspend: the drag parks its move and the view snaps to the parked cell, not back to the start.
            m_simulation.Drag.Interrupt();
            presenter.SnapHeld();

            Assert.That(root.localPosition, Is.EqualTo(CellPosition(new Cell(1, 0))));
            Assert.That(m_simulation.Board.GetOrigin(1), Is.EqualTo(new Cell(0, 0)), "the parked move has not run");

            // Resume: the parked move runs first and the root is already on its cell.
            Assert.That(m_simulation.Drag.TryTakePending(out GameCommand parked), Is.True);
            m_simulation.Events.Clear();
            Assert.That(m_simulation.Commands.Execute(parked), Is.EqualTo(CommandOutcome.Applied));
            presenter.Sync(m_simulation, new BoardPoint(1f, 0f));

            Assert.That(root.localPosition, Is.EqualTo(CellPosition(new Cell(1, 0))));
            Assert.That(Tween.GetTweensCount(root), Is.Zero);
        }

        [UnityTest]
        public IEnumerator AMoveThePointerDidNotDragSettlesOntoItsNewCell()
        {
            BoardPresenter presenter = m_scene.Presenter;
            Transform root = presenter.GetPiece(1).transform;

            Assert.That(m_simulation.Commands.Execute(GameCommand.Move(1, new Cell(0, 0), new Cell(1, 0))), Is.EqualTo(CommandOutcome.Applied));
            presenter.Sync(m_simulation, default);

            Assert.That(Tween.GetTweensCount(root), Is.EqualTo(1), "a replay move settles with a tween");
            yield return WaitForTweens(root);
            Assert.That(root.localPosition, Is.EqualTo(CellPosition(new Cell(1, 0))));
        }

        [UnityTest]
        public IEnumerator ASourceMoveRightAfterAReleaseSettlesOnTheFinalCell()
        {
            BoardPresenter presenter = m_scene.Presenter;
            Transform root = presenter.GetPiece(1).transform;
            Assert.That(m_simulation.Drag.TryBegin(1, new BoardPoint(0f, 0f)), Is.True);
            presenter.SetHeld(1);
            m_simulation.Drag.Update(new BoardPoint(1f, 0f), out GameCommand _);
            presenter.Sync(m_simulation, new BoardPoint(1f, 0f));
            m_simulation.Events.Clear();

            // One command phase: the pointer releases the block, then a command source moves it on.
            Assert.That(m_simulation.Drag.TryEnd(out GameCommand release), Is.True);
            Assert.That(m_simulation.Commands.Execute(release), Is.EqualTo(CommandOutcome.Applied));
            presenter.SetHeld(-1);
            Assert.That(m_simulation.Commands.Execute(GameCommand.Move(1, new Cell(1, 0), new Cell(0, 1))), Is.EqualTo(CommandOutcome.Applied));
            presenter.Sync(m_simulation, new BoardPoint(1f, 0f));

            yield return WaitForTweens(root);
            Assert.That(root.localPosition, Is.EqualTo(CellPosition(new Cell(0, 1))));
        }

        [UnityTest]
        public IEnumerator AnExitThePointerDidNotDragLeavesFromItsExitCell()
        {
            BoardPresenter presenter = m_scene.Presenter;
            Transform root = presenter.GetPiece(0).transform;
            Assert.That(m_simulation.Commands.Execute(GameCommand.Move(0, new Cell(1, 2), new Cell(2, 2))), Is.EqualTo(CommandOutcome.Applied));
            presenter.Sync(m_simulation, default);
            yield return WaitForTweens(root);
            m_simulation.Events.Clear();

            // One replayed command moves the block back under the door and exits: the motion starts there.
            GameCommand exit = GameCommand.MoveAndExit(0, new Cell(2, 2), new Cell(1, 2), Side.Top);
            Assert.That(m_simulation.Commands.Execute(exit), Is.EqualTo(CommandOutcome.Applied));
            presenter.Sync(m_simulation, default);

            Assert.That(root.localPosition, Is.EqualTo(CellPosition(new Cell(1, 2))));
            yield return WaitForTweens(root);
            Assert.That(root.localPosition.x, Is.EqualTo(CellPosition(new Cell(1, 2)).x).Within(1e-4f), "it left straight through the door column");
        }

        [UnityTest]
        public IEnumerator AnExitWhileStillSettlingLeavesFromItsExitCell()
        {
            BoardPresenter presenter = m_scene.Presenter;
            Transform root = presenter.GetPiece(0).transform;
            Assert.That(m_simulation.Commands.Execute(GameCommand.Move(0, new Cell(1, 2), new Cell(2, 2))), Is.EqualTo(CommandOutcome.Applied));
            presenter.Sync(m_simulation, default);
            yield return WaitForTweens(root);

            // A replay moves the block back under the door, then exits it before the settle ends.
            m_simulation.Events.Clear();
            Assert.That(m_simulation.Commands.Execute(GameCommand.Move(0, new Cell(2, 2), new Cell(1, 2))), Is.EqualTo(CommandOutcome.Applied));
            presenter.Sync(m_simulation, default);
            yield return new WaitForSeconds(0.04f);
            m_simulation.Events.Clear();
            Assert.That(m_simulation.Commands.Execute(GameCommand.MoveAndExit(0, new Cell(1, 2), new Cell(1, 2), Side.Top)),
                Is.EqualTo(CommandOutcome.Applied));
            presenter.Sync(m_simulation, default);

            Assert.That(root.localPosition, Is.EqualTo(CellPosition(new Cell(1, 2))), "the exit starts on its cell, not mid-settle");
        }

        private Vector3 CellPosition(Cell cell) => m_level.Layout.CellToLocal(new Vector2Int(cell.X, cell.Y));

        // Drags block 0 upward past the exit threshold and runs the exit command, as the pointer router does.
        private void ExitBlock()
        {
            BoardPoint press = BoardPoint.FromCell(PresenterScene.ExitBlockOrigin);
            Assert.That(m_simulation.Drag.TryBegin(0, press), Is.True);
            Assert.That(m_simulation.Drag.Update(s_pushOut, out GameCommand exit), Is.EqualTo(DragResult.ExitReady));
            Assert.That(m_simulation.Commands.Execute(exit), Is.EqualTo(CommandOutcome.Applied));
        }

        private static IEnumerator WaitForTweens(Transform target)
        {
            float deadline = Time.realtimeSinceStartup + ExitMotionSettings.k_maxSeconds + 1f;
            while (Tween.GetTweensCount(target) > 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(Tween.GetTweensCount(target), Is.Zero, "The exit tween ends within its bound.");
        }

        // Reads the clip flag each tile's property block carries; released tiles keep their renderer.
        private static bool IsClipped(MeshRenderer[] renderers)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(properties);
                if (properties.GetFloat(s_clipEnabledId) < 0.5f)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
#endif
