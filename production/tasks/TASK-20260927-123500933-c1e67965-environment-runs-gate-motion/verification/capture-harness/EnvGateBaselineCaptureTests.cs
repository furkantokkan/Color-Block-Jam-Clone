// Copy-only BASELINE verification harness for TASK-20260927-123500933-c1e67965-environment-runs-gate-motion.
// Lives only in the isolated %TEMP% copy; never copied back. Renders the real Game scene camera
// (DEC-103/118 framing, light and clear colour) into 1080x1920 and 1080x2400 targets.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Environment;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using Onity.Unity.Contexts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EnvGateCapture
{
    public sealed class EnvGateCaptureTests : InputTestFixture
    {
        private const float k_step = 0.1f;
        private static readonly string s_out =
            System.Environment.GetEnvironmentVariable("ENVGATE_CAPTURE_OUT")
            ?? @"C:\Users\e-fur\AppData\Local\Temp\cbj-envgate-7336b1-results\captures-base";

        private Mouse m_mouse;
        private Camera m_camera;
        private BoardCamera m_boardCamera;
        private EnvironmentView m_environment;
        private RenderTexture m_rt1920;
        private RenderTexture m_rt2400;
        private Texture2D m_tex1920;
        private Texture2D m_tex2400;

        public override void Setup()
        {
            base.Setup();
            m_mouse = InputSystem.AddDevice<Mouse>();
            Directory.CreateDirectory(s_out);
            m_rt1920 = new RenderTexture(1080, 1920, 24);
            m_rt2400 = new RenderTexture(1080, 2400, 24);
            m_tex1920 = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
            m_tex2400 = new Texture2D(1080, 2400, TextureFormat.RGB24, false);
            Time.captureFramerate = 60;
        }

        public override void TearDown()
        {
            Time.captureFramerate = 0;
            if (m_camera != null) m_camera.targetTexture = null;
            m_rt1920.Release();
            m_rt2400.Release();
            Object.Destroy(m_rt1920);
            Object.Destroy(m_rt2400);
            Object.Destroy(m_tex1920);
            Object.Destroy(m_tex2400);
            base.TearDown();
        }

        [UnityTest, Order(1)]
        public IEnumerator A_BaselineLevelOneIdle()
        {
            SceneContext scope = null;
            yield return LoadGame(s => scope = s);
            yield return null;
            yield return null;
            CaptureBoth("L1-00-idle");
        }

        [UnityTest, Order(2)]
        public IEnumerator B_BaselineEnvironmentFixtures()
        {
            SceneContext scope = null;
            yield return LoadGame(s => scope = s);
            Transform blocks = m_environment.BoardRoot.Find("Blocks");
            if (blocks != null) blocks.gameObject.SetActive(false);

            var fixtures = new List<(string name, BoardLayout layout)>();
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "ColorBlockJam/Data/Levels/LevelDatabase.json"));
            Assert.That(LevelDatabaseJson.TryRead(json, out LevelData[] levels, out string error), Is.True, error);
            foreach (LevelData level in levels)
            {
                Assert.That(BoardLayout.TryCreate(level, out BoardLayout layout, out error), Is.True, error);
                fixtures.Add((level.Id, layout));
            }

            fixtures.Add(("fx-notch", Layout(5, 5, new[] { new Vector2Int(4, 4) },
                new LevelDoorData("t", BoardSide.Top, 1, 2, "Red"))));
            fixtures.Add(("fx-hole", Layout(6, 6, new[] { new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(2, 3),
                new Vector2Int(3, 3), new Vector2Int(0, 5), new Vector2Int(1, 5) },
                new LevelDoorData("b", BoardSide.Bottom, 1, 3, "Blue"))));
            fixtures.Add(("fx-pinch", Layout(3, 3, new[] { new Vector2Int(1, 0), new Vector2Int(0, 1) })));
            fixtures.Add(("fx-corridor", Layout(5, 3, new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
                new Vector2Int(3, 0), new Vector2Int(4, 0), new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2),
                new Vector2Int(3, 2), new Vector2Int(4, 2) },
                new LevelDoorData("r", BoardSide.Right, 1, 1, "DarkGreen"))));
            fixtures.Add(("fx-spans", Layout(6, 7, Array.Empty<Vector2Int>(),
                new LevelDoorData("t1", BoardSide.Top, 0, 1, "Red"),
                new LevelDoorData("t2", BoardSide.Top, 2, 3, "Blue"),
                new LevelDoorData("l", BoardSide.Left, 1, 2, "Yellow"),
                new LevelDoorData("r", BoardSide.Right, 3, 3, "DarkGreen"),
                new LevelDoorData("b", BoardSide.Bottom, 2, 2, "Pink"))));

            foreach ((string name, BoardLayout layout) in fixtures)
            {
                m_environment.Build(layout);
                yield return null;
                CaptureBoth("fx-" + name);
            }

        }

        private static BoardLayout Layout(int width, int height, Vector2Int[] voids, params LevelDoorData[] doors)
        {
            Assert.That(BoardLayout.TryCreate(width, height, voids, doors, out BoardLayout layout, out string error), Is.True, error);
            return layout;
        }

        private IEnumerator LoadGame(Action<SceneContext> found)
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            SceneContext scope = null;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                foreach (SceneContext candidate in Object.FindObjectsOfType<SceneContext>())
                {
                    if (candidate.gameObject.scene.name == "Game") scope = candidate;
                }

                if (scope != null && scope.IsReady) break;
                yield return null;
            }

            Assert.That(scope != null && scope.IsReady, Is.True, "Game scope ready");
            LevelSession session = scope.Container.Resolve<LevelSession>();
            while (session.State.Value != SessionState.Playing && Time.realtimeSinceStartup < deadline) yield return null;
            m_environment = scope.Container.Resolve<EnvironmentView>();
            m_camera = Camera.main;
            m_boardCamera = m_camera.GetComponent<BoardCamera>();
            found(scope);
        }

        private void CaptureBoth(string name)
        {
            CaptureOne(m_rt1920, m_tex1920, name + "-1080x1920");
            CaptureOne(m_rt2400, m_tex2400, name + "-1080x2400");
        }

        // Frames the camera for the target's aspect, renders, saves, then restores the play framing.
        private void CaptureOne(RenderTexture target, Texture2D texture, string name)
        {
            RenderTexture previous = m_camera.targetTexture;
            Vector3 position = m_camera.transform.position;
            Quaternion rotation = m_camera.transform.rotation;
            m_camera.targetTexture = target;
            m_camera.ResetAspect();
            Bounds local = m_environment.Layout.LocalBounds;
            m_boardCamera.Frame(new Bounds(m_environment.BoardRoot.TransformPoint(local.center), local.size));
            m_camera.Render();
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(s_out, name + ".png"), texture.EncodeToPNG());
            m_camera.targetTexture = previous;
            m_camera.ResetAspect();
            m_camera.transform.SetPositionAndRotation(position, rotation);
        }

        private static bool TryFindDoor(LevelModel model, int block, Cell origin, out Side side, out int doorIndex)
        {
            BlockDefinition definition = model.GetBlock(block);
            for (int index = 0; index < model.DoorCount; index++)
            {
                DoorDefinition door = model.GetDoor(index);
                bool alongX = SideMath.RunsAlongX(door.Side);
                int lo = alongX ? origin.X + definition.MinX : origin.Y + definition.MinY;
                int hi = alongX ? origin.X + definition.MaxX : origin.Y + definition.MaxY;
                if (door.Color == definition.Color && door.Covers(lo, hi))
                {
                    side = door.Side;
                    doorIndex = index;
                    return true;
                }
            }

            side = default;
            doorIndex = -1;
            return false;
        }

        private Vector2 ToScreen(Vector2 boardPoint)
        {
            Vector3 world = m_environment.BoardRoot.TransformPoint(
                new Vector3(boardPoint.x, ExitMotionSettings.k_blockTopHeight, boardPoint.y));
            Vector3 screen = m_camera.WorldToScreenPoint(world);
            return new Vector2(screen.x, screen.y);
        }
    }
}
