using System.Collections;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Environment;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Session;
using NUnit.Framework;
using Onity.Unity.Contexts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.Gameplay
{
    /// <summary>
    /// Plays the real Game scene (level 1 from the project's level JSON) with a simulated mouse: each
    /// block is pressed and dragged straight toward the door that fits it until the level is won (AC-9).
    /// The scene is loaded additively and unloaded afterwards, so other PlayMode suites see no leftovers.
    /// </summary>
    public sealed class GameSceneDragTests : InputTestFixture
    {
        private const string k_gameScene = "Game";
        private const float k_buildTimeoutSeconds = 20f;
        private const float k_dragStepCells = 0.25f;

        private Mouse m_mouse;
        private bool m_sceneLoaded;

        public override void Setup()
        {
            base.Setup();
            m_mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnloadGameScene()
        {
            if (m_sceneLoaded)
            {
                m_sceneLoaded = false;

                // The won level's popup prepares the next level in a second, hidden Game scene (DEC-199).
                yield return PreparedLevelScenes.UnloadAllGameScenes();
            }
        }

        [UnityTest]
        public IEnumerator EveryBlockOfLevelOneLeavesThroughItsDoorAndTheLevelIsWon()
        {
            yield return SceneManager.LoadSceneAsync(k_gameScene, LoadSceneMode.Additive);
            m_sceneLoaded = true;

            SceneContext scope = FindGameScope();
            Assert.That(scope, Is.Not.Null, "the Game scene has a SceneContext");

            float deadline = Time.realtimeSinceStartup + k_buildTimeoutSeconds;
            while (!scope.IsReady && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(scope.IsReady, Is.True, "the Game scene container finished its async build");
            LevelSession session = scope.Container.Resolve<LevelSession>();
            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing), "the attempt auto-plays (DEC-116)");

            EnvironmentView environment = scope.Container.Resolve<EnvironmentView>();
            LevelModel model = session.Attempt.Level.Model;
            Assert.That(session.Attempt.Level.LevelId, Is.EqualTo("level-01"));

            // A block that must wait for another is retried on the next pass; level 1 needs one pass.
            for (int pass = 0; pass < model.BlockCount && session.State.Value == SessionState.Playing; pass++)
            {
                for (int block = 0; block < model.BlockCount && session.State.Value == SessionState.Playing; block++)
                {
                    if (session.Attempt.Simulation.Board.GetStatus(block) != BlockStatus.OnBoard)
                    {
                        continue;
                    }

                    yield return DragTowardItsDoor(session, environment, model, block);
                }
            }

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Won), "the last exit wins the level");

            // Won Finish is bounded by 0.8 foreground seconds before Teardown (DEC-93).
            deadline = Time.realtimeSinceStartup + 1.5f;
            while (session.Attempt != null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(session.Attempt, Is.Null, "Teardown followed the bounded Finish");
        }

        private IEnumerator DragTowardItsDoor(LevelSession session, EnvironmentView environment, LevelModel model, int block)
        {
            BoardState board = session.Attempt.Simulation.Board;
            BlockDefinition definition = model.GetBlock(block);
            Cell origin = board.GetOrigin(block);
            if (!TryFindFittingDoorSide(model, definition, origin, out Side side))
            {
                yield break;
            }

            Cell grabCell = origin + definition.GetCell(0);
            Cell outward = SideMath.GetOutward(side);
            Vector2 start = new Vector2(grabCell.X, grabCell.Y);
            float travel = Mathf.Max(model.Width, model.Height) + 1f;

            Move(m_mouse.position, ToScreen(environment, start));
            yield return null;
            Press(m_mouse.leftButton);
            yield return null;
            yield return null;

            for (float moved = k_dragStepCells; moved <= travel; moved += k_dragStepCells)
            {
                Vector2 point = start + new Vector2(outward.X, outward.Y) * moved;
                Move(m_mouse.position, ToScreen(environment, point));
                yield return null;
                if (session.State.Value != SessionState.Playing || board.GetStatus(block) != BlockStatus.OnBoard)
                {
                    break;
                }
            }

            Release(m_mouse.leftButton);
            yield return null;
            yield return null;
        }

        // A door of the block's colour on some side whose span covers the block's extent along that side.
        private static bool TryFindFittingDoorSide(LevelModel model, BlockDefinition definition, Cell origin, out Side side)
        {
            for (int index = 0; index < model.DoorCount; index++)
            {
                DoorDefinition door = model.GetDoor(index);
                bool alongX = SideMath.RunsAlongX(door.Side);
                int lo = alongX ? origin.X + definition.MinX : origin.Y + definition.MinY;
                int hi = alongX ? origin.X + definition.MaxX : origin.Y + definition.MaxY;
                if (door.Color == definition.Color && door.Covers(lo, hi))
                {
                    side = door.Side;
                    return true;
                }
            }

            side = default;
            return false;
        }

        // The same board plane the gameplay projector uses, so the press lands on the block top.
        private static Vector2 ToScreen(EnvironmentView environment, Vector2 boardPoint)
        {
            Vector3 world = environment.BoardRoot.TransformPoint(
                new Vector3(boardPoint.x, ExitMotionSettings.k_blockTopHeight, boardPoint.y));
            Vector3 screen = Camera.main.WorldToScreenPoint(world);
            return new Vector2(screen.x, screen.y);
        }

        private static SceneContext FindGameScope()
        {
            Scene game = SceneManager.GetSceneByName(k_gameScene);
            GameObject[] roots = game.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                SceneContext scope = roots[i].GetComponentInChildren<SceneContext>(true);
                if (scope != null)
                {
                    return scope;
                }
            }

            return null;
        }
    }
}
