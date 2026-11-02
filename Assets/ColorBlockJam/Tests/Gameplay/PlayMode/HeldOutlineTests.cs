using System;
using System.Collections;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Environment;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Board.Outline;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Presentation.Board;
using HighlightPlus;
using NUnit.Framework;
using Onity.Unity.Contexts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Gameplay
{
    /// <summary>
    /// Proves the held-block outline (DEC-89, DEC-145, contract acceptance 1-4) in the real Game scene with a
    /// simulated mouse: every placed root carries one white Highlight Plus outline around all its tiles, a
    /// press switches on only the pressed block's, and release, Pause, Restart and Home switch it off.
    /// </summary>
    public sealed class HeldOutlineTests : InputTestFixture
    {
        private const string k_gameScene = "Game";
        private const float k_buildTimeoutSeconds = 20f;

        private Mouse m_mouse;
        private bool m_sceneLoaded;

        public override void Setup()
        {
            base.Setup();
            m_mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            if (m_sceneLoaded)
            {
                m_sceneLoaded = false;
                yield return SceneManager.UnloadSceneAsync(k_gameScene);
            }

            yield return null;
        }

        [Test]
        public void Prepare_ConfiguresOneWhiteChildrenOutlineAndStartsOff()
        {
            GameObject root = new GameObject("Root");
            try
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.transform.SetParent(root.transform, false);

                HighlightEffect effect = HeldBlockHighlight.Prepare(root);
                HighlightEffect again = HeldBlockHighlight.Prepare(root);

                Assert.That(again, Is.SameAs(effect), "a pooled root keeps its one effect");
                Assert.That(root.GetComponents<HighlightEffect>().Length, Is.EqualTo(1));
                Assert.That(effect.effectGroup, Is.EqualTo(TargetOptions.Children), "all tiles form one silhouette");
                Assert.That(effect.outlineColor, Is.EqualTo(Color.white));
                Assert.That(effect.outlineVisibility, Is.EqualTo(Visibility.AlwaysOnTop), "no wall or neighbour hides it");
                Assert.That(effect.glow, Is.Zero);
                Assert.That(effect.highlighted, Is.False);
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator GameScene_PressHighlightsOnlyThePressedBlockAndReleaseClearsIt()
        {
            SceneContext scope = null;
            yield return LoadGame(found => scope = found);
            LevelSession session = scope.Container.Resolve<LevelSession>();
            EnvironmentView environment = scope.Container.Resolve<EnvironmentView>();
            IUiPointerFilter ui = scope.Container.Resolve<IUiPointerFilter>();
            Assert.That(CountHighlighted(environment), Is.Zero, "nothing is held before a press");

            int held = 0;
            for (int block = 0; block < session.Attempt.Level.Model.BlockCount; block++)
            {
                yield return PressBlock(session, environment, ui, block);

                // A block its effects keep still (level 1's Ice block) refuses the drag and shows no outline.
                bool dragging = session.Attempt.Simulation.Drag.IsActive && session.Attempt.Simulation.Drag.Block == block;
                Assert.That(CountHighlighted(environment), Is.EqualTo(dragging ? 1 : 0), $"block {block}: outline only while held");
                if (dragging)
                {
                    held++;
                }

                Release(m_mouse.leftButton);
                yield return null;
                yield return null;
                Assert.That(CountHighlighted(environment), Is.Zero, $"release of block {block} clears the outline");
            }

            Assert.That(held, Is.GreaterThan(0), "at least one block of the level was held");
        }

        [UnityTest]
        public IEnumerator GameScene_PauseRestartAndHomeClearTheOutline()
        {
            SceneContext scope = null;
            yield return LoadGame(found => scope = found);
            LevelSession session = scope.Container.Resolve<LevelSession>();
            EnvironmentView environment = scope.Container.Resolve<EnvironmentView>();
            IUiPointerFilter ui = scope.Container.Resolve<IUiPointerFilter>();

            yield return PressBlock(session, environment, ui, 0);
            Assert.That(CountHighlighted(environment), Is.EqualTo(1));
            Assert.That(session.Pause(PauseReason.User), Is.True);
            Assert.That(CountHighlighted(environment), Is.Zero, "Suspend snaps the held block and clears the outline");
            Release(m_mouse.leftButton);
            yield return null;
            Assert.That(session.Resume(), Is.True);
            yield return null;
            yield return PressBlock(session, environment, ui, 0);
            Assert.That(CountHighlighted(environment), Is.EqualTo(1), "a press after Resume shows it again");

            // Restart and Home are left from Paused (PROJECT.md game states).
            Assert.That(session.Pause(PauseReason.User), Is.True);
            Release(m_mouse.leftButton);
            yield return null;
            Assert.That(session.Restart(), Is.True);
            Assert.That(CountHighlighted(environment), Is.Zero, "no root goes back to its pool highlighted");
            yield return null;
            yield return PressBlock(session, environment, ui, 0);
            Assert.That(CountHighlighted(environment), Is.EqualTo(1), "the rebuilt attempt shows the outline again");

            Assert.That(session.Pause(PauseReason.User), Is.True);
            Release(m_mouse.leftButton);
            yield return null;
            Assert.That(session.GoHome(), Is.True);
            Assert.That(CountHighlighted(environment), Is.Zero, "Home tears the attempt down");
        }

        private static int CountHighlighted(EnvironmentView environment)
        {
            int count = 0;
            foreach (HighlightEffect effect in environment.BoardRoot.GetComponentsInChildren<HighlightEffect>(true))
            {
                if (effect.highlighted)
                {
                    Assert.That(effect.GetComponent<BlockPiece>(), Is.Not.Null, "only block roots carry the outline");
                    count++;
                }
            }

            return count;
        }

        // Presses the first cell of the block that no runtime UI covers; the batch test window is small, so a HUD
        // element can lie over part of the board there.
        private IEnumerator PressBlock(LevelSession session, EnvironmentView environment, IUiPointerFilter ui, int block)
        {
            Cell origin = session.Attempt.Simulation.Board.GetOrigin(block);
            BlockDefinition definition = session.Attempt.Level.Model.GetBlock(block);
            Vector2 screen = default;
            bool found = false;
            for (int cell = 0; cell < definition.CellCount && !found; cell++)
            {
                Cell grab = origin + definition.GetCell(cell);
                screen = ToScreen(environment, new Vector2(grab.X, grab.Y));
                found = !ui.IsOverUi(screen);
            }

            Assert.That(found, Is.True, $"block {block} has a cell outside the runtime UI");
            Move(m_mouse.position, screen);
            yield return null;
            Press(m_mouse.leftButton);
            yield return null;
            yield return null;
        }

        private IEnumerator LoadGame(Action<SceneContext> found)
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
            while (session.State.Value != SessionState.Playing && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing));
            found(scope);
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
            GameObject[] roots = SceneManager.GetSceneByName(k_gameScene).GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                SceneContext scope = roots[index].GetComponentInChildren<SceneContext>(true);
                if (scope != null)
                {
                    return scope;
                }
            }

            return null;
        }
    }
}
