using System;
using System.Collections.Generic;
using ColorBlockJam.Presentation.Board;
using PrimeTween;
using UnityEngine;

namespace ColorBlockJam.Environment
{
    /// <summary>
    /// One authored door's gate: its Door_1x1 pieces and arrow move down and up as one. Open sinks it
    /// under the floor, where a floor clip plane hides the part below, holds it and raises it to its
    /// exact built height. EnvironmentView owns every gate and is its only caller; one PrimeTween
    /// Sequence per gate is its only motion, and Stop, SetPaused and Reset are its stop points.
    /// </summary>
    public sealed class EnvironmentGate
    {
        /// <summary>
        /// How far an open gate sinks. The kit's gate is 0.72 high, so a 0.04 coloured strip and its
        /// arrow stay above the floor while a block passes, as the reference sequence shows.
        /// </summary>
        public const float k_sinkDepth = 0.68f;

        /// <summary>A full sink. It ends well inside the shortest exit (ExitMotionSettings.k_minSeconds, 0.2 s).</summary>
        public const float k_sinkSeconds = 0.08f;

        /// <summary>The rise after the last exit; the longest exit (0.6 s) plus it stays under the 0.8 s Won Finish (DEC-93).</summary>
        public const float k_riseSeconds = 0.15f;

        // A step shorter than this is skipped; PrimeTween rejects durations under 0.0001 s.
        private const float k_minStepSeconds = 0.001f;

        // Cached once, so a motion's value callbacks allocate nothing.
        private static readonly Action<EnvironmentGate, float> s_setOffset = SetOffset;

        private readonly List<BoardPieceView> m_pieces = new List<BoardPieceView>();
        private Transform m_boardRoot;
        private float m_closedHeight;
        private float m_offset;
        private Sequence m_motion;
        // True once Open started a motion that may still run. The LevelEditor preview binds and resets
        // gates in Edit Mode, so only a gate that moved touches PrimeTween.
        private bool m_hasMotion;

        public int DoorIndex { get; private set; }

        public int PieceCount => m_pieces.Count;

        /// <summary>How far below its built height the gate stands; 0 at rest.</summary>
        public float Offset => m_offset;

        public bool IsMoving => m_hasMotion && m_motion.isAlive;

        public bool IsPaused => IsMoving && m_motion.isPaused;

        /// <summary>The time the current motion still needs, hold and rise included; 0 at rest.</summary>
        public float SecondsRemaining => IsMoving ? m_motion.durationTotal - m_motion.elapsedTimeTotal : 0f;

        /// <summary>How far down the gate is, from 0 (built height) to 1 (fully sunk).</summary>
        public float OpenFraction => m_offset / k_sinkDepth;

        /// <summary>The time a sink from the current height needs to reach the bottom; 0 when already down.</summary>
        public float SinkSecondsRemaining => k_sinkSeconds * (k_sinkDepth - m_offset) / k_sinkDepth;

        public BoardPieceView GetPiece(int index)
        {
            return m_pieces[index];
        }

        // Home only: the view binds the gate to the door pieces and arrow it rented for one door.
        internal void Bind(int doorIndex, Transform boardRoot, float closedHeight)
        {
            StopMotion();
            m_pieces.Clear();
            DoorIndex = doorIndex;
            m_boardRoot = boardRoot;
            m_closedHeight = closedHeight;
            m_offset = 0f;
        }

        internal void Add(BoardPieceView piece)
        {
            m_pieces.Add(piece);
        }

        // Sinks from where the gate stands now, keeps it down until holdSeconds from now, then raises it.
        // A later call replaces the motion, so the caller passes the longest running exit through the door.
        internal void Open(float holdSeconds)
        {
            StopMotion();
            SetFloorClip(true);
            Sequence motion = Sequence.Create();
            float sinkSeconds = SinkSecondsRemaining;
            if (sinkSeconds >= k_minStepSeconds)
            {
                motion.Chain(Tween.Custom(this, m_offset, k_sinkDepth, sinkSeconds, s_setOffset, Ease.OutQuad));
            }
            else
            {
                sinkSeconds = 0f;
                SetOffset(this, k_sinkDepth);
            }

            float waitSeconds = holdSeconds - sinkSeconds;
            if (waitSeconds >= k_minStepSeconds)
            {
                motion.ChainDelay(waitSeconds);
            }

            motion.Chain(Tween.Custom(this, k_sinkDepth, 0f, k_riseSeconds, s_setOffset, Ease.OutQuad));
            m_motion = motion;
            m_hasMotion = true;
        }

        // Suspend step 3 and Resume.
        internal void SetPaused(bool paused)
        {
            if (IsMoving)
            {
                m_motion.isPaused = paused;
            }
        }

        // Teardown step 3: the gate stays where it is.
        internal void Stop()
        {
            StopMotion();
        }

        // Teardown step 6 and Restart: back at the built height, unclipped, with no motion.
        internal void Reset()
        {
            StopMotion();
            SetOffset(this, 0f);
            SetFloorClip(false);
        }

        private void StopMotion()
        {
            if (m_hasMotion)
            {
                m_motion.Stop();
                m_hasMotion = false;
            }
        }

        private static void SetOffset(EnvironmentGate gate, float offset)
        {
            gate.m_offset = offset;
            float height = gate.m_closedHeight - offset;
            for (int index = 0; index < gate.m_pieces.Count; index++)
            {
                Transform piece = gate.m_pieces[index].transform;
                Vector3 position = piece.localPosition;
                position.y = height;
                piece.localPosition = position;
            }
        }

        // BoardLit keeps fragments with dot(plane, position) >= 0. The plane is the floor surface facing
        // up, so the part of a sunk gate below the floor draws nothing, its shadow included.
        private void SetFloorClip(bool enabled)
        {
            Vector3 normal = m_boardRoot.up;
            Vector3 point = m_boardRoot.TransformPoint(new Vector3(0f, m_closedHeight, 0f));
            Vector4 plane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));
            for (int index = 0; index < m_pieces.Count; index++)
            {
                m_pieces[index].SetClipPlane(plane, enabled);
            }
        }
    }
}
