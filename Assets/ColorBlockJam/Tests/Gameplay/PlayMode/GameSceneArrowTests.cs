using System.Collections;
using System.Reflection;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Exit;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Environment;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Composition;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using Onity.DI;
using Onity.Unity.Contexts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.Gameplay
{
    /// <summary>
    /// Plays Arrow blocks (catalog BLK-01, TASK-20260926-230921278-a3ffdc40-arrow-block-gameplay AC-3) with the real
    /// Game scene's environment, board art, effect view context, pointer input, drag router, camera and loader. The
    /// scene is loaded additively and unloaded afterwards; the level JSON, the scene files and the scene's own session
    /// are left alone. The test resolves the effect source and hook source that GameSceneInstaller binds (Ice, then
    /// Arrow), builds its own presenter and session around them and hands them to GameAttemptLoader.Load through a
    /// child container, so the Home build takes the real path: the runtime read accepts the Arrow level, the installed
    /// effect source reads it, and the session's TryBuild(level) shows each Arrow block's centred marker. A simulated mouse
    /// then drags diagonally and pushes: each Arrow block moves only along its axis, leaves through a matching door on
    /// an axis side and never through a matching door on a perpendicular side.
    /// </summary>
    public sealed class GameSceneArrowTests : InputTestFixture
    {
        private const string k_gameScene = "Game";
        private const float k_buildTimeoutSeconds = 20f;
        private const float k_dragStepCells = 0.25f;
        private const float k_frameSeconds = 1f / 60f;
        private const int k_horizontalBlock = 0;
        private const int k_verticalBlock = 1;
        private const int k_plainBlock = 2;

        private Mouse m_mouse;
        private bool m_sceneLoaded;
        private LevelCatalog m_levels;
        private LevelSession m_session;
        private BoardPresenter m_presenter;

        public override void Setup()
        {
            base.Setup();
            m_mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnloadGameScene()
        {
            // The test's own session and presenter end before the scene that lends them its collaborators.
            m_session?.Dispose();
            m_session = null;
            m_presenter?.Dispose();
            m_presenter = null;
            m_levels = null;
            if (m_sceneLoaded)
            {
                m_sceneLoaded = false;
                yield return SceneManager.UnloadSceneAsync(k_gameScene);
            }
        }

        [UnityTest]
        public IEnumerator ArrowBlocksShowTheirMarkersAndMoveAndExitOnlyAlongTheirAxes()
        {
            yield return SceneManager.LoadSceneAsync(k_gameScene, LoadSceneMode.Additive);
            m_sceneLoaded = true;

            SceneContext scope = FindInGameScene<SceneContext>();
            Assert.That(scope, Is.Not.Null, "the Game scene has a SceneContext");
            float deadline = Time.realtimeSinceStartup + k_buildTimeoutSeconds;
            while (!scope.IsReady && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(scope.IsReady, Is.True, "the Game scene container finished its async build");
            LevelSession sceneSession = scope.Container.Resolve<LevelSession>();
            Assert.That(sceneSession.Pause(PauseReason.User), Is.True);
            Assert.That(sceneSession.GoHome(), Is.True, "Home tears the scene's own attempt down");

            // The game's pack read (LevelCatalogLoader.LoadPackAsync uses LevelReadMode.Runtime) accepts the Arrow level.
            m_levels = new LevelCatalog();
            Assert.That(LevelDatabaseJson.TryWrite(new[] { CreateLevel() }, out string json, out string error), Is.True, error);
            Assert.That(m_levels.LoadJson(json, LevelReadMode.Runtime, out error), Is.True, error);

            // The module list GameSceneInstaller binds: blockers first, then Arrow (catalog "Arrow + Ice: Ice first").
            EnvironmentView environment = scope.Container.Resolve<EnvironmentView>();
            BoardArt art = scope.Container.Resolve<BoardArt>();
            BlockEffectContextSource source = scope.Container.Resolve<BlockEffectContextSource>();
            Assert.That(source.Count, Is.EqualTo(2), "the installer registers Ice and Arrow");
            Assert.That(source.GetModule(0).TypeId, Is.EqualTo("ice"));
            Assert.That(source.GetModule(1).TypeId, Is.EqualTo("arrow"), "the installer lists Arrow after Ice");
            m_presenter = new BoardPresenter(environment, art, source, scope.Container.Resolve<BlockEffectViewContext>());
            m_session = new LevelSession(scope.Container.Resolve<DragInputRouter>(), scope.Container.Resolve<IPointerSource>(),
                m_presenter, new RunningCountdown(), scope.Container.Resolve<IPauseOverlay>(),
                scope.Container.Resolve<IAttemptHookSource>(), new PrimeTweenFinishDeadline());

            // GameAttemptLoader.Load on Home, over the scene's collaborators; BindInstance does not own them.
            using (OnityContainer child = new OnityContainer())
            {
                child.BindInstance(environment);
                child.BindInstance(source);
                child.BindInstance(m_session);
                child.BindInstance(m_presenter);
                GameAttemptLoader loader = new GameAttemptLoader(0, FindInGameScene<BoardCamera>());
                // Load takes the selected level and its number once its pack is resident (DEC-196): index 0 is Level 1.
                typeof(GameAttemptLoader).GetMethod("Load", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(loader, new object[] { child, m_levels.GetAt(0), 1 });
            }

            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing), "the loader built and played the level");
            Assert.That(sceneSession.State.Value, Is.EqualTo(SessionState.Home), "the scene's own session stays on Home");

            // The session's TryBuild(level) attached each Arrow block's marker through ArrowEffectView.
            ArrowEffectView arrows = (ArrowEffectView)m_presenter.GetEffectView("arrow");
            Assert.That(arrows, Is.Not.Null, "the presenter created the Arrow view from its module");
            // Arrow_1 for the single, plus Arrow_1 x 2 for the I3: its own, or its two end parts after a middle Rocket.
            Assert.That(arrows.PreparedArrowMarkerCount, Is.EqualTo(3), "Home warms each Arrow block's markers and its parts'");
            AssertMarkerOnRoot(arrows, k_horizontalBlock, Vector3.zero);
            AssertMarkerOnRoot(arrows, k_verticalBlock, new Vector3(1f, 0f, 0f));
            Assert.That(arrows.GetArrowMarker(k_plainBlock), Is.Null, "a block without Arrow has no marker");

            BoardSimulation simulation = m_session.Attempt.Simulation;
            Assert.That(simulation.Hooks.GetAxes(simulation.Board, k_horizontalBlock), Is.EqualTo(AxisMask.Horizontal),
                "the hook source bound the Arrow axis constraint");
            Assert.That(simulation.Hooks.GetAxes(simulation.Board, k_verticalBlock), Is.EqualTo(AxisMask.Vertical));
            Assert.That(simulation.Hooks.GetAxes(simulation.Board, k_plainBlock), Is.EqualTo(AxisMask.Both));

            // Horizontal single at (2, 5): pushed up into its matching Top door, it stays (perpendicular side).
            yield return Drag(environment, new Vector2(2f, 5f), new Vector2(2f, 7f));
            Assert.That(simulation.Board.GetStatus(k_horizontalBlock), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(simulation.Board.GetOrigin(k_horizontalBlock), Is.EqualTo(new Cell(2, 5)));
            Assert.That(ExitRules.Evaluate(simulation.Board, simulation.Hooks, k_horizontalBlock, new Cell(2, 5), Side.Top,
                out ExitCandidate _), Is.EqualTo(ExitVerdict.Vetoed));
            Assert.That(simulation.Commands.Execute(GameCommand.MoveAndExit(k_horizontalBlock, new Cell(2, 5), new Cell(2, 5),
                Side.Top)), Is.EqualTo(CommandOutcome.AxisRefused), "a perpendicular exit command is refused");

            // A diagonal drag changes only its column.
            yield return Drag(environment, new Vector2(2f, 5f), new Vector2(4f, 3f));
            Assert.That(simulation.Board.GetOrigin(k_horizontalBlock), Is.EqualTo(new Cell(4, 5)), "row 5 kept");
            Assert.That(arrows.GetArrowMarker(k_horizontalBlock).transform.parent,
                Is.SameAs(m_presenter.GetPiece(k_horizontalBlock).transform), "the marker rides with its root");

            // Dragged left along its axis, it leaves through its matching Left door.
            yield return Drag(environment, new Vector2(4f, 5f), new Vector2(-1f, 5f));
            Assert.That(simulation.Board.GetStatus(k_horizontalBlock), Is.Not.EqualTo(BlockStatus.OnBoard), "Left exit");

            // Vertical I3 at (0, 1): a diagonal drag changes only its row.
            yield return Drag(environment, new Vector2(1f, 1f), new Vector2(3f, 3f));
            Assert.That(simulation.Board.GetOrigin(k_verticalBlock), Is.EqualTo(new Cell(0, 3)), "column 0 kept");

            // Flush against its matching Left door (row 3) on a perpendicular side and pushed into it, it stays.
            yield return Drag(environment, new Vector2(1f, 3f), new Vector2(-1f, 3f));
            Assert.That(simulation.Board.GetStatus(k_verticalBlock), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(simulation.Board.GetOrigin(k_verticalBlock), Is.EqualTo(new Cell(0, 3)));
            Assert.That(ExitRules.Evaluate(simulation.Board, simulation.Hooks, k_verticalBlock, new Cell(0, 3), Side.Left,
                out ExitCandidate _), Is.EqualTo(ExitVerdict.Vetoed));

            // Dragged down along its axis, it leaves through its matching Bottom door.
            yield return Drag(environment, new Vector2(1f, 3f), new Vector2(1f, -2f));
            Assert.That(simulation.Board.GetStatus(k_verticalBlock), Is.Not.EqualTo(BlockStatus.OnBoard), "Bottom exit");
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing), "the plain block remains");

            // The exit tweens end within ExitMotionSettings.k_maxSeconds; Sync then returns each marker with its root.
            deadline = Time.realtimeSinceStartup + 3f;
            while ((m_presenter.GetPiece(k_horizontalBlock) != null || m_presenter.GetPiece(k_verticalBlock) != null)
                   && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                m_session.RunTick(k_frameSeconds);
            }

            Assert.That(arrows.GetArrowMarker(k_horizontalBlock), Is.Null, "the exit returned the marker");
            Assert.That(arrows.GetArrowMarker(k_verticalBlock), Is.Null, "the exit returned the marker");
            Assert.That(arrows.PreparedArrowMarkerCount, Is.EqualTo(3), "nothing was created during play");
        }

        // A 6 x 6 board:
        // - block 0, a red 1x1 Horizontal Arrow at (2, 5) under a red Top door (column 2) and in line with a red Left
        //   door (row 5);
        // - block 1, a blue horizontal I3 with a Vertical Arrow at (0, 1) over a blue Bottom door (columns 0-2), with a
        //   blue Left door (row 3);
        // - block 2, an orange plain 1x1 at (5, 0), which keeps the level playing.
        private static LevelData CreateLevel()
        {
            return new LevelData("level-arrows", "Arrows", 6, 6, 60,
                new[]
                {
                    new LevelDoorData("d1", BoardSide.Top, 2, 1, "Red"),
                    new LevelDoorData("d2", BoardSide.Left, 5, 1, "Red"),
                    new LevelDoorData("d3", BoardSide.Bottom, 0, 3, "Blue"),
                    new LevelDoorData("d4", BoardSide.Left, 3, 1, "Blue")
                },
                new[]
                {
                    new LevelBlockData("across", "Red", new Vector2Int(2, 5), new[] { Vector2Int.zero },
                        new[] { Arrow("Horizontal") }),
                    new LevelBlockData("upright", "Blue", new Vector2Int(0, 1),
                        new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) },
                        new[] { Arrow("Vertical") }),
                    new LevelBlockData("plain", "Orange", new Vector2Int(5, 0), new[] { Vector2Int.zero })
                });
        }

        private static LevelEffectData Arrow(string axis)
        {
            return new LevelEffectData("arrow", new[] { new LevelEffectParamData("axis", axis) });
        }

        // The marker is active, parented to its block root at the footprint's bounding-box centre.
        private void AssertMarkerOnRoot(ArrowEffectView arrows, int block, Vector3 localCentre)
        {
            BoardPieceView marker = arrows.GetArrowMarker(block);
            Assert.That(marker, Is.Not.Null, "block " + block + " shows its Arrow marker");
            Assert.That(marker.gameObject.activeSelf, Is.True);
            Assert.That(marker.transform.parent, Is.SameAs(m_presenter.GetPiece(block).transform), "on its block root");
            Assert.That(Vector3.Distance(marker.transform.localPosition, localCentre), Is.LessThan(1e-4f), "centred");
        }

        // Presses at the start board point, moves the mouse straight to the end in quarter-cell steps and releases.
        // The test's session is not the scene's tick owner's, so each frame runs one of its ticks after input updates.
        private IEnumerator Drag(EnvironmentView environment, Vector2 start, Vector2 end)
        {
            Move(m_mouse.position, ToScreen(environment, start));
            yield return null;
            m_session.RunTick(k_frameSeconds);
            Press(m_mouse.leftButton);
            yield return null;
            m_session.RunTick(k_frameSeconds);
            yield return null;
            m_session.RunTick(k_frameSeconds);

            float length = Vector2.Distance(start, end);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / k_dragStepCells));
            for (int step = 1; step <= steps; step++)
            {
                Move(m_mouse.position, ToScreen(environment, Vector2.Lerp(start, end, step / (float)steps)));
                yield return null;
                m_session.RunTick(k_frameSeconds);
            }

            Release(m_mouse.leftButton);
            yield return null;
            m_session.RunTick(k_frameSeconds);
            yield return null;
            m_session.RunTick(k_frameSeconds);
        }

        // The same board plane the gameplay projector uses, so the press lands on the block top.
        private static Vector2 ToScreen(EnvironmentView environment, Vector2 boardPoint)
        {
            Vector3 world = environment.BoardRoot.TransformPoint(
                new Vector3(boardPoint.x, ExitMotionSettings.k_blockTopHeight, boardPoint.y));
            Vector3 screen = FindInGameScene<Camera>().WorldToScreenPoint(world);
            return new Vector2(screen.x, screen.y);
        }

        private static T FindInGameScene<T>() where T : Component
        {
            GameObject[] roots = SceneManager.GetSceneByName(k_gameScene).GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T found = roots[i].GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        // A countdown that never runs out, so the attempt stays Playing for the whole test.
        private sealed class RunningCountdown : ICountdown
        {
            public float Remaining { get; private set; }
            public void Start(int seconds) { Remaining = seconds; }
            public void SetPaused(bool paused) { }
            public void Stop() { }
            public bool Tick(float deltaTime) => false;
        }
    }
}
