#if UNITY_EDITOR
using System;
using System.Collections;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Environment
{
    /// <summary>
    /// Verifies the environment gates (environment-runs-gate-motion AC-2 to AC-5): one gate per authored
    /// door groups its Door_1x1 pieces and arrow, sinks them together under a floor clip plane, holds
    /// them, raises them to their exact built height, pauses, stops, resets on Restart and Clear, leaves
    /// every other gate alone and allocates nothing once warm.
    /// </summary>
    public sealed class EnvironmentGateTests
    {
        private const float k_closedHeight = 0f;
        private const float k_margin = 0.1f;
        private static readonly int[] s_lengths = { 3, 2, 1, 3 };
        private static readonly int s_clipPlaneId = Shader.PropertyToID("_ClipPlane");
        private static readonly int s_clipEnabledId = Shader.PropertyToID("_ClipEnabled");

        private GameObject m_root;
        private EnvironmentView m_view;
        private BoardLayout m_layout;
        private float m_cycleSecondsRemaining;

        // The first allocation assertion in a domain also measures the recorder's own setup.
        [OneTimeSetUp]
        public void WarmUpTheAllocationRecorder()
        {
            // Only absorbs the recorder's one-time cost: its first run may count that cost, so it asserts nothing.
            new AllocatingGCMemoryConstraint().ApplyTo((TestDelegate)(() => { }));
        }

        [SetUp]
        public void SetUp()
        {
            m_root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentViewTestSupport.PrefabPath));
            m_view = m_root.GetComponent<EnvironmentView>();
            m_view.Construct(AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(EnvironmentViewTestSupport.DatabasePath));
            LevelDoorData[] doors =
            {
                new LevelDoorData("top", BoardSide.Top, 1, 3, "Red"),
                new LevelDoorData("right", BoardSide.Right, 1, 2, "Blue"),
                new LevelDoorData("bottom", BoardSide.Bottom, 4, 1, "Yellow"),
                new LevelDoorData("left", BoardSide.Left, 0, 3, "Red"),
            };
            Assert.That(BoardLayout.TryCreate(5, 4, Array.Empty<Vector2Int>(), doors, out m_layout, out string error),
                Is.True, error);
            m_view.Build(m_layout);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_root != null)
            {
                Object.DestroyImmediate(m_root);
            }
        }

        [Test]
        public void EachAuthoredDoorBecomesOneGateOverItsSlotsAndArrow()
        {
            Assert.That(m_view.GateCount, Is.EqualTo(s_lengths.Length));
            for (int door = 0; door < s_lengths.Length; door++)
            {
                EnvironmentGate gate = m_view.GetGate(door);
                Assert.That(gate.DoorIndex, Is.EqualTo(door));
                // Door_End closes both ends of a gate (two half-length ends on a one-slot gate), Door_1x1 between.
                int pieces = Math.Max(s_lengths[door], 2);
                Assert.That(gate.PieceCount, Is.EqualTo(pieces + 1), "its slot pieces and one arrow");
                for (int piece = 0; piece < pieces; piece++)
                {
                    bool isEnd = piece == 0 || piece == pieces - 1;
                    Assert.That(gate.GetPiece(piece).name, Does.StartWith(isEnd ? "Door_End" : "Door_1x1"));
                }

                Assert.That(gate.GetPiece(pieces).name, Does.StartWith("Door_Arrow"));
                Assert.That(gate.IsMoving, Is.False);
                AssertOffset(gate, 0f, "built at rest");
            }
        }

        [UnityTest]
        public IEnumerator EverySideAndAuthoredSpanMovesOnlyItsSlotsAndArrow()
        {
            for (int door = 0; door < s_lengths.Length; door++)
            {
                EnvironmentGate gate = m_view.GetGate(door);
                Vector3[] homes = new Vector3[gate.PieceCount];
                for (int piece = 0; piece < homes.Length; piece++)
                {
                    homes[piece] = gate.GetPiece(piece).transform.localPosition;
                }

                Assert.That(m_view.TryOpenGate(door, 0.3f), Is.True);
                yield return new WaitForSeconds(EnvironmentGate.k_sinkSeconds + 0.05f);
                AssertOffset(gate, EnvironmentGate.k_sinkDepth, "the complete authored gate sinks");
                for (int piece = 0; piece < homes.Length; piece++)
                {
                    Vector3 current = gate.GetPiece(piece).transform.localPosition;
                    Assert.That(current.x, Is.EqualTo(homes[piece].x).Within(1e-4f));
                    Assert.That(current.z, Is.EqualTo(homes[piece].z).Within(1e-4f));
                    Assert.That(ReadProperties(gate.GetPiece(piece)).GetFloat(s_clipEnabledId), Is.EqualTo(1f));
                }

                for (int other = 0; other < s_lengths.Length; other++)
                {
                    if (other != door)
                    {
                        AssertOffset(m_view.GetGate(other), 0f, "every unmatched gate stays closed");
                    }
                }

                yield return new WaitForSeconds(0.3f + EnvironmentGate.k_riseSeconds + k_margin);
                AssertOffset(gate, 0f, "the complete gate returns to its built height");
            }
        }

        [UnityTest]
        public IEnumerator AnOpenedGateSinksHoldsAndRisesToItsExactBuiltHeight()
        {
            EnvironmentGate gate = m_view.GetGate(0);
            Assert.That(m_view.TryOpenGate(0, 0.3f), Is.True);
            Assert.That(gate.IsMoving, Is.True);
            Assert.That(m_view.GateSecondsRemaining, Is.EqualTo(0.3f + EnvironmentGate.k_riseSeconds).Within(1e-4f));

            yield return new WaitForSeconds(EnvironmentGate.k_sinkSeconds + 0.05f);
            AssertOffset(gate, EnvironmentGate.k_sinkDepth, "down after its short sink");
            AssertOffset(m_view.GetGate(1), 0f, "another gate stays up");
            AssertOffset(m_view.GetGate(2), 0f, "another gate stays up");
            AssertOffset(m_view.GetGate(3), 0f, "the left gate stays up");

            yield return new WaitForSeconds(0.3f + EnvironmentGate.k_riseSeconds + k_margin);
            Assert.That(gate.IsMoving, Is.False);
            AssertOffset(gate, 0f, "back at its exact built height");
        }

        [Test]
        public void ASinkingGateClipsAtTheFloorAndAResetGateDoesNot()
        {
            Assert.That(m_view.TryOpenGate(0, 0.3f), Is.True);
            EnvironmentGate gate = m_view.GetGate(0);
            Transform boardRoot = m_view.BoardRoot;
            Vector3 floor = boardRoot.TransformPoint(new Vector3(0f, k_closedHeight, 0f));
            Vector3 above = floor + boardRoot.up;
            for (int piece = 0; piece < gate.PieceCount; piece++)
            {
                MaterialPropertyBlock properties = ReadProperties(gate.GetPiece(piece));
                Vector4 plane = properties.GetVector(s_clipPlaneId);
                Assert.That(properties.GetFloat(s_clipEnabledId), Is.EqualTo(1f));
                Assert.That(Vector4.Dot(plane, new Vector4(floor.x, floor.y, floor.z, 1f)), Is.EqualTo(0f).Within(1e-4f));
                Assert.That(Vector4.Dot(plane, new Vector4(above.x, above.y, above.z, 1f)), Is.EqualTo(1f).Within(1e-4f),
                    "the part above the floor stays");
            }

            m_view.ResetGates();
            Assert.That(gate.IsMoving, Is.False);
            AssertOffset(gate, 0f, "Reset snaps it back");
            for (int piece = 0; piece < gate.PieceCount; piece++)
            {
                Assert.That(ReadProperties(gate.GetPiece(piece)).GetFloat(s_clipEnabledId), Is.EqualTo(0f));
            }
        }

        [UnityTest]
        public IEnumerator ReopeningARisingGateSinksItAgainFromWhereItStands()
        {
            EnvironmentGate gate = m_view.GetGate(0);
            m_view.TryOpenGate(0, 0.2f);
            float deadline = Time.realtimeSinceStartup + 1f;
            while (gate.SecondsRemaining > EnvironmentGate.k_riseSeconds * 0.6f && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            float rising = gate.Offset;
            Assert.That(rising, Is.GreaterThan(0f).And.LessThan(EnvironmentGate.k_sinkDepth), "part way up");
            m_view.TryOpenGate(0, 0.3f);
            AssertOffset(gate, rising, "a new exit starts from where the gate stands");

            yield return new WaitForSeconds(EnvironmentGate.k_sinkSeconds + 0.05f);
            AssertOffset(gate, EnvironmentGate.k_sinkDepth, "down again");
            yield return new WaitForSeconds(0.3f + EnvironmentGate.k_riseSeconds + k_margin);
            AssertOffset(gate, 0f, "up again after the new hold");
        }

        [UnityTest]
        public IEnumerator SuspendHoldsAGateStopLeavesItAndResetRestoresItsBuiltPose()
        {
            EnvironmentGate gate = m_view.GetGate(1);
            m_view.TryOpenGate(1, 0.3f);
            yield return new WaitForSeconds(0.04f);
            m_view.SetGateTweensPaused(true);
            Assert.That(gate.IsPaused, Is.True);
            float held = gate.Offset;
            float remaining = m_view.GateSecondsRemaining;
            yield return new WaitForSeconds(0.4f);
            AssertOffset(gate, held, "Suspend holds the gate where it is");
            Assert.That(m_view.GateSecondsRemaining, Is.EqualTo(remaining).Within(1e-5f));

            m_view.SetGateTweensPaused(false);
            Assert.That(gate.IsPaused, Is.False);
            yield return new WaitForSeconds(EnvironmentGate.k_sinkSeconds);
            m_view.StopGateTweens();
            Assert.That(gate.IsMoving, Is.False);
            float stopped = gate.Offset;
            yield return null;
            AssertOffset(gate, stopped, "a stopped gate stays where it was");

            m_view.ResetGates();
            AssertOffset(gate, 0f, "Reset restores its built height");
        }

        [UnityTest]
        public IEnumerator RestartBuildResetsTheGatesAndClearForgetsThem()
        {
            m_view.TryOpenGate(2, 0.3f);
            yield return new WaitForSeconds(EnvironmentGate.k_sinkSeconds + 0.05f);
            EnvironmentGate gate = m_view.GetGate(2);
            BoardPieceView door = gate.GetPiece(0);
            AssertOffset(gate, EnvironmentGate.k_sinkDepth, "down");

            m_view.Build(m_layout);
            Assert.That(m_view.GetGate(2), Is.SameAs(gate));
            Assert.That(gate.GetPiece(0), Is.SameAs(door), "Restart keeps the same pieces");
            Assert.That(gate.IsMoving, Is.False);
            AssertOffset(gate, 0f, "Restart resets the gate");

            m_view.Clear();
            Assert.That(m_view.GateCount, Is.Zero);
            Assert.That(m_view.GateSecondsRemaining, Is.Zero);
            Assert.That(m_view.TryOpenGate(2, 0.3f), Is.False, "a cleared play area has no gate");
        }

        [Test]
        public void AnUnknownDoorHasNoGateAndMovesNothing()
        {
            Assert.That(m_view.TryOpenGate(-1, 0.3f), Is.False);
            Assert.That(m_view.TryOpenGate(s_lengths.Length, 0.3f), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => m_view.GetGate(s_lengths.Length));
            for (int door = 0; door < s_lengths.Length; door++)
            {
                Assert.That(m_view.GetGate(door).IsMoving, Is.False);
            }
        }

        [Test]
        public void OpeningPausingStoppingAndResettingGatesAllocateNothing()
        {
            Cycle();
            Assert.That(m_cycleSecondsRemaining, Is.GreaterThan(0f));
            Assert.That(() => Cycle(), Is.Not.AllocatingGCMemory());
            Assert.That(m_cycleSecondsRemaining, Is.GreaterThan(0f));
        }

        // No assertion runs inside the measured scope; the caller checks what it recorded.
        private void Cycle()
        {
            m_view.TryOpenGate(0, 0.3f);
            m_view.TryOpenGate(0, 0.4f);
            m_view.TryOpenGate(2, 0.2f);
            m_cycleSecondsRemaining = m_view.GateSecondsRemaining;
            m_view.SetGateTweensPaused(true);
            m_view.SetGateTweensPaused(false);
            m_view.StopGateTweens();
            m_view.ResetGates();
        }

        private static void AssertOffset(EnvironmentGate gate, float offset, string message)
        {
            Assert.That(gate.Offset, Is.EqualTo(offset).Within(1e-4f), message);
            for (int piece = 0; piece < gate.PieceCount; piece++)
            {
                Assert.That(gate.GetPiece(piece).transform.localPosition.y,
                    Is.EqualTo(k_closedHeight - offset).Within(1e-4f), message);
            }
        }

        private static MaterialPropertyBlock ReadProperties(BoardPieceView piece)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            piece.Renderer.GetPropertyBlock(properties);
            return properties;
        }
    }
}
#endif
