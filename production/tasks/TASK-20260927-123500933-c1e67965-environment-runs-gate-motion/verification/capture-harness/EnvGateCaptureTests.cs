// Copy-only verification harness for TASK-20260927-123500933-c1e67965-environment-runs-gate-motion.
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
            ?? @"C:\Users\e-fur\AppData\Local\Temp\cbj-envgate-7336b1-results\captures";

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
        public IEnumerator A_LevelOneExitSequence()
        {
            SceneContext scope = null;
            yield return LoadGame(s => scope = s);
            LevelSession session = scope.Container.Resolve<LevelSession>();
            BoardPresenter presenter = scope.Container.Resolve<BoardPresenter>();
            LevelModel model = session.Attempt.Level.Model;
            Assert.That(session.Attempt.Level.LevelId, Is.EqualTo("level-01"));

            yield return null;
            yield return null;
            CaptureBoth("L1-00-idle");

            // Block 0 (LightBlue 2x2 at (2,2)) leaves through door 0 (Top, cols 1-3).
            int block = 0;
            Assert.That(TryFindDoor(model, block, session.Attempt.Simulation.Board.GetOrigin(block), out Side side, out int door), Is.True);
            EnvironmentGate gate = m_environment.GetGate(door);
            var csv = new StringBuilder("frame,phase,pointerCells,gateOffset,gateMoving,domainStatus,pieceAlive,pieceX,pieceY,pieceZ,finishSeconds,fragments\n");

            BoardState board = session.Attempt.Simulation.Board;
            BlockDefinition definition = model.GetBlock(block);
            Cell origin = board.GetOrigin(block);
            Cell grab = origin + definition.GetCell(0);
            Cell outward = SideMath.GetOutward(side);
            Vector2 start = new Vector2(grab.X, grab.Y);

            Move(m_mouse.position, ToScreen(start));
            yield return null;
            Press(m_mouse.leftButton);
            yield return null;
            yield return null;

            int frame = 0;
            float moved = 0f;
            bool leanHeld = false, exitSeen = false, goneSeen = false, riseShot = false, settledShot = false, sinkShot = false, midShot = false;
            int exitFrame = -1, goneFrame = -1;
            string phase = "drag";
            while (frame < 400)
            {
                bool onBoard = board.GetStatus(block) == BlockStatus.OnBoard;
                if (onBoard)
                {
                    if (!leanHeld && gate.Offset > 0f)
                    {
                        // The held block leans into its door: hold the pointer here and film the lean.
                        leanHeld = true;
                        phase = "lean";
                        for (int hold = 0; hold < 14; hold++)
                        {
                            Record(csv, frame++, phase, moved, gate, board, block, presenter);
                            CaptureOne(m_rt1920, m_tex1920, $"seq-{frame:D3}-lean");
                            if (hold == 10) CaptureBoth("L1-01-lean-held");
                            yield return null;
                        }

                        phase = "push";
                    }

                    moved += k_step;
                    Vector2 point = start + new Vector2(outward.X, outward.Y) * moved;
                    Move(m_mouse.position, ToScreen(point));
                }
                else if (!exitSeen)
                {
                    exitSeen = true;
                    exitFrame = frame;
                    phase = "exit";
                    Release(m_mouse.leftButton);
                }

                yield return null;
                frame++;
                bool alive = presenter.GetPiece(block) != null;
                if (exitSeen && !alive && !goneSeen)
                {
                    goneSeen = true;
                    goneFrame = frame;
                    phase = "rise";
                    CaptureBoth("L1-05-root-returned");
                }

                Record(csv, frame, phase, moved, gate, board, block, presenter);
                if (leanHeld || exitSeen) CaptureBoth($"seq-{frame:D3}-{phase}");

                if (exitSeen && !sinkShot && frame == exitFrame + 2) { sinkShot = true; CaptureBoth("L1-02-exit-start"); }
                if (exitSeen && !midShot && alive && frame == exitFrame + 12) { midShot = true; CaptureBoth("L1-03-mid-exit"); }
                if (goneSeen && !riseShot && gate.Offset > 0.05f && gate.Offset < EnvironmentGate.k_sinkDepth - 0.05f)
                {
                    riseShot = true;
                    CaptureBoth("L1-06-rising");
                }

                if (goneSeen && !gate.IsMoving && gate.Offset == 0f)
                {
                    if (!settledShot)
                    {
                        settledShot = true;
                        CaptureBoth("L1-07-settled");
                        phase = "settled";
                    }

                    if (frame > goneFrame + 30) break;
                }
            }

            File.WriteAllText(Path.Combine(s_out, "L1-sequence.csv"), csv.ToString());
            Assert.That(exitSeen, Is.True, "block 0 exited");
            Assert.That(goneSeen, Is.True, "block 0 body disappeared");
            Assert.That(settledShot, Is.True, "gate settled");

            // Win with block 1 through the bottom door and time the bounded Finish.
            int other = 1;
            Assert.That(TryFindDoor(model, other, board.GetOrigin(other), out Side otherSide, out int otherDoor), Is.True);
            BlockDefinition otherDef = model.GetBlock(other);
            Cell otherGrab = board.GetOrigin(other) + otherDef.GetCell(0);
            Cell otherOut = SideMath.GetOutward(otherSide);
            Vector2 otherStart = new Vector2(otherGrab.X, otherGrab.Y);
            Move(m_mouse.position, ToScreen(otherStart));
            yield return null;
            Press(m_mouse.leftButton);
            yield return null;
            yield return null;
            var win = new StringBuilder("frame,state,finishSeconds,gate0Offset,gate1Offset,attempt\n");
            int wonFrame = -1;
            for (int f = 0; f < 200; f++)
            {
                if (session.State.Value == SessionState.Playing && board.GetStatus(other) == BlockStatus.OnBoard)
                {
                    Vector2 point = otherStart + new Vector2(otherOut.X, otherOut.Y) * (k_step * (f + 1));
                    Move(m_mouse.position, ToScreen(point));
                }
                else if (wonFrame < 0)
                {
                    wonFrame = f;
                    Release(m_mouse.leftButton);
                }

                yield return null;
                bool built = m_environment.GateCount > otherDoor;
                win.AppendLine(string.Join(",", f, session.State.Value,
                    session.Attempt != null ? presenter.FinishSeconds.ToString("F4", CultureInfo.InvariantCulture) : "-",
                    built ? m_environment.GetGate(door).Offset.ToString("F4", CultureInfo.InvariantCulture) : "-",
                    built ? m_environment.GetGate(otherDoor).Offset.ToString("F4", CultureInfo.InvariantCulture) : "-",
                    session.Attempt != null));
                if (wonFrame >= 0 && session.Attempt == null && f > wonFrame + 5) break;
            }

            File.WriteAllText(Path.Combine(s_out, "L1-win.csv"), win.ToString());
            Assert.That(session.State.Value, Is.EqualTo(SessionState.Won));
        }

        [UnityTest, Order(3)]
        public IEnumerator C_LevelOneFlickExitsFromARaisedGate()
        {
            SceneContext scope = null;
            yield return LoadGame(s => scope = s);
            LevelSession session = scope.Container.Resolve<LevelSession>();
            BoardPresenter presenter = scope.Container.Resolve<BoardPresenter>();
            LevelModel model = session.Attempt.Level.Model;
            BoardState board = session.Attempt.Simulation.Board;
            int block = 0;
            Assert.That(TryFindDoor(model, block, board.GetOrigin(block), out Side side, out int door), Is.True);
            EnvironmentGate gate = m_environment.GetGate(door);
            Cell grab = board.GetOrigin(block) + model.GetBlock(block).GetCell(0);
            Cell outward = SideMath.GetOutward(side);
            Vector2 start = new Vector2(grab.X, grab.Y);
            var csv = new StringBuilder("frame,phase,pointerCells,gateOffset,gateMoving,domainStatus,pieceAlive,pieceX,pieceY,pieceZ,finishSeconds,fragments\n");

            Move(m_mouse.position, ToScreen(start));
            yield return null;
            Press(m_mouse.leftButton);
            yield return null;
            yield return null;
            Record(csv, 0, "raised", 0f, gate, board, block, presenter);
            CaptureBoth("flick-000-raised");

            // One pointer jump far past the push threshold: the exit is accepted in one command phase,
            // before any lean could lower the gate, so the block must wait for the sink inside its exit time.
            Move(m_mouse.position, ToScreen(start + new Vector2(outward.X, outward.Y) * 2.5f));
            bool released = false;
            int settled = -1;
            for (int frame = 1; frame < 120; frame++)
            {
                yield return null;
                if (!released && board.GetStatus(block) != BlockStatus.OnBoard)
                {
                    released = true;
                    Release(m_mouse.leftButton);
                }

                string phase = board.GetStatus(block) == BlockStatus.OnBoard ? "drag" : "exit";
                Record(csv, frame, phase, 2.5f, gate, board, block, presenter);
                CaptureBoth($"flick-{frame:D3}-{phase}");
                if (released && presenter.GetPiece(block) == null && !gate.IsMoving && gate.Offset == 0f)
                {
                    if (settled < 0) settled = frame;
                    if (frame > settled + 3) break;
                }
            }

            File.WriteAllText(Path.Combine(s_out, "L1-flick.csv"), csv.ToString());
            Assert.That(released, Is.True, "the flick exited");
            Assert.That(settled, Is.GreaterThan(0), "the gate settled");
        }

        [UnityTest, Order(2)]
        public IEnumerator B_EnvironmentFixtures()
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
            // Camera-side corner notches (level 9 style).
            fixtures.Add(("fx-near-notch", Layout(5, 5, new[] { new Vector2Int(0, 0), new Vector2Int(4, 0), new Vector2Int(2, 0) })));
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

            // Independent gates: level-05 has two top doors; lower only door 0.
            BoardLayout five = fixtures.Find(f => f.name == "level-05").layout;
            m_environment.Build(five);
            yield return null;
            Assert.That(m_environment.TryOpenGate(0, 1f), Is.True);
            for (int i = 0; i < 12; i++) yield return null;
            File.WriteAllText(Path.Combine(s_out, "level-05-gates.txt"),
                $"gate0 offset {m_environment.GetGate(0).Offset:F4}; gate1 offset {m_environment.GetGate(1).Offset:F4}; gates {m_environment.GateCount}");
            CaptureBoth("fx-level-05-gate0-down");
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

        private static void Record(StringBuilder csv, int frame, string phase, float moved, EnvironmentGate gate,
            BoardState board, int block, BoardPresenter presenter)
        {
            var piece = presenter.GetPiece(block);
            Vector3 p = piece != null ? piece.transform.localPosition : Vector3.zero;
            csv.AppendLine(string.Join(",", frame, phase, moved.ToString("F2", CultureInfo.InvariantCulture),
                gate.Offset.ToString("F4", CultureInfo.InvariantCulture), gate.IsMoving, board.GetStatus(block), piece != null,
                p.x.ToString("F3", CultureInfo.InvariantCulture), p.y.ToString("F3", CultureInfo.InvariantCulture),
                p.z.ToString("F3", CultureInfo.InvariantCulture),
                presenter.FinishSeconds.ToString("F4", CultureInfo.InvariantCulture), presenter.ActiveExitFragmentCount));
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
