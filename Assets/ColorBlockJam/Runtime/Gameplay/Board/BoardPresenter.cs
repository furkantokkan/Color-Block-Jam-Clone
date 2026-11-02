using System;
using System.Collections.Generic;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Environment;
using ColorBlockJam.Gameplay.Board.Outline;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Haptics;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Presentation.Boosters;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using HighlightPlus;
using PrimeTween;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Gameplay.Board
{
    /// <summary>
    /// Shows one attempt's blocks: it owns the level's BlockPieceFactory (the only place one is
    /// constructed at runtime), checks block roots out onto their cells under the environment's BoardRoot
    /// and hands each root to the registered block effect views (Arrow markers, Ice), follows the held block,
    /// settles a released block and moves an exiting block through its door behind a clip plane while
    /// its ExitFragments burst out of the door. It lowers the door's gate before a block leans into or
    /// crosses it and raises it after the last exit through it. It reads every block's runtime footprint,
    /// so a Rocket split recomposes the block's root and spawns a root per new part, and it keeps a
    /// removed block's root through its Hammer or Vacuum removal while its BoosterCasts play (B-19, B-20).
    /// Its Suspend and Teardown steps also pause and stop the Time Freeze frost's fade (B-25).
    /// It renders what the domain decided and never changes domain state.
    /// </summary>
    public sealed class BoardPresenter : IBoardPresenter, IBlockedDragFeedback, IDisposable
    {
        // One exit's fragment start: the target of its delayed start, so the callback allocates nothing.
        private sealed class ExitCue
        {
            public BoardPresenter Owner;
            public int Block;
            public Side Side;
            public Cell Origin;
            public float Seconds;
        }

        // Cached once: plays a cue's fragments once its gate is down. It reads the board Place received,
        // the same reader Sync passes (LevelSession places simulation.Preview); an exited block's
        // footprint stays readable there.
        private static readonly Action<ExitCue> s_playCue =
            cue => cue.Owner.PlayExitFragments(cue.Owner.m_board, cue.Block, cue.Side, cue.Origin, cue.Seconds);

        // One Rocket hit waiting for the nose's contact: the target of its delayed start, so it allocates nothing.
        private sealed class HitCue
        {
            public BoardPresenter Owner;
            public int Block;
        }

        // Cached once: shows the split the Rocket already made in the domain once its nose touches the cell.
        private static readonly Action<HitCue> s_applyHit = cue => cue.Owner.ApplyHit(cue.Block);

        // A shorter wait starts at once; PrimeTween rejects durations under 0.0001 s.
        private const float k_minWaitSeconds = 0.001f;

        private const int k_none = -1;
        private const string k_blocksRootName = "Blocks";

        // Below this squared local distance a released root already sits on its cell.
        private const float k_onCellSqrDistance = 1e-8f;

        // The drag view leads its cell by at most half a cell per axis (DEC-112), so a root further
        // than that from its exit cell was never shown there: a command the pointer did not drag moved it.
        private const float k_leadSqrDistance = 0.5f;

        // An exiting root drawn beside its cell slides into line for at least this long before it leaves (DEC-197).
        private const float k_alignSeconds = 0.08f;

        // Below this local offset a root already lines up with its exit cell and needs no slide.
        private const float k_alignEpsilon = 1e-4f;

        // The sideways punch of a refused, ungated block (B-12): a tenth of a cell, like the Ice view's punch in scale.
        private const float k_blockedPunchSeconds = 0.2f;
        private const float k_blockedPunchFrequency = 18f;
        private static readonly Vector3 s_blockedPunch = new Vector3(0.1f, 0f, 0f);

        private readonly EnvironmentView m_environment;
        private readonly BoardArt m_art;
        private readonly BlockEffectContextSource m_effectSource;
        private readonly BlockEffectViewContext m_effectViewContext;
        private readonly BoosterCatalog m_boosters;
        private readonly BoosterRemovalMotion m_removalMotion = new BoosterRemovalMotion();

        // The Time Freeze frost: the Suspend and Teardown steps pause and stop its fade with the attempt's other
        // presentation tweens (B-25). Null for a presenter built without it; the scene container owns and disposes it.
        private readonly TimeFreezeFrostView m_frost;
        private IBlockEffectView[] m_effects = Array.Empty<IBlockEffectView>();

        // Vibrates once per block leaving through a gate (DEC-156); null: nothing.
        private IHaptics m_haptics;

        // Where the booster casts enter the screen, above the HUD's booster bar; null: below the board's lower edge.
        private IBoosterCastAnchor m_castAnchor;


        private Transform m_blocksRoot;
        private BlockPieceFactory m_factory;

        // Owner: this presenter; its emitters wait under the Blocks root. Stops: Dispose. Created on the
        // first TryBuild and grown only on Home (DEC-131).
        private ExitFragments m_fragments;

        // Owner: this presenter; its views wait under BoardRoot. Stops: StopTweens and ReturnPieces at Teardown,
        // Dispose. Created and warmed on the first TryBuild when a booster catalog was injected (B-20).
        private BoosterCasts m_casts;
        private AttemptLevel m_level;
        private IBoardReader m_board;

        // Parallel per-block arrays, sized once per level on Home to the level's total cell count, the most
        // block IDs an attempt can issue (B-1); a null slot means no root is checked out. Slots are tested
        // with reference checks (is null) on purpose: transforms are cached so tween calls still reach a
        // root that a scene unload destroyed first, as PrimeTween matches targets by reference. Only the
        // Teardown paths (SnapHeld, the pool return) test for a destroyed root.
        private BlockPiece[] m_pieces = Array.Empty<BlockPiece>();
        private Transform[] m_transforms = Array.Empty<Transform>();
        private Tween[] m_exits = Array.Empty<Tween>();
        // Per block, sized with m_exits: the door its running exit uses, so a gate waits for every exit through it.
        private int[] m_exitDoors = Array.Empty<int>();
        // Per block: the delayed start of its exit fragments while its gate finishes sinking, and the
        // Home-allocated cue that delay targets, so the start allocates nothing and stops with the attempt.
        private Tween[] m_fragmentStarts = Array.Empty<Tween>();
        private ExitCue[] m_cues = Array.Empty<ExitCue>();
        // The tracked removal motion of a Hammer or Vacuum target's root; the return poll waits for it.
        private Tween[] m_removals = Array.Empty<Tween>();
        // Per block that lost a Rocket cell: the delayed start of its split until the Rocket's nose touches the cell,
        // and the Home-allocated cue that delay targets. m_hitOwners maps the parent and each new part to that parent
        // while the split waits, k_none otherwise; until then the parent's root shows its whole old footprint.
        private Tween[] m_hitStarts = Array.Empty<Tween>();
        private HitCue[] m_hitCues = Array.Empty<HitCue>();
        private int[] m_hitOwners = Array.Empty<int>();

        // One runtime footprint converted to view cells; allocated on Home with room for the largest block,
        // which no part outgrows. Factory, piece and effect calls only borrow it.
        private List<Vector2Int> m_cells = new List<Vector2Int>();
        // Per block, sized with the other arrays: the held-block outline on the checked-out root (DEC-145).
        // Prepared on Home in Place; SetHeld switches it on, and every end of a hold switches it off.
        private HighlightEffect[] m_highlights = Array.Empty<HighlightEffect>();
        private int m_held = k_none;

        private bool m_disposed;

        // Owner: the Game scene container (AsSingle). Stops: Dispose, when the scene container is disposed.
        public BoardPresenter(EnvironmentView environment, BoardArt art)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            if (art == null)
            {
                throw new ArgumentNullException(nameof(art));
            }

            m_environment = environment;
            m_art = art;
        }

        /// <summary>Block effects without booster casts, for boards whose tests need no cast.</summary>
        public BoardPresenter(EnvironmentView environment, BoardArt art,
            BlockEffectContextSource effects, BlockEffectViewContext viewContext) : this(environment, art)
        {
            m_effectSource = effects ?? throw new ArgumentNullException(nameof(effects));
            m_effectViewContext = viewContext ?? throw new ArgumentNullException(nameof(viewContext));
        }

        /// <summary>Block effects plus the Hammer, Rocket and Vacuum casts the catalog names, without the Time Freeze frost.</summary>
        public BoardPresenter(EnvironmentView environment, BoardArt art,
            BlockEffectContextSource effects, BlockEffectViewContext viewContext, BoosterCatalog boosters)
            : this(environment, art, effects, viewContext)
        {
            m_boosters = boosters != null ? boosters : throw new ArgumentNullException(nameof(boosters));
        }

        /// <summary>
        /// The Game scene's presenter: block effects, the booster casts and the Time Freeze frost, whose fade its
        /// Suspend and Teardown steps also pause and stop (B-25). The held-block outline needs no service: each
        /// root gets its Highlight Plus effect in Place (DEC-145).
        /// </summary>
        [Onity.DI.Inject]
        public BoardPresenter(EnvironmentView environment, BoardArt art, BlockEffectContextSource effects,
            BlockEffectViewContext viewContext, BoosterCatalog boosters, TimeFreezeFrostView frost)
            : this(environment, art, effects, viewContext, boosters)
        {
            m_frost = frost ?? throw new ArgumentNullException(nameof(frost));
        }

        public void ReprojectEffects()
        {
            for (int index = 0; index < m_effects.Length; index++) m_effects[index]?.Reproject();
        }

        /// <summary>
        /// Sets the haptics each block leaving through a gate plays (DEC-156); null plays nothing. The Game scene sets
        /// it once at scene build; the Vibration setting gate lives in <see cref="IHaptics"/>.
        /// </summary>
        public void SetHaptics(IHaptics haptics)
        {
            m_haptics = haptics;
        }

        /// <summary>
        /// Sets where the Hammer, Rocket and Vacuum casts enter the screen: the Game scene passes its HUD, so they rise
        /// from just above the booster bar. Null, or a HUD with no laid-out bar, keeps the below-board launch.
        /// </summary>
        public void SetCastAnchor(IBoosterCastAnchor anchor)
        {
            m_castAnchor = anchor;
        }

        /// <summary>
        /// Feedback for a refused press on a block: every effect view shows its own, and a block no drag gate holds,
        /// such as a one-cell block refused as a Rocket target, punches its root sideways once (B-12), so a refusal is
        /// never silent. A gated block gets no punch from this presenter: its gate's own effect view answers, and the
        /// Ice view punches the frozen block's root once, carrying the shell so its studs stay on the block's.
        /// </summary>
        public void ShowBlockedDrag(int block)
        {
            CompleteHit(block);
            for (int index = 0; index < m_effects.Length; index++)
                if (m_effects[index] is IBlockedDragFeedback feedback) feedback.ShowBlockedDrag(block);
            if (AllowsDrag(block)) PunchRoot(block);
        }

        // The attempt's own drag gates (Ice) tell whether an effect view already answers the refusal; without effect
        // hooks every block is ungated.
        private bool AllowsDrag(int block)
        {
            BlockEffectAttempt attempt = m_effectSource?.CurrentAttempt;
            return attempt == null || m_board == null || attempt.Hooks.AllowsDrag(m_board, block);
        }

        private void PunchRoot(int block)
        {
            if (block < 0 || block >= m_transforms.Length || m_board == null || block == m_held)
            {
                return;
            }

            Transform root = m_transforms[block];
            if (root is null || m_board.GetStatus(block) != BlockStatus.OnBoard)
            {
                return;
            }

            // The punch starts from the block's cell, so an unfinished settle cannot leave it off its cell.
            Tween.StopAll(root);
            root.localPosition = GetCellPosition(m_board.GetOrigin(block));

            // Owner: this presenter; target: the root Transform, so Suspend pauses it and Teardown's StopTweens stops
            // it. Stops: it ends by itself on the cell, or a later settle, exit or removal of the block stops it first.
            Tween.PunchLocalPosition(root, s_blockedPunch, k_blockedPunchSeconds, k_blockedPunchFrequency);
        }

        private bool TryPrepareEffects(AttemptLevel level, out string error)
        {
            if (m_effectSource == null) { error = null; return true; }
            BlockEffectContext context = m_effectSource.Context;
            if (context == null || !ReferenceEquals(context.Level, level))
            {
                error = "Prepare effect metadata for this exact level before building its views.";
                return false;
            }
            try
            {
                if (m_effects.Length == 0)
                {
                    m_effects = new IBlockEffectView[m_effectSource.Count];
                    for (int index = 0; index < m_effects.Length; index++)
                        m_effects[index] = m_effectSource.GetModule(index).CreateView(m_effectViewContext);
                }
                for (int index = 0; index < m_effects.Length; index++)
                {
                    IBlockEffectView view = m_effects[index];
                    if (view == null) continue;
                    if (view.TypeId != m_effectSource.GetModule(index).TypeId)
                        throw new InvalidOperationException("Effect view registration order does not match its module.");
                    if (!view.TryPrepare(context, context.GetAt(index), out error))
                    {
                        DisposeEffects();
                        return false;
                    }
                }
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                DisposeEffects();
                error = exception.Message;
                return false;
            }
        }

        private void DisposeEffects()
        {
            for (int index = 0; index < m_effects.Length; index++) m_effects[index]?.Dispose();
            m_effects = Array.Empty<IBlockEffectView>();
        }

        /// <summary>
        /// The time the longest running exit (its gate wait included), removal, booster cast or gate rise
        /// still needs; the session caps its Won Finish with it at 0.8 s (DEC-93, B-20).
        /// </summary>
        public float FinishSeconds
        {
            get
            {
                float longest = m_casts != null ? m_casts.RemainingSeconds : 0f;
                for (int block = 0; block < m_exits.Length; block++)
                {
                    longest = Math.Max(longest, GetRemaining(m_exits[block]));
                    longest = Math.Max(longest, GetRemaining(m_removals[block]));
                }

                // Each gate rises after its last exit, inside the same bounded Finish (at most 0.6 + 0.15 s).
                return Math.Max(longest, m_environment.GateSecondsRemaining);
            }
        }

        /// <summary>Block roots currently checked out of the factory.</summary>
        public int ActivePieceCount => m_factory != null ? m_factory.ActiveCount : 0;

        /// <summary>Block roots the factory instantiated for the current level; Restart keeps this number.</summary>
        public int PreparedPieceCount => m_factory != null ? m_factory.PreparedCount : 0;

        /// <summary>The root showing a block, or null when the block has none checked out.</summary>
        public BlockPiece GetPiece(int block) => m_pieces[block];

        /// <summary>The registered effect view with this type ID once a level was built, or null.</summary>
        public IBlockEffectView GetEffectView(string typeId)
        {
            for (int index = 0; index < m_effects.Length; index++)
            {
                if (m_effects[index] != null && m_effects[index].TypeId == typeId)
                {
                    return m_effects[index];
                }
            }

            return null;
        }

        /// <summary>Exit fragment bursts playing or waiting for their last fragment.</summary>
        public int ActiveExitFragmentCount => m_fragments != null ? m_fragments.ActiveCount : 0;

        /// <summary>Exit fragment emitters warmed so far; Restart keeps this number.</summary>
        public int PreparedExitFragmentCount => m_fragments != null ? m_fragments.PreparedCount : 0;

        /// <summary>Booster cast views playing or not yet returned by Sync.</summary>
        public int ActiveCastCount => m_casts != null ? m_casts.ActiveCount : 0;

        /// <summary>Booster cast views warmed so far; Restart and later levels keep this number.</summary>
        public int PreparedCastCount => m_casts != null ? m_casts.PreparedCount : 0;

        /// <summary>A warmed cast view (Hammer, Rocket, Vacuum, two each; BoosterCasts.GetPrepared), or null.</summary>
        public BoosterEffectView GetPreparedCast(int index) => m_casts?.GetPrepared(index);

        /// <summary>
        /// Prepares the one block root pool (DEC-193), sized to every block and the Rocket parts it can split
        /// into at once (FragmentCapacity) and to the level's largest footprint, and warms the shared tile pool
        /// with every placed block's tiles plus one Rocket remainder's spare (DEC-184), on Home, then lets
        /// every effect view prepare for the level (Arrow markers, Ice looks). Building the same level again (Restart) keeps
        /// the pools; a different level replaces the factory and the effect views' pools. The catalog's block
        /// root is checked first, so a missing or unusable root changes nothing and returns false with one
        /// error; an effect view's rejection (such as a missing Arrow prefab) names the block and leaves no
        /// level built.
        /// </summary>
        public bool TryBuild(AttemptLevel level, out string error)
        {
            ThrowIfDisposed();
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (ReferenceEquals(level, m_level))
            {
                error = null;
                return true;
            }

            if (!TryGetPoolNeeds(level, out int roots, out int largest, out int[] tiles, out error))
            {
                return false;
            }

            ReturnPieces();
            DisposeFactory();
            CreateBlocksRoot();
            if (!TryPrepareEffects(level, out error)) return false;

            // Owner: this presenter. Stops: the next TryBuild of another level or Dispose disposes it.
            BlockPieceFactory factory = new BlockPieceFactory(m_art, m_blocksRoot);
            try
            {
                factory.Prepare(roots, largest);
                factory.WarmTiles(tiles);

                // Emitters do not depend on the level; only the number a level may need grows (DEC-131).
                if (m_fragments == null)
                {
                    m_fragments = ExitFragments.Create(m_art, m_blocksRoot);
                }

                m_fragments.Prepare(level.BlockCount);

                // Casts do not depend on the level either; they are warmed once, on the first Home (B-20).
                if (m_boosters != null && m_casts == null)
                {
                    m_casts = new BoosterCasts(m_boosters, m_environment.BoardRoot);
                }

                m_casts?.Prepare();
            }
            catch
            {
                factory.Dispose();
                DisposeEffects();
                throw;
            }

            int capacity = level.Model.TotalCellCount;
            m_factory = factory;
            m_pieces = new BlockPiece[capacity];
            m_transforms = new Transform[capacity];
            m_exits = new Tween[capacity];
            m_exitDoors = new int[capacity];
            m_fragmentStarts = new Tween[capacity];
            m_cues = new ExitCue[capacity];
            for (int block = 0; block < m_cues.Length; block++)
            {
                m_cues[block] = new ExitCue { Owner = this, Block = block };
            }

            m_removals = new Tween[capacity];
            m_hitStarts = new Tween[capacity];
            m_hitCues = new HitCue[capacity];
            m_hitOwners = new int[capacity];
            for (int block = 0; block < capacity; block++)
            {
                m_hitCues[block] = new HitCue { Owner = this, Block = block };
                m_hitOwners[block] = k_none;
            }

            m_highlights = new HighlightEffect[capacity];
            m_cells = new List<Vector2Int>(largest);
            m_level = level;
            return true;
        }

        /// <summary>Checks out one root per on-board block and puts it on its cell (CellToLocal of its origin).</summary>
        public void Place(IBoardReader board)
        {
            ThrowIfDisposed();
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (m_level == null || !ReferenceEquals(board.Model, m_level.Model))
            {
                throw new InvalidOperationException("Build the board's level before placing its blocks.");
            }

            m_board = board;
            m_held = k_none;
            try
            {
                if (m_effectSource != null)
                {
                    BlockEffectAttempt attempt = m_effectSource.CurrentAttempt;
                    if (attempt == null || !ReferenceEquals(attempt.Context.Level, m_level))
                        throw new InvalidOperationException("Only completed effect state may be placed.");
                    for (int index = 0; index < m_effects.Length; index++)
                        m_effects[index]?.BindAttempt(attempt, attempt.GetAt(index));
                }
                for (int block = 0; block < board.BlockCount; block++)
                {
                    if (board.GetStatus(block) != BlockStatus.OnBoard)
                    {
                        continue;
                    }

                    if (m_pieces[block] is not null)
                    {
                        throw new InvalidOperationException("Return the placed blocks before placing them again.");
                    }

                    BlockFootprint footprint = board.GetFootprint(block);
                    BlockVisualSpec visual = m_level.GetVisual(footprint.Source);
                    CopyCells(footprint);
                    BlockPiece piece = m_factory.Create(new BlockPieceSpawnData(
                        visual.Color, m_cells, GetCellPosition(board.GetOrigin(block))));
                    m_pieces[block] = piece;
                    m_transforms[block] = piece.transform;
                    m_highlights[block] = HeldBlockHighlight.Prepare(piece.gameObject);
                    AttachEffects(block, footprint.Source);
                }
            }
            catch { ReturnPieces(); throw; }
        }

        /// <summary>
        /// The view sync phase (design E): starts the exit motion of every block that left this tick,
        /// settles blocks that a command moved without the pointer holding them, recomposes a block a
        /// Rocket split and spawns its new parts, starts the removal of Hammer and Vacuum targets with
        /// their casts, returns blocks whose exit or removal finished and puts the held block on its
        /// preview cell plus its follow lead.
        /// </summary>
        public void Sync(BoardSimulation simulation, BoardPoint pointer)
        {
            IBoardReader board = simulation.Preview;
            for (int index = 0; index < m_effects.Length; index++) m_effects[index]?.Sync(board);
            m_removalMotion.BeginSync();
            BoardEventLog events = simulation.Events;
            for (int i = 0; i < events.Count; i++)
            {
                ref readonly BoardEvent boardEvent = ref events.GetAt(i);
                // A new part of the split this Sync started joins that waiting hit; any other event on a block
                // still waiting for its Rocket shows the split first, so every motion starts from its real cells.
                if (boardEvent.Kind != BoardEventKind.FootprintChanged || boardEvent.Block == boardEvent.Parent)
                {
                    CompleteHit(boardEvent.Block);
                }

                if (boardEvent.Kind == BoardEventKind.Exited)
                {
                    // A Stay outcome keeps the block on the board, so only a leaving block moves out.
                    if (boardEvent.Outcome == ExitOutcome.Leave)
                    {
                        PlaceOnExitCell(boardEvent.Block, boardEvent.To);
                        StartExit(board, boardEvent.Block, boardEvent.Side, boardEvent.To, boardEvent.Door);
                        m_haptics?.Play(HapticFeedback.GateExit);
                    }
                    else if (boardEvent.Block != m_held)
                    {
                        Settle(boardEvent.Block);
                    }
                }
                else if (boardEvent.Kind == BoardEventKind.MoveCommitted && boardEvent.Block != m_held)
                {
                    // Every move settles here, including one a replay or another command source made
                    // right after the pointer released the same block. A release already settling onto
                    // this cell restarts the same tween from the same place, so nothing visibly changes.
                    Settle(boardEvent.Block);
                }
                else if (boardEvent.Kind == BoardEventKind.FootprintChanged)
                {
                    // A Rocket split: the block that lost the cell comes first, then each new part (B-5). Both wait
                    // for the Rocket's nose to touch the cell (B-20).
                    if (boardEvent.Block == boardEvent.Parent)
                    {
                        StartHit(board, boardEvent.Block, boardEvent.From);
                    }
                    else if (m_hitOwners[boardEvent.Parent] == boardEvent.Parent)
                    {
                        m_hitOwners[boardEvent.Block] = boardEvent.Parent;
                    }
                    else
                    {
                        SpawnPart(board, boardEvent.Block);
                    }
                }
                else if (boardEvent.Kind == BoardEventKind.Removed)
                {
                    StartRemoval(board, boardEvent.Block, boardEvent.Cause, boardEvent.Parent);
                }
            }

            for (int block = 0; block < board.BlockCount; block++)
            {
                // Completion is polled here, so no tween callback exists to outlive the attempt.
                if (m_pieces[block] is not null && board.GetStatus(block) != BlockStatus.OnBoard
                    && !m_exits[block].isAlive && !m_removals[block].isAlive)
                {
                    ReturnPiece(block);
                }
            }

            // Fragment bursts and casts are polled the same way; each returns once it ends.
            m_fragments?.Sync();
            m_casts?.Sync();

            DragResolver drag = simulation.Drag;
            if (!drag.IsActive || drag.IsConsumed || m_pieces[drag.Block] is null)
            {
                return;
            }

            // Board point offsets are cell units and a cell spans one local unit (IBoardLayout). The lead
            // never enters a door: touching a fitting door exits at once (DEC-164), and the exit lowers the gate.
            Cell origin = board.GetOrigin(drag.Block);
            BoardPoint lead = drag.GetViewOffset(pointer);
            m_transforms[drag.Block].localPosition =
                GetCellPosition(origin) + new Vector3(lead.X, 0f, lead.Y);

        }

        /// <summary>Marks the held block; the previously held block, if still on the board, settles onto its cell.</summary>
        public void SetHeld(int block)
        {
            if (block == m_held)
            {
                return;
            }

            // A block still waiting for its Rocket shows its split first, so the held root is its own.
            CompleteHit(block);
            int previous = m_held;
            m_held = block;
            if (block != k_none && m_pieces[block] is not null)
            {
                // The drag follow owns the held root now, so an unfinished settle stops.
                Tween.StopAll(m_transforms[block]);
                // The held block's white outline (DEC-89, DEC-145).
                SetHighlight(block, true);
            }

            if (previous != k_none)
            {
                SetHighlight(previous, false);
                Settle(previous);
            }
        }

        /// <summary>Writes the held block straight onto its cell, with no tween, for Suspend and Teardown.</summary>
        public void SnapHeld()
        {
            int block = m_held;
            m_held = k_none;
            SetHighlight(block, false);
            if (block == k_none)
            {
                return;
            }

            // Teardown can run after a scene unload destroyed the root, so this tests for a destroyed
            // root too. An exited block keeps its lead so its exit motion starts where it was last shown.
            Transform root = m_transforms[block];
            if (root == null || m_board.GetStatus(block) != BlockStatus.OnBoard)
            {
                return;
            }

            root.localPosition = GetCellPosition(m_board.GetOrigin(block));
        }

        /// <summary>
        /// Suspend step 3 and Resume: pauses or continues every tween on a checked-out root, including a
        /// removal, every delayed fragment start, every gate, every booster cast and the Time Freeze frost's fade.
        /// </summary>
        public void SetTweensPaused(bool paused)
        {
            for (int index = 0; index < m_effects.Length; index++) m_effects[index]?.SetPaused(paused);
            m_fragments?.SetPaused(paused);
            m_casts?.SetPaused(paused);
            m_frost?.SetPaused(paused);
            m_environment.SetGateTweensPaused(paused);
            for (int block = 0; block < m_transforms.Length; block++)
            {
                if (m_transforms[block] is not null)
                {
                    Tween.SetPausedAll(paused, m_transforms[block]);
                }

                if (m_fragmentStarts[block].isAlive)
                {
                    m_fragmentStarts[block].isPaused = paused;
                }

                if (m_hitStarts[block].isAlive)
                {
                    m_hitStarts[block].isPaused = paused;
                }
            }
        }

        /// <summary>
        /// Teardown step 3: stops every settle, exit and removal tween on a checked-out root, every delayed
        /// fragment start, every gate, every cast, and the Time Freeze frost, which it clears at once.
        /// </summary>
        public void StopTweens()
        {
            for (int index = 0; index < m_effects.Length; index++) m_effects[index]?.StopTweens();
            m_fragments?.Stop();
            m_casts?.Stop();
            m_frost?.Stop();
            m_environment.StopGateTweens();
            for (int block = 0; block < m_transforms.Length; block++)
            {
                if (m_transforms[block] is not null)
                {
                    Tween.StopAll(m_transforms[block]);
                }

                if (m_fragmentStarts[block].isAlive)
                {
                    m_fragmentStarts[block].Stop();
                }
            }

            // A split still waiting for its Rocket is dropped; Teardown returns its parent's root next.
            DropHits();
        }

        /// <summary>
        /// Shows every Rocket split still waiting for the Rocket's nose at once, as its contact would; for a caller
        /// that must read the runtime footprints on the roots without waiting for the casts.
        /// </summary>
        public void CompleteRocketHits()
        {
            for (int block = 0; block < m_hitOwners.Length; block++)
            {
                if (m_hitOwners[block] == block)
                {
                    CompleteHit(block);
                }
            }
        }

        /// <summary>
        /// Teardown step 6: returns every checked-out root and cast. The pool release resets its tiles' clip
        /// plane, colour and root scale (BlockPiece.OnPoolRelease), so an interrupted exit or removal leaves
        /// nothing behind, and every gate returns to its built height.
        /// </summary>
        public void ReturnPieces()
        {
            for (int index = 0; index < m_effects.Length; index++) m_effects[index]?.ReturnAll();
            m_fragments?.ReturnAll();
            m_casts?.ReturnAll();
            m_environment.ResetGates();
            DropHits();
            m_held = k_none;
            // Before the roots go back, so no outline rides into a pool switched on.
            for (int block = 0; block < m_highlights.Length; block++)
            {
                SetHighlight(block, false);
            }

            for (int block = 0; block < m_pieces.Length; block++)
            {
                if (m_pieces[block] is not null)
                {
                    ReturnPiece(block);
                }
            }
        }

        /// <summary>
        /// Returns every root and disposes the factory and the Blocks root. When a scene unload has
        /// already destroyed the roots, it only drops the references (design D.3).
        /// </summary>
        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            ReturnPieces();
            DisposeEffects();
            DisposeFactory();
            m_fragments?.Dispose();
            m_fragments = null;
            m_casts?.Dispose();
            m_casts = null;
            if (m_blocksRoot != null)
            {
                Object.Destroy(m_blocksRoot.gameObject);
            }

            m_blocksRoot = null;
            m_board = null;
        }

        // Switches a block root's outline; a block without a checked-out root, or a root a scene unload
        // destroyed, is skipped. A split part, or a root a Rocket recomposed, is prepared (its tiles re-read) the
        // first time it is switched on again.
        private void SetHighlight(int block, bool highlighted)
        {
            if (block < 0 || block >= m_highlights.Length)
            {
                return;
            }

            HighlightEffect effect = m_highlights[block];
            if (effect is null && highlighted && m_transforms[block] != null)
            {
                effect = m_highlights[block] = HeldBlockHighlight.Prepare(m_transforms[block].gameObject);
            }

            if (effect != null)
            {
                effect.SetHighlighted(highlighted);
            }
        }

        // Checks the catalog's block root, then sizes the one root pool (DEC-193): roots is every block's own root
        // plus the Rocket parts it can split into at once, and largest is the largest footprint, which no part
        // outgrows. tiles is the shared tile budget: every block's own tiles plus the largest extra any one block
        // needs after Rocket removals and splits (BlockPieceCapacity.AddTileBudget). A later need beyond it grows
        // the shared pool instead of being refused (DEC-184).
        private bool TryGetPoolNeeds(AttemptLevel level, out int roots, out int largest, out int[] tiles, out string error)
        {
            roots = 0;
            largest = 0;
            tiles = new int[BlockPieceCapacity.KindCount];
            if (!BlockPieceFactory.TryGetRoot(m_art.Catalog, out _, out error))
            {
                return false;
            }

            var spare = new int[BlockPieceCapacity.KindCount];
            for (int block = 0; block < level.BlockCount; block++)
            {
                BlockVisualSpec visual = level.GetVisual(block);
                roots += FragmentCapacity.GetMaxFragments(visual.Cells);
                largest = Math.Max(largest, visual.Cells.Count);
                BlockPieceCapacity.AddTileBudget(visual.Cells, tiles, spare);
            }

            for (int kind = 0; kind < tiles.Length; kind++) tiles[kind] += spare[kind];
            return true;
        }

        private void CreateBlocksRoot()
        {
            if (m_blocksRoot != null)
            {
                return;
            }

            // Under BoardRoot at local zero, so block roots share the environment's cell frame.
            m_blocksRoot = new GameObject(k_blocksRootName).transform;
            m_blocksRoot.SetParent(m_environment.BoardRoot, false);
        }

        // A replay or bot exit moved the block to its exit cell in the same command, so the root still
        // stands where the block started or is still settling; it leaves from the exit cell, as a
        // pushed-out block does. A pushed-out root stays within its follow lead and has no tween,
        // because holding a block stops its tweens.
        private void PlaceOnExitCell(int block, Cell origin)
        {
            Transform root = m_transforms[block];
            if (root is null)
            {
                return;
            }

            Vector3 cell = GetCellPosition(origin);
            if ((root.localPosition - cell).sqrMagnitude > k_leadSqrDistance || Tween.GetTweensCount(root) > 0)
            {
                Tween.StopAll(root);
                root.localPosition = cell;
            }
        }

        private void StartExit(IBoardReader board, int block, Side side, Cell origin, int door)
        {
            Transform root = m_transforms[block];
            if (root is null)
            {
                return;
            }

            Tween.StopAll(root);
            // A pushed-out block leaves the hold here; the outline does not follow it through the door.
            SetHighlight(block, false);

            // Effect presentation on the root (an Arrow marker, an Ice shell) clips with its block.
            Vector4 clipPlane = GetClipPlane(side);
            m_pieces[block].View.SetClipPlane(clipPlane, true);
            for (int index = 0; index < m_effects.Length; index++) m_effects[index]?.SetClipPlane(block, clipPlane, true);

            // depth + 1 cells carries the innermost cell past the clip line, 0.25 cell beyond the edge. The
            // runtime footprint gives a Rocket part or a block that lost a cell its own depth (B-19).
            int travel = board.GetFootprint(block).Depth(side) + 1;
            Cell outward = SideMath.GetOutward(side);
            Vector3 end = GetCellPosition(origin) + new Vector3(outward.X, 0f, outward.Y) * travel;

            float seconds = Mathf.Clamp(ExitMotionSettings.k_secondsPerCell * travel,
                ExitMotionSettings.k_minSeconds, ExitMotionSettings.k_maxSeconds);

            // The block waits, inside its unchanged exit time, until its gate is fully down, so it never
            // crosses a raised gate. The domain already committed the exit (DEC-113); only the view waits.
            float wait = GetGateSinkSeconds(door);

            // Straight out through the gate (DEC-164): the outward axis moves alone after the wait. A held block
            // drawn beside its cell (DEC-197) first slides into line on the other axis, during the gate's sink
            // (at least k_alignSeconds), so it never cuts across the wall. Owner: this presenter; target: the
            // root Transform. Stops: Sync returns the root once the outward tween (its wait included) ends; Suspend
            // pauses both; Teardown's StopTweens stops both.
            Vector3 start = root.localPosition;
            float align = Math.Max(wait, k_alignSeconds);
            if (outward.X == 0)
            {
                if (Math.Abs(start.x - end.x) > k_alignEpsilon) Tween.LocalPositionX(root, end.x, align, Ease.OutQuad);
                m_exits[block] = Tween.LocalPositionZ(root, end.z, seconds - wait, Ease.InQuad, startDelay: wait);
            }
            else
            {
                if (Math.Abs(start.z - end.z) > k_alignEpsilon) Tween.LocalPositionZ(root, end.z, align, Ease.OutQuad);
                m_exits[block] = Tween.LocalPositionX(root, end.x, seconds - wait, Ease.InQuad, startDelay: wait);
            }
            m_exitDoors[block] = door;
            OpenGate(door);
            StartExitFragments(board, block, side, origin, wait, seconds - wait);
        }

        // The exit's gate stays down until the longest running exit through the same door ends, then
        // rises, so a shorter, later exit never raises it under an earlier block. Owner: the
        // EnvironmentView gate; Suspend pauses it and Teardown stops and resets it. A play area shown
        // without this door (a presenter without its environment built) has no gate.
        private void OpenGate(int door)
        {
            m_environment.TryOpenGate(door, GetRunningExitSeconds(door));
        }

        // The longest time a running exit through this door still needs, its wait included; 0 for none.
        private float GetRunningExitSeconds(int door)
        {
            float longest = 0f;
            for (int block = 0; block < m_exits.Length; block++)
            {
                Tween exit = m_exits[block];
                if (exit.isAlive && m_exitDoors[block] == door)
                {
                    longest = Math.Max(longest, exit.durationTotal - exit.elapsedTimeTotal);
                }
            }

            return longest;
        }

        // Fragments leave the door line once the block starts through it, so they wait for the gate as
        // well. Owner: this presenter; the delayed start targets the block's Home-allocated cue. Stops:
        // Suspend pauses it; Teardown's StopTweens or the root's return stops it.
        private void StartExitFragments(IBoardReader board, int block, Side side, Cell origin, float wait, float seconds)
        {
            if (wait < k_minWaitSeconds)
            {
                PlayExitFragments(board, block, side, origin, seconds);
                return;
            }

            ExitCue cue = m_cues[block];
            cue.Side = side;
            cue.Origin = origin;
            cue.Seconds = seconds;
            m_fragmentStarts[block] = Tween.Delay(cue, wait, s_playCue);
        }

        // How long the door's gate still needs to reach the bottom; 0 without a shown gate.
        private float GetGateSinkSeconds(int door)
        {
            return m_environment.TryGetGate(door, out EnvironmentGate gate) ? gate.SinkSecondsRemaining : 0f;
        }

        // Same-colour cube fragments leave the door line for as long as the block slides through it
        // (DEC-90, DEC-131). A full pool skips them; the exit itself never depends on the effect.
        private void PlayExitFragments(IBoardReader board, int block, Side side, Cell origin, float seconds)
        {
            if (m_fragments == null)
            {
                return;
            }

            ExitFragmentPlacement placement = ExitFragmentPlacement.Create(board, block, side, origin);
            BlockVisualSpec visual = m_level.GetVisual(board.GetFootprint(block).Source);
            m_fragments.TryPlay(placement.LocalPosition, placement.LocalRotation, placement.Lanes, seconds,
                m_art.GetAtlasColor(visual.Color));
        }

        // The Rocket flies to the removed cell; the block keeps showing its whole old footprint until the nose
        // touches the cell, then breaks (ApplyHit). Without a free cast it breaks at once. Owner: this presenter;
        // target: the Home-allocated cue. Stops: Suspend pauses it; a later event on its blocks, SetHeld or
        // CompleteRocketHits applies it early; Teardown's StopTweens and ReturnPieces drop it.
        private void StartHit(IBoardReader board, int block, Cell removedCell)
        {
            float contact = PlayRocket(removedCell, GetBlockColor(board, block));
            if (contact < k_minWaitSeconds)
            {
                Recompose(board, block);
                return;
            }

            m_hitOwners[block] = block;
            m_hitStarts[block] = Tween.Delay(m_hitCues[block], contact, s_applyHit);
        }

        // Shows at once the waiting split that block belongs to, as parent or new part; nothing when none waits.
        private void CompleteHit(int block)
        {
            if (block < 0 || block >= m_hitOwners.Length || m_hitOwners[block] == k_none)
            {
                return;
            }

            int parent = m_hitOwners[block];
            if (m_hitStarts[parent].isAlive)
            {
                m_hitStarts[parent].Stop();
            }

            ApplyHit(parent);
        }

        // The Rocket's nose touched the cell: the parent is recomposed over its remaining cells and each new part
        // gets its root, read from the current board, so a part that already left it gets none. The parent goes
        // first (B-5), so the tiles it no longer shows are back in the shared pool before the parts take theirs and
        // one block's split never needs more than its Home budget (DEC-184).
        private void ApplyHit(int parent)
        {
            if (m_hitOwners[parent] != parent || m_board == null)
            {
                return;
            }

            m_hitStarts[parent] = default;
            Recompose(m_board, parent);
            for (int block = 0; block < m_hitOwners.Length; block++)
            {
                if (m_hitOwners[block] != parent)
                {
                    continue;
                }

                m_hitOwners[block] = k_none;
                if (block != parent && block < m_board.BlockCount && m_board.GetStatus(block) == BlockStatus.OnBoard)
                {
                    SpawnPart(m_board, block);
                }
            }
        }

        // Teardown: every waiting split is dropped without being shown.
        private void DropHits()
        {
            for (int block = 0; block < m_hitOwners.Length; block++)
            {
                if (m_hitStarts[block].isAlive)
                {
                    m_hitStarts[block].Stop();
                }

                m_hitStarts[block] = default;
                m_hitOwners[block] = k_none;
            }
        }

        // The block that lost a Rocket cell keeps its root, recomposed over its remaining cells (B-20), with its
        // footprint-bound effects (Arrow marker, Ice shell) following.
        private void Recompose(IBoardReader board, int block)
        {
            BlockPiece piece = m_pieces[block];
            if (piece is null)
            {
                return;
            }

            CopyCells(board.GetFootprint(block));
            if (!piece.TrySetCells(m_cells))
            {
                // The shared tile pool grows on demand (DEC-184), so only a footprint the domain never produces lands here.
                Debug.LogError("A Rocket part's root could not show its footprint; it is larger than the root's cell capacity or not connected.");
                return;
            }

            for (int index = 0; index < m_effects.Length; index++)
                if (m_effects[index] is IBlockFootprintView view) view.SetFootprint(block, m_cells);

            // The outline's renderer list is stale now; the next switch-on re-reads the remaining tiles (DEC-145).
            SetHighlight(block, false);
            m_highlights[block] = null;
        }

        // A new Rocket part: a root from the block root pool, warmed on Home for it (DEC-193), at the parent's
        // origin with the part's own cells, so no world cell moves (B-4, B-19).
        private void SpawnPart(IBoardReader board, int block)
        {
            if (m_pieces[block] is not null)
            {
                return;
            }

            BlockFootprint footprint = board.GetFootprint(block);
            if (footprint.CellCount == 0 || footprint.Source < 0)
            {
                return;
            }

            if (!m_factory.CanCreate())
            {
                Debug.LogError("A Rocket part has no warmed root left in the block root pool.");
                return;
            }

            BlockVisualSpec visual = m_level.GetVisual(footprint.Source);
            CopyCells(footprint);
            BlockPiece piece;
            try
            {
                piece = m_factory.Create(new BlockPieceSpawnData(
                    visual.Color, m_cells, GetCellPosition(board.GetOrigin(block))));
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
            {
                // Create returns the root to its pool first. As in Recompose, a root that cannot show the part's
                // cells is a Home sizing bug; the part stays unshown instead of throwing out of the Playing tick.
                Debug.LogError("A Rocket part's root could not show its footprint: " + exception.Message);
                return;
            }

            m_pieces[block] = piece;
            m_transforms[block] = piece.transform;
            // A split part gets its outline only when it is first held or vacuumed (SetHighlight), so the split
            // itself allocates nothing for it.
            m_highlights[block] = null;
            AttachEffects(block, footprint.Source);
        }

        // A Hammer or Vacuum target keeps its root until its removal motion ends; the return poll waits for
        // the tracked tween (B-20). Every removal tween targets the root, so SetTweensPaused and StopTweens
        // already cover it.
        private void StartRemoval(IBoardReader board, int block, CommandKind cause, int source)
        {
            Transform root = m_transforms[block];
            if (root is null)
            {
                return;
            }

            Tween.StopAll(root);
            BoosterCastPlacement placement = GetCastPlacement();
            if (cause == CommandKind.RemoveBlock)
            {
                m_removals[block] = m_casts != null
                    ? m_removalMotion.PlayHammer(m_casts, root, GetBlockTop(board, block), GetBlockColor(board, block), placement)
                    : BoosterRemovalMotion.Shrink(root);
            }
            else if (cause == CommandKind.RemoveColor)
            {
                // Every Vacuum target shows the white outline until its piece is gone (ReturnPiece).
                SetHighlight(block, true);
                m_removals[block] = m_removalMotion.PlayVacuum(m_casts, root, source, placement);
            }
            else
            {
                m_removals[block] = BoosterRemovalMotion.Shrink(root);
            }
        }

        // The Rocket leaves from above the booster bar and its nose reaches the removed cell's top, where
        // it shatters in the block's colour. Returns the seconds until the nose touches it; 0 without a cast.
        private float PlayRocket(Cell cell, Color color)
        {
            if (m_casts == null)
            {
                return 0f;
            }

            Transform boardRoot = m_environment.BoardRoot;
            Vector3 destination = boardRoot.TransformPoint(GetCellPosition(cell) + Vector3.up * ExitMotionSettings.k_blockTopHeight);
            return m_casts.TryPlayRocket(GetCastPlacement().GetLaunch(), boardRoot.rotation, destination, color, out float contact)
                ? contact
                : 0f;
        }

        // The block's authored tint, as its pieces show it (DEC-123); the cast particles take it as start colour.
        private Color GetBlockColor(IBoardReader board, int block)
        {
            return m_art.GetColor(m_level.GetVisual(board.GetFootprint(block).Source).Color);
        }

        // The top centre of the block's footprint box on its logical cell, where the Hammer's face lands.
        private Vector3 GetBlockTop(IBoardReader board, int block)
        {
            BlockFootprint footprint = board.GetFootprint(block);
            Vector3 local = GetCellPosition(board.GetOrigin(block)) + new Vector3((footprint.MinX + footprint.MaxX) * 0.5f,
                ExitMotionSettings.k_blockTopHeight, (footprint.MinY + footprint.MaxY) * 0.5f);
            return m_environment.BoardRoot.TransformPoint(local);
        }

        private BoosterCastPlacement GetCastPlacement()
        {
            return new BoosterCastPlacement(m_environment.BoardRoot, m_level.Layout, m_effectViewContext?.Camera, m_castAnchor);
        }

        private void Settle(int block)
        {
            Transform root = m_transforms[block];
            if (root is null || m_board.GetStatus(block) != BlockStatus.OnBoard)
            {
                return;
            }

            Tween.StopAll(root);

            // A tap or a blocked push releases the root exactly on its cell. A tween to its own position
            // would make PrimeTween log an allocated warning in a Playing tick, so it is written instead.
            Vector3 cell = GetCellPosition(m_board.GetOrigin(block));
            if ((root.localPosition - cell).sqrMagnitude < k_onCellSqrDistance)
            {
                root.localPosition = cell;
                return;
            }

            // Owner: this presenter; target: the root Transform. Stops: it ends by itself, or the next
            // SetHeld of this block, Suspend (pause) or Teardown's StopTweens stops it.
            Tween.LocalPosition(root, cell, ExitMotionSettings.k_settleSeconds, Ease.OutQuad);
        }

        // The door line in world space as a plane whose normal points into the board. BoardLit keeps
        // fragments with dot(plane, position) >= 0, so the part of the block beyond the door disappears.
        private Vector4 GetClipPlane(Side side)
        {
            Cell outward = SideMath.GetOutward(side);

            // The exit fragments start on this same line, so they appear where the block disappears.
            float line = ExitFragmentPlacement.GetDoorLine(m_level.Model, side);
            Vector3 lineLocal = SideMath.RunsAlongX(side) ? new Vector3(0f, 0f, line) : new Vector3(line, 0f, 0f);
            Transform boardRoot = m_environment.BoardRoot;
            Vector3 point = boardRoot.TransformPoint(lineLocal);
            Vector3 normal = boardRoot.TransformDirection(new Vector3(-outward.X, 0f, -outward.Y));
            return new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));
        }

        private Vector3 GetCellPosition(Cell cell) => m_level.Layout.CellToLocal(new Vector2Int(cell.X, cell.Y));

        // Converts a runtime footprint into the presenter's view-cell buffer, relative to the block's origin.
        private void CopyCells(in BlockFootprint footprint)
        {
            m_cells.Clear();
            for (int i = 0; i < footprint.CellCount; i++)
            {
                Cell cell = footprint.GetCell(i);
                m_cells.Add(new Vector2Int(cell.X, cell.Y));
            }
        }

        // An authored block gets Attach; a Rocket part gets AttachPart from footprint-bound views, with its
        // cells in m_cells, and Attach from the others.
        private void AttachEffects(int block, int source)
        {
            bool part = block >= m_level.BlockCount;
            for (int index = 0; index < m_effects.Length; index++)
            {
                IBlockEffectView view = m_effects[index];
                if (view == null)
                {
                    continue;
                }

                if (part && view is IBlockFootprintView footprintView)
                {
                    footprintView.AttachPart(block, source, m_transforms[block], m_cells);
                }
                else
                {
                    view.Attach(block, m_transforms[block]);
                }
            }
        }

        private static float GetRemaining(Tween tween)
        {
            return tween.isAlive ? tween.durationTotal - tween.elapsedTimeTotal : 0f;
        }

        // Every path that ends a root (exit or removal in Sync, Teardown, Restart, a level change and
        // Dispose) comes here, so the block's effect presentation (an Arrow marker, an Ice shell) returns
        // first and none rides a pooled root.
        private void ReturnPiece(int block)
        {
            // No root goes back to its pool outlined: a held or Vacuum target outline ends here.
            SetHighlight(block, false);
            for (int index = 0; index < m_effects.Length; index++) m_effects[index]?.Detach(block);
            BlockPiece piece = m_pieces[block];
            m_pieces[block] = null;
            m_transforms[block] = null;
            m_exits[block] = default;
            m_removals[block] = default;
            if (m_fragmentStarts[block].isAlive)
            {
                m_fragmentStarts[block].Stop();
            }

            if (m_factory != null && piece != null)
            {
                m_factory.Return(piece);
            }
        }

        private void DisposeFactory()
        {
            BlockPieceFactory factory = m_factory;
            m_factory = null;
            m_level = null;
            if (factory != null && m_blocksRoot != null)
            {
                factory.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(BoardPresenter));
            }
        }
    }
}
