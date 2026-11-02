using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Environment;
using ColorBlockJam.Gameplay.Board;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>
    /// Shows the bound attempt's Barrier state on the play area (catalog OBJ-05, DEC-121): when the presenter places an
    /// attempt, Restart included, it snaps every barrier to the attempt's rule, and in the view-sync phase it runs one
    /// animated EnvironmentView.SetBarrierOpen for each flip the rule made since the last sync. It only reads the rule,
    /// and the domain never waits for it. Owner: the board presenter's effect view list. The tweens belong to the
    /// environment's barrier pieces: Suspend pauses them through SetPaused and Teardown stops them through StopTweens;
    /// the environment's own owner clears the pieces. It has no tick of its own and allocates nothing in Sync.
    /// </summary>
    public sealed class BarrierEffectView : IBlockEffectView
    {
        private readonly EnvironmentView m_environment;
        private BlockEffectContext m_context;
        private string[] m_ids = Array.Empty<string>();
        // Per barrier: the rule version and the open state this view last showed.
        private int[] m_shownVersions = Array.Empty<int>();
        private bool[] m_shownOpen = Array.Empty<bool>();
        private BarrierRule m_rule;
        private int m_generation;
        private bool m_disposed;

        /// <param name="environment">The scene's play-area view, which shows the barriers under their cell-object IDs.</param>
        public BarrierEffectView(EnvironmentView environment)
        {
            if (environment == null) throw new ArgumentNullException(nameof(environment));
            m_environment = environment;
        }

        public string TypeId => "barrier";

        /// <summary>
        /// The animated SetBarrierOpen calls Sync made since the last BindAttempt: exactly one per flip of each barrier.
        /// The snaps BindAttempt makes are not counted.
        /// </summary>
        public int AnimatedFlipCount { get; private set; }

        /// <summary>
        /// Takes the level's barrier IDs on Home. A level with barriers needs a play area that shows as many (the loader
        /// builds the environment from the same level first), so no Playing call names a barrier the play area lacks.
        /// </summary>
        public bool TryPrepare(BlockEffectContext context, IBlockEffectLevel metadata, out string error)
        {
            if (m_disposed) throw new ObjectDisposedException(nameof(BarrierEffectView));
            if (!(metadata is BarrierEffectLevel barriers) || context == null)
            {
                error = "Barrier view requires its typed level metadata.";
                return false;
            }

            if (ReferenceEquals(m_context, context))
            {
                error = null;
                return true;
            }

            if (barriers.BarrierCount > 0 && m_environment.BarrierCount != barriers.BarrierCount)
            {
                error = $"Level {context.Level.LevelId} has {barriers.BarrierCount} barriers but the play area shows " +
                        $"{m_environment.BarrierCount}; build the play area with this level before its attempt.";
                return false;
            }

            string[] ids = new string[barriers.BarrierCount];
            for (int barrier = 0; barrier < ids.Length; barrier++) ids[barrier] = barriers.GetId(barrier);
            m_rule = null;
            m_context = context;
            m_ids = ids;
            m_shownVersions = new int[ids.Length];
            m_shownOpen = new bool[ids.Length];
            error = null;
            return true;
        }

        /// <summary>Binds the new attempt's rule and snaps every barrier to it without animating (AC-5).</summary>
        public void BindAttempt(BlockEffectAttempt attempt, IBlockEffectState state)
        {
            if (attempt == null || !ReferenceEquals(attempt.Context, m_context) || attempt.Generation <= m_generation
                || !(state is BarrierEffectState barrier) || barrier.Rule.BarrierCount != m_ids.Length)
                throw new InvalidOperationException("Bind Barrier presentation to the newly published attempt for its prepared level.");
            m_generation = attempt.Generation;
            m_rule = barrier.Rule;
            AnimatedFlipCount = 0;
            for (int index = 0; index < m_ids.Length; index++)
            {
                bool open = m_rule.IsOpen(index);
                m_environment.SetBarrierOpen(m_ids[index], open, false);
                m_shownOpen[index] = open;
                m_shownVersions[index] = m_rule.GetVersion(index);
            }
        }

        // Barriers belong to the board, not to a block root.
        public void Attach(int block, Transform root) { }

        /// <summary>
        /// One animated call per flip since the last sync, in order; the last carries the rule's state. Each call starts
        /// the pieces from where they are, so several flips in one command phase (a Vacuum) end at the rule's state.
        /// </summary>
        public void Sync(IBoardReader board)
        {
            if (m_rule == null) return;
            for (int index = 0; index < m_ids.Length; index++)
            {
                int version = m_rule.GetVersion(index);
                int pending = version - m_shownVersions[index];
                if (pending <= 0) continue;
                bool open = m_shownOpen[index];
                for (int flip = 1; flip < pending; flip++)
                {
                    open = !open;
                    m_environment.SetBarrierOpen(m_ids[index], open, true);
                }

                open = m_rule.IsOpen(index);
                m_environment.SetBarrierOpen(m_ids[index], open, true);
                AnimatedFlipCount += pending;
                m_shownOpen[index] = open;
                m_shownVersions[index] = version;
            }
        }

        public void Reproject() { }

        public void SetClipPlane(int block, Vector4 plane, bool enabled) { }

        /// <summary>Suspend step 3 and Resume: pauses or continues the barrier tweens.</summary>
        public void SetPaused(bool paused)
        {
            if (!m_disposed) m_environment.SetBarrierTweensPaused(paused);
        }

        /// <summary>Teardown step 3: stops the barrier tweens where they are.</summary>
        public void StopTweens()
        {
            if (!m_disposed) m_environment.StopBarrierTweens();
        }

        public void Detach(int block) { }

        /// <summary>Teardown: the ended attempt's rule is never read again; the next BindAttempt snaps the barriers.</summary>
        public void ReturnAll()
        {
            m_rule = null;
        }

        // The environment's own owner clears its pieces; a scene unload may already have destroyed them.
        public void Dispose()
        {
            m_disposed = true;
            m_rule = null;
            m_context = null;
        }
    }
}
