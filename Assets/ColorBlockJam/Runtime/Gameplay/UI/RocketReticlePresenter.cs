using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Boosters;
using ColorBlockJam.Domain.Picking;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Runtime.Boosters;
using Onity.Reactive;

namespace ColorBlockJam.Gameplay.UI
{
    /// <summary>
    /// Places the Rocket crosshairs (B-24): while Rocket targeting runs, one reticle on every cell Rocket accepts on the
    /// committed board (the shared rule of StartingRocketCandidates, so a block's final cell gets none) and none
    /// otherwise. It places them again only on entering Rocket mode, on a board revision change and on a layer
    /// geometry change, through the gameplay projector, never per frame.
    /// </summary>
    /// <remarks>
    /// Owner: the gameplay HUD. Stop points: <see cref="Unbind"/> clears its subscriptions and hides every reticle;
    /// <see cref="Dispose"/> is the last one. The cell buffer is sized on Home, so a placement allocates nothing.
    /// </remarks>
    public sealed class RocketReticlePresenter : IDisposable
    {
        /// <summary>A reticle's width as a share of its cell's projected width (reference target screenshot).</summary>
        public const float k_cellFraction = 0.6f;

        private readonly LevelSession m_session;
        private readonly BoosterTargeting m_targeting;
        private readonly IBoardProjector m_projector;
        private readonly CompositeDisposable m_subscriptions = new CompositeDisposable();
        private readonly Observer<BoosterId> m_onModeChanged;
        private readonly Observer<int> m_onRevisionChanged;
        private readonly Action m_onGeometryChanged;

        private Cell[] m_cells = Array.Empty<Cell>();
        private IReticleView m_view;
        private bool m_disposed;

        public RocketReticlePresenter(LevelSession session, BoosterTargeting targeting, IBoardProjector projector)
        {
            m_session = session ?? throw new ArgumentNullException(nameof(session));
            m_targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
            m_projector = projector ?? throw new ArgumentNullException(nameof(projector));
            m_onModeChanged = OnModeChanged;
            m_onRevisionChanged = OnRevisionChanged;
            m_onGeometryChanged = Refresh;
        }

        public bool IsBound => m_view != null;

        /// <summary>How many reticles the last placement showed.</summary>
        public int ShownCount { get; private set; }

        public void Bind(IReticleView view)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(RocketReticlePresenter));
            }

            if (m_view != null)
            {
                throw new InvalidOperationException("Rocket reticle presenter: already bound; unbind first.");
            }

            m_view = view ?? throw new ArgumentNullException(nameof(view));
            m_view.GeometryChanged += m_onGeometryChanged;
            m_subscriptions.Add(m_targeting.Mode.Subscribe(m_onModeChanged));
            m_subscriptions.Add(m_session.BoardRevision.Subscribe(m_onRevisionChanged));
        }

        /// <summary>
        /// Sizes the cell buffer and the reticle pool on Home for a level's total cell count, which bounds every later
        /// board: Rocket only removes cells.
        /// </summary>
        public void Prepare(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (m_cells.Length < capacity)
            {
                m_cells = new Cell[capacity];
            }

            m_view?.Reserve(capacity);
        }

        /// <summary>Places the reticles for the current board and mode; hides them all outside Rocket targeting.</summary>
        public void Refresh()
        {
            if (m_view == null)
            {
                return;
            }

            LevelAttempt attempt = m_session.Attempt;
            if (m_targeting.Mode.Value != BoosterId.Rocket || attempt == null)
            {
                Hide();
                return;
            }

            IBoardReader board = attempt.Simulation.Board;

            // Only a level that skipped Prepare lands here; the buffer then grows once.
            if (m_cells.Length < board.Model.TotalCellCount)
            {
                Prepare(board.Model.TotalCellCount);
            }

            int count = StartingRocketCandidates.Collect(board, m_cells);
            IFootprintProjector footprint = m_projector.Footprint;
            for (int index = 0; index < count; index++)
            {
                Cell cell = m_cells[index];
                ScreenPoint center = footprint.Project(cell.X, cell.Y);
                ScreenPoint left = footprint.Project(cell.X - 0.5f, cell.Y);
                ScreenPoint right = footprint.Project(cell.X + 0.5f, cell.Y);
                float dx = right.X - left.X;
                float dy = right.Y - left.Y;
                m_view.Show(index, center, (float)Math.Sqrt(dx * dx + dy * dy) * k_cellFraction);
            }

            m_view.HideFrom(count);
            ShownCount = count;
        }

        public void Unbind()
        {
            if (m_view == null)
            {
                return;
            }

            m_subscriptions.Clear();
            Hide();
            m_view.GeometryChanged -= m_onGeometryChanged;
            m_view = null;
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            Unbind();
            m_subscriptions.Dispose();
            m_disposed = true;
        }

        private void Hide()
        {
            if (ShownCount == 0)
            {
                return;
            }

            m_view.HideFrom(0);
            ShownCount = 0;
        }

        private void OnModeChanged(BoosterId mode)
        {
            Refresh();
        }

        // A board change during Rocket targeting (a command source's move, the resumed parked move) moves the targets.
        private void OnRevisionChanged(int revision)
        {
            if (ShownCount > 0 || m_targeting.Mode.Value == BoosterId.Rocket)
            {
                Refresh();
            }
        }
    }
}
