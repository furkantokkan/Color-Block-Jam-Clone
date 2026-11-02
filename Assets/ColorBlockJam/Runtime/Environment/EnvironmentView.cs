using System;
using System.Collections.Generic;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using Onity.DI;
using Onity.Pooling;
using PrimeTween;
using UnityEngine;

namespace ColorBlockJam.Environment
{
    /// <summary>
    /// Play-area view: builds one level's floor, floor base, walls, corners, doors and Barrier
    /// board objects from Onity pools, placing each piece through the BoardRoot Grid, and returns
    /// every piece on Clear. It has no Update; its owner calls Build on Home and Clear or Release on
    /// a level change or scene unload. Barriers move only when the gameplay owner calls SetBarrierOpen,
    /// and each authored door's EnvironmentGate only through the board presenter's TryOpenGate calls.
    /// </summary>
    public sealed class EnvironmentView : MonoBehaviour
    {
        // Released pieces stay in their pool so a later Build reuses them.
        private const int k_poolMaxSize = 1024;
        // An open barrier sinks this far: the board kit's 0.72 wall height plus a margin, so the
        // floor tile and base square over its cells hide it completely.
        private const float k_barrierSinkDepth = 0.8f;
        // One rise or sink of a barrier piece (catalog OBJ-05: a short tween per piece).
        private const float k_barrierSeconds = 0.25f;
        // Cached once, so a sink's completion callback allocates nothing.
        private static readonly Action<MeshRenderer> s_hideRenderer = HideRenderer;

        [SerializeField] private Transform m_boardRoot;
        // The single coordinate authority: half-cell quadrant cells, swizzled onto the XZ board.
        [SerializeField] private Grid m_grid;

        private readonly List<EnvironmentPiecePlacement> m_placements = new List<EnvironmentPiecePlacement>();
        private readonly List<EnvironmentPiecePlacement> m_barrierPlacements = new List<EnvironmentPiecePlacement>();
        // Composed into m_composedBarriers first, so a failed compose keeps the shown barriers intact.
        private readonly List<EnvironmentBarrier> m_composedBarriers = new List<EnvironmentBarrier>();
        private readonly List<EnvironmentBarrier> m_barriers = new List<EnvironmentBarrier>();
        // Built on Home, so SetBarrierOpen finds a barrier's piece range without allocating.
        private readonly Dictionary<string, int> m_barrierIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<BoardPieceView> m_pieces = new List<BoardPieceView>();
        private readonly List<EnvironmentGateSpan> m_gateSpans = new List<EnvironmentGateSpan>();
        // One gate per authored door, at its door index; instances are reused across builds.
        private readonly List<EnvironmentGate> m_gates = new List<EnvironmentGate>();
        private int m_gateCount;
        private EnvironmentDatabase m_database;
        private EnvironmentComposer m_composer;
        private EnvironmentBarrierComposer m_barrierComposer;
        private PrefabComponentPool<BoardPieceView>[] m_pools;
        private BoardPieceView[] m_active = Array.Empty<BoardPieceView>();
        private int[] m_activePools = Array.Empty<int>();
        private int m_activeCount;
        // Barrier pieces follow the play-area pieces in m_active, starting here.
        private int m_barrierPieceStart;
        private float m_barrierClosedHeight;
        // True once SetBarrierOpen started a tween that may still run. The LevelEditor preview only
        // snaps, so Edit Mode never touches PrimeTween.
        private bool m_hasBarrierTweens;
        private IBoardLayout m_layout;
        private LevelData m_level;

        public IBoardLayout Layout => m_layout;
        public Transform BoardRoot => m_boardRoot;
        public bool IsBuilt => m_layout != null;
        public int ActivePieceCount => m_activeCount;
        public int BarrierCount => m_barriers.Count;
        public int GateCount => m_gateCount;

        /// <summary>The longest time a gate still needs to reach rest; the board presenter's Won Finish waits for it.</summary>
        public float GateSecondsRemaining
        {
            get
            {
                float longest = 0f;
                for (int index = 0; index < m_gateCount; index++)
                {
                    longest = Math.Max(longest, m_gates[index].SecondsRemaining);
                }

                return longest;
            }
        }

        [Inject]
        public void Construct(EnvironmentDatabase database)
        {
            m_database = database;
        }

        /// <summary>Shows a level's play area without board objects; see Build(IBoardLayout, LevelData).</summary>
        public void Build(IBoardLayout layout)
        {
            Build(layout, null);
        }

        /// <summary>
        /// Shows a level's play area and the Barrier board objects of level (null for none), each at
        /// its start height. Call it outside Playing: pools grow and barrier parameters are read here
        /// only. Building the layout and level already shown (Restart) keeps every piece and only
        /// returns the barriers to their start state.
        /// </summary>
        public void Build(IBoardLayout layout, LevelData level)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (m_database == null) throw new InvalidOperationException("Inject the environment database before Build.");
            if (m_grid == null || m_grid.transform != m_boardRoot)
            {
                // Pieces are BoardRoot children, so only a Grid on BoardRoot maps them to local space.
                throw new InvalidOperationException("The environment prefab needs its Grid on BoardRoot.");
            }

            if (ReferenceEquals(layout, m_layout) && ReferenceEquals(level, m_level))
            {
                ResetBarriers();
                ResetGates();
                return;
            }

            if (m_composer == null)
            {
                m_composer = new EnvironmentComposer(m_database);
                m_barrierComposer = new EnvironmentBarrierComposer(m_database);
                CreatePools();
            }

            // Compose first so an invalid layout or level leaves the current play area visible.
            m_composer.ComposePlayArea(layout, m_placements, m_gateSpans);
            m_barrierComposer.Compose(layout, level, m_barrierPlacements, m_composedBarriers);
            Clear();
            if (!HasPools(m_placements) || !HasPools(m_barrierPlacements))
            {
                // A rule set edit gave a rule an output that has no pool yet. Recreate the
                // pools here, on Home, instead of failing on an unknown piece.
                DisposePools();
                CreatePools();
            }

            int pieceCount = m_placements.Count + m_barrierPlacements.Count;
            if (m_active.Length < pieceCount)
            {
                m_active = new BoardPieceView[pieceCount];
                m_activePools = new int[pieceCount];
            }

            AddPieces(m_placements);
            BindGates();
            m_barrierPieceStart = m_activeCount;
            AddPieces(m_barrierPlacements);
            // Barriers are composed at grid height 0, the closed (raised) height.
            m_barrierClosedHeight = m_grid.CellToLocalInterpolated(Vector3.zero).y;
            for (int index = 0; index < m_composedBarriers.Count; index++)
            {
                m_barriers.Add(m_composedBarriers[index]);
                m_barrierIndices.Add(m_composedBarriers[index].Id, index);
            }

            ResetBarriers();

            // Centre the board on this object so cameras and UI can frame it.
            Vector3 center = layout.LocalBounds.center;
            m_boardRoot.localPosition = new Vector3(-center.x, 0f, -center.z);
            m_layout = layout;
            m_level = level;
        }

        /// <summary>
        /// Raises (closed) or sinks below the floor (open) one shown barrier. Owner: the gameplay
        /// session's barrier adapter, which animates each flip in the Playing command phase and
        /// snaps at attempt build and Restart. Each tween targets its piece's Transform, so Suspend
        /// pauses it (SetBarrierTweensPaused) and Teardown stops it (StopBarrierTweens or Clear).
        /// A piece at rest under the floor has its renderer off, so an open barrier draws nothing.
        /// It changes presentation only and allocates nothing.
        /// </summary>
        public void SetBarrierOpen(string objectId, bool open, bool animate)
        {
            if (!m_barrierIndices.TryGetValue(objectId, out int barrier))
            {
                throw new ArgumentException("No shown barrier has ID " + objectId + ".", nameof(objectId));
            }

            MoveBarrier(m_barriers[barrier], open, animate);
        }

        /// <summary>Suspend step 3 and Resume: pauses or continues every barrier tween.</summary>
        public void SetBarrierTweensPaused(bool paused)
        {
            if (!m_hasBarrierTweens)
            {
                return;
            }

            for (int index = m_barrierPieceStart; index < m_activeCount; index++)
            {
                Tween.SetPausedAll(paused, m_active[index].transform);
            }
        }

        /// <summary>Teardown step 3: stops every barrier tween where it is.</summary>
        public void StopBarrierTweens()
        {
            if (!m_hasBarrierTweens)
            {
                return;
            }

            for (int index = m_barrierPieceStart; index < m_activeCount; index++)
            {
                // A scene unload can destroy a piece before its owner tears the view down.
                BoardPieceView piece = m_active[index];
                if (piece != null)
                {
                    Tween.StopAll(piece.transform);
                }
            }

            m_hasBarrierTweens = false;
        }

        /// <summary>
        /// Sinks one authored door's gate now and raises it holdSeconds from now; the board presenter
        /// passes the longest running exit through that door. False when the shown play area has no such
        /// door, so a presenter shown without its play area moves no gate. It allocates nothing.
        /// </summary>
        public bool TryOpenGate(int doorIndex, float holdSeconds)
        {
            if ((uint)doorIndex >= (uint)m_gateCount)
            {
                return false;
            }

            m_gates[doorIndex].Open(holdSeconds);
            return true;
        }

        /// <summary>The gate of one shown authored door; false when the shown play area has none.</summary>
        public bool TryGetGate(int doorIndex, out EnvironmentGate gate)
        {
            bool shown = (uint)doorIndex < (uint)m_gateCount;
            gate = shown ? m_gates[doorIndex] : null;
            return shown;
        }

        /// <summary>The gate of one shown authored door, for inspection.</summary>
        public EnvironmentGate GetGate(int doorIndex)
        {
            if ((uint)doorIndex >= (uint)m_gateCount)
            {
                throw new ArgumentOutOfRangeException(nameof(doorIndex), doorIndex, "No shown gate has this door index.");
            }

            return m_gates[doorIndex];
        }

        /// <summary>Suspend step 3 and Resume: pauses or continues every gate motion.</summary>
        public void SetGateTweensPaused(bool paused)
        {
            for (int index = 0; index < m_gateCount; index++)
            {
                m_gates[index].SetPaused(paused);
            }
        }

        /// <summary>Teardown step 3: stops every gate where it is.</summary>
        public void StopGateTweens()
        {
            for (int index = 0; index < m_gateCount; index++)
            {
                m_gates[index].Stop();
            }
        }

        /// <summary>Teardown step 6 and Restart: every gate back at its built height, unclipped and still.</summary>
        public void ResetGates()
        {
            for (int index = 0; index < m_gateCount; index++)
            {
                m_gates[index].Reset();
            }
        }

        /// <summary>Stops barrier and gate tweens, returns every piece to its pool and forgets the layout.</summary>
        public void Clear()
        {
            StopBarrierTweens();
            StopGateTweens();
            for (int index = 0; index < m_activeCount; index++)
            {
                if (index >= m_barrierPieceStart && m_active[index] != null)
                {
                    // Barriers share the wall pools, so a piece returns visible for its next use.
                    m_active[index].Renderer.enabled = true;
                }

                m_pools[m_activePools[index]].Release(m_active[index]);
                m_active[index] = null;
            }

            m_activeCount = 0;
            m_barrierPieceStart = 0;
            m_barriers.Clear();
            m_barrierIndices.Clear();
            m_gateCount = 0;
            m_layout = null;
            m_level = null;
        }

        /// <summary>Clears the play area and destroys the pooled pieces.</summary>
        public void Release()
        {
            Clear();
            DisposePools();
            m_composer = null;
            m_barrierComposer = null;
        }

        private void AddPieces(List<EnvironmentPiecePlacement> placements)
        {
            for (int index = 0; index < placements.Count; index++)
            {
                EnvironmentPiecePlacement placement = placements[index];
                int poolIndex = m_pieces.IndexOf(placement.Piece);
                BoardPieceView piece = m_pools[poolIndex].Get();
                piece.transform.SetLocalPositionAndRotation(
                    m_grid.CellToLocalInterpolated(placement.GridPosition), Quaternion.Euler(0f, placement.Yaw, 0f));
                piece.transform.localScale = placement.Scale;
                piece.SetColor(placement.Tint);

                m_active[m_activeCount] = piece;
                m_activePools[m_activeCount++] = poolIndex;
            }
        }

        private void ResetBarriers()
        {
            StopBarrierTweens();
            for (int index = 0; index < m_barriers.Count; index++)
            {
                MoveBarrier(m_barriers[index], m_barriers[index].StartsOpen, false);
            }
        }

        // Home only: one gate per authored door over the pieces just rented for its span. The play-area
        // placements fill m_active from index 0, so a span's placement range is its piece range.
        private void BindGates()
        {
            float closedHeight = m_grid.CellToLocalInterpolated(Vector3.zero).y;
            for (int index = 0; index < m_gateSpans.Count; index++)
            {
                if (m_gates.Count == index)
                {
                    m_gates.Add(new EnvironmentGate());
                }

                EnvironmentGateSpan span = m_gateSpans[index];
                EnvironmentGate gate = m_gates[index];
                gate.Bind(span.DoorIndex, m_boardRoot, closedHeight);
                for (int piece = 0; piece < span.PlacementCount; piece++)
                {
                    gate.Add(m_active[span.FirstPlacement + piece]);
                }
            }

            m_gateCount = m_gateSpans.Count;
        }

        private void MoveBarrier(EnvironmentBarrier barrier, bool open, bool animate)
        {
            float height = open ? m_barrierClosedHeight - k_barrierSinkDepth : m_barrierClosedHeight;
            int first = m_barrierPieceStart + barrier.FirstPiece;
            int end = first + barrier.PieceCount;
            for (int index = first; index < end; index++)
            {
                Transform piece = m_active[index].transform;
                if (m_hasBarrierTweens)
                {
                    // A flip during an unfinished one starts from where the piece is now.
                    Tween.StopAll(piece);
                }

                MeshRenderer pieceRenderer = m_active[index].Renderer;
                Vector3 position = piece.localPosition;
                if (!animate || position.y == height)
                {
                    position.y = height;
                    piece.localPosition = position;
                    pieceRenderer.enabled = !open;
                    continue;
                }

                // A rising piece shows from its first frame; a sinking one hides once under the floor.
                pieceRenderer.enabled = true;
                Tween tween = Tween.LocalPositionY(piece, height, k_barrierSeconds, Ease.InOutQuad);
                if (open)
                {
                    tween.OnComplete(pieceRenderer, s_hideRenderer);
                }

                m_hasBarrierTweens = true;
            }
        }

        private static void HideRenderer(MeshRenderer pieceRenderer)
        {
            pieceRenderer.enabled = false;
        }

        private bool HasPools(List<EnvironmentPiecePlacement> placements)
        {
            for (int index = 0; index < placements.Count; index++)
            {
                if (m_pieces.IndexOf(placements[index].Piece) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void DisposePools()
        {
            if (m_pools != null)
            {
                for (int index = 0; index < m_pools.Length; index++)
                {
                    m_pools[index].Dispose();
                }
            }

            m_pools = null;
        }

        private void CreatePools()
        {
            m_database.GetPieces(m_pieces);
            m_pools = new PrefabComponentPool<BoardPieceView>[m_pieces.Count];
            for (int index = 0; index < m_pieces.Count; index++)
            {
                BoardPieceView piece = m_pieces[index];
                m_pools[index] = new PrefabComponentPool<BoardPieceView>(
                    piece, m_boardRoot, 0, k_poolMaxSize, "Environment " + piece.name);
            }
        }

        private void OnDisable() => Clear();
        private void OnDestroy() => Release();
    }
}
