using System;
using ColorBlockJam.Presentation.Boosters;
using ColorBlockJam.Runtime.Boosters;
using Onity.Pooling;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Board
{
    /// <summary>
    /// Owns the board's pooled booster casts (B-20): two warmed instances each of the Hammer, Rocket and Vacuum effect
    /// prefabs that the booster definitions name, waiting under the board root. The board presenter starts a cast when
    /// the command's board event reaches its view sync phase and forwards Suspend, Resume, Teardown and disposal to it
    /// (the ExitFragments pattern, DEC-131). A cast whose two instances are busy is skipped: the domain has already
    /// applied the command and never waits for a visual. Owner: the board presenter. Stop points: Stop and ReturnAll
    /// at Teardown, Dispose with the presenter.
    /// </summary>
    public sealed class BoosterCasts : IDisposable
    {
        /// <summary>Instances warmed per cast kind; a third simultaneous cast of one kind is skipped.</summary>
        public const int k_instancesPerKind = 2;

        private const int k_kindCount = 3;
        private const int k_hammer = 0;
        private const int k_rocket = 1;
        private const int k_vacuum = 2;

        // One kind's warmed views: its pool, the views it created on Home and which are checked out.
        private sealed class CastPool
        {
            private readonly PrefabComponentPool<BoosterEffectView> m_pool;
            private readonly BoosterEffectView[] m_views = new BoosterEffectView[k_instancesPerKind];
            // Each view's root Transform, read once on Home: the first managed access to a component creates its C#
            // wrapper, an allocation, so a view's first Play during Playing must not be the first to read it (AC-16).
            private readonly Transform[] m_roots = new Transform[k_instancesPerKind];
            private readonly bool[] m_inUse = new bool[k_instancesPerKind];
            private bool m_prepared;

            // Owner: BoosterCasts. Stops: Dispose destroys the pooled views.
            public CastPool(BoosterEffectView prefab, Transform parent, string name)
            {
                m_pool = new PrefabComponentPool<BoosterEffectView>(prefab, parent, k_instancesPerKind,
                    k_instancesPerKind, name);
            }

            public int ActiveCount { get; private set; }

            public int PreparedCount => m_prepared ? m_views.Length : 0;

            public BoosterEffectView GetPrepared(int index) => m_views[index];

            // Rents every instance, then returns them, so no Get during Playing instantiates.
            public void Prepare()
            {
                if (m_prepared)
                {
                    return;
                }

                for (int index = 0; index < m_views.Length; index++)
                {
                    m_views[index] = m_pool.Get();
                    m_roots[index] = m_views[index].transform;
                }

                for (int index = 0; index < m_views.Length; index++)
                {
                    m_pool.Release(m_views[index]);
                }

                m_prepared = true;
            }

            // Onity maxSize caps retained objects, not growth, so the checked-out count is the hard bound.
            public bool TryTake(out BoosterEffectView view, out Transform root)
            {
                view = null;
                root = null;
                if (!m_prepared || ActiveCount >= m_views.Length)
                {
                    return false;
                }

                BoosterEffectView taken = m_pool.Get();
                for (int index = 0; index < m_views.Length; index++)
                {
                    if (ReferenceEquals(m_views[index], taken))
                    {
                        m_inUse[index] = true;
                        ActiveCount++;
                        view = taken;
                        root = m_roots[index];
                        return true;
                    }
                }

                // The pool returned a view it did not warm; give it back rather than track it.
                m_pool.Release(taken);
                return false;
            }

            public void Give(BoosterEffectView view)
            {
                for (int index = 0; index < m_views.Length; index++)
                {
                    if (m_inUse[index] && ReferenceEquals(m_views[index], view))
                    {
                        Return(index);
                        return;
                    }
                }
            }

            public float RemainingSeconds
            {
                get
                {
                    float longest = 0f;
                    for (int index = 0; index < m_views.Length; index++)
                    {
                        if (m_inUse[index] && m_views[index] != null)
                        {
                            longest = Math.Max(longest, m_views[index].RemainingSeconds);
                        }
                    }

                    return longest;
                }
            }

            // Polled from the view sync phase, so no completion callback outlives an attempt. A view stays checked out
            // until its motion and its particle show have both ended (DEC-152).
            public void Sync()
            {
                for (int index = 0; index < m_views.Length; index++)
                {
                    if (m_inUse[index] && (m_views[index] == null || !m_views[index].IsBusy))
                    {
                        Return(index);
                    }
                }
            }

            public void SetPaused(bool paused)
            {
                for (int index = 0; index < m_views.Length; index++)
                {
                    if (!m_inUse[index] || m_views[index] == null)
                    {
                        continue;
                    }

                    if (paused)
                    {
                        m_views[index].Suspend();
                    }
                    else
                    {
                        m_views[index].Resume();
                    }
                }
            }

            public void Stop()
            {
                for (int index = 0; index < m_views.Length; index++)
                {
                    if (m_inUse[index] && m_views[index] != null)
                    {
                        m_views[index].Stop();
                    }
                }
            }

            public void ReturnAll()
            {
                for (int index = 0; index < m_views.Length; index++)
                {
                    if (m_inUse[index])
                    {
                        Return(index);
                    }
                }
            }

            public void Dispose()
            {
                ReturnAll();
                m_pool.Dispose();
                m_prepared = false;
            }

            // A scene unload may destroy a view before its owner returns it; the slot is then only marked free.
            private void Return(int index)
            {
                m_inUse[index] = false;
                ActiveCount--;
                BoosterEffectView view = m_views[index];
                if (view != null)
                {
                    view.Stop();
                    m_pool.Release(view);
                }
            }
        }

        private readonly CastPool[] m_pools = new CastPool[k_kindCount];
        private bool m_disposed;

        /// <summary>
        /// Reads the Hammer, Rocket and Vacuum definitions' effect prefabs; a definition without one, or whose prefab
        /// lacks its view, gets no cast. Nothing is instantiated until Prepare.
        /// </summary>
        public BoosterCasts(BoosterCatalog catalog, Transform parent)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            m_pools[k_hammer] = CreatePool<HammerEffectView>(catalog, BoosterId.Hammer, parent, "Booster casts Hammer");
            m_pools[k_rocket] = CreatePool<RocketEffectView>(catalog, BoosterId.Rocket, parent, "Booster casts Rocket");
            m_pools[k_vacuum] = CreatePool<VacuumEffectView>(catalog, BoosterId.Vacuum, parent, "Booster casts Vacuum");
        }

        /// <summary>Cast views warmed so far across the three kinds; Restart and later levels keep them.</summary>
        public int PreparedCount
        {
            get
            {
                int count = 0;
                for (int kind = 0; kind < m_pools.Length; kind++) count += m_pools[kind]?.PreparedCount ?? 0;
                return count;
            }
        }

        /// <summary>Cast views playing or not yet returned by Sync.</summary>
        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int kind = 0; kind < m_pools.Length; kind++) count += m_pools[kind]?.ActiveCount ?? 0;
                return count;
            }
        }

        /// <summary>The longest time a playing cast still needs; the presenter adds it to the Won Finish (DEC-93).</summary>
        public float RemainingSeconds
        {
            get
            {
                float longest = 0f;
                for (int kind = 0; kind < m_pools.Length; kind++)
                {
                    if (m_pools[kind] != null) longest = Math.Max(longest, m_pools[kind].RemainingSeconds);
                }

                return longest;
            }
        }

        /// <summary>A warmed view, Hammer first, then Rocket and Vacuum, two each; null for a kind without a prefab.</summary>
        public BoosterEffectView GetPrepared(int index)
        {
            CastPool pool = m_pools[index / k_instancesPerKind];
            return pool != null && pool.PreparedCount > 0 ? pool.GetPrepared(index % k_instancesPerKind) : null;
        }

        /// <summary>Warms every cast view on Home, once; later calls create nothing.</summary>
        public void Prepare()
        {
            ThrowIfDisposed();
            for (int kind = 0; kind < m_pools.Length; kind++) m_pools[kind]?.Prepare();
        }

        /// <summary>
        /// Plays a Hammer strike with its root at position, turned by rotation, and its hit burst in hitColor; the
        /// mallet flies in from launch (world space) during its wind-up. contactSeconds is when its face touches the
        /// root (HammerEffectView.k_strikeFraction). False, with nothing shown, when no instance is free.
        /// </summary>
        public bool TryPlayHammer(Vector3 position, Quaternion rotation, Color hitColor, Vector3 launch,
            out float contactSeconds)
        {
            contactSeconds = 0f;
            if (!TryTake(k_hammer, position, rotation, out BoosterEffectView view)) return false;
            if (!((HammerEffectView)view).Play(hitColor, launch))
            {
                m_pools[k_hammer].Give(view);
                return false;
            }

            contactSeconds = view.Duration * HammerEffectView.k_strikeFraction;
            return true;
        }

        /// <summary>
        /// Launches a Rocket from position toward destination, both in world space, shattering there in cellColor;
        /// false when none is free.
        /// </summary>
        public bool TryPlayRocket(Vector3 position, Quaternion rotation, Vector3 destination, Color cellColor)
        {
            return TryPlayRocket(position, rotation, destination, cellColor, out _);
        }

        /// <summary>
        /// As TryPlayRocket; contactSeconds is when its nose reaches destination (RocketEffectView.k_noseFraction).
        /// </summary>
        public bool TryPlayRocket(Vector3 position, Quaternion rotation, Vector3 destination, Color cellColor,
            out float contactSeconds)
        {
            contactSeconds = 0f;
            if (!TryTake(k_rocket, position, rotation, out BoosterEffectView view)) return false;
            if (!((RocketEffectView)view).Play(destination, cellColor))
            {
                m_pools[k_rocket].Give(view);
                return false;
            }

            contactSeconds = view.Duration * RocketEffectView.k_noseFraction;
            return true;
        }

        /// <summary>
        /// Plays the Vacuum turned by rotation (camera-facing) with its drawn canister standing on position: the root
        /// rises by the view's BaseDepth along the rotation's up axis, which keeps its depth, so on screen the canister's
        /// bottom is exactly where position projects. intake is where pieces go in world space, and intakeStart and
        /// intakeSeconds bound the front-facing intake window of the motion. False when no instance is free.
        /// </summary>
        public bool TryPlayVacuum(Vector3 position, Quaternion rotation, out Vector3 intake, out float intakeStart,
            out float intakeSeconds)
        {
            intake = default;
            intakeStart = 0f;
            intakeSeconds = 0f;
            if (!TryTake(k_vacuum, position, rotation, out BoosterEffectView view, out Transform root)) return false;
            VacuumEffectView vacuum = (VacuumEffectView)view;
            root.position = position + rotation * (Vector3.up * vacuum.BaseDepth);
            if (!vacuum.Play())
            {
                m_pools[k_vacuum].Give(view);
                return false;
            }

            intake = vacuum.ReadyIntakePosition;
            intakeStart = view.Duration * VacuumEffectView.k_intakeStartFraction;
            intakeSeconds = view.Duration * (VacuumEffectView.k_intakeEndFraction - VacuumEffectView.k_intakeStartFraction);
            return true;
        }

        /// <summary>The view sync phase: returns every cast whose motion ended.</summary>
        public void Sync()
        {
            for (int kind = 0; kind < m_pools.Length; kind++) m_pools[kind]?.Sync();
        }

        /// <summary>Suspend step 3 and Resume: holds or continues every playing cast.</summary>
        public void SetPaused(bool paused)
        {
            for (int kind = 0; kind < m_pools.Length; kind++) m_pools[kind]?.SetPaused(paused);
        }

        /// <summary>Teardown step 3: stops every playing cast and restores its rest pose; ReturnAll then returns them.</summary>
        public void Stop()
        {
            for (int kind = 0; kind < m_pools.Length; kind++) m_pools[kind]?.Stop();
        }

        /// <summary>Teardown step 6: returns every checked-out cast to its pool.</summary>
        public void ReturnAll()
        {
            for (int kind = 0; kind < m_pools.Length; kind++) m_pools[kind]?.ReturnAll();
        }

        /// <summary>Returns every cast and destroys the pooled views; a scene unload may have destroyed them already.</summary>
        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;
            for (int kind = 0; kind < m_pools.Length; kind++) m_pools[kind]?.Dispose();
        }

        private bool TryTake(int kind, Vector3 position, Quaternion rotation, out BoosterEffectView view)
        {
            return TryTake(kind, position, rotation, out view, out _);
        }

        private bool TryTake(int kind, Vector3 position, Quaternion rotation, out BoosterEffectView view, out Transform root)
        {
            view = null;
            root = null;
            if (m_disposed || m_pools[kind] == null || !m_pools[kind].TryTake(out view, out root)) return false;
            root.SetPositionAndRotation(position, rotation);
            root.localScale = Vector3.one;
            return true;
        }

        private static CastPool CreatePool<TView>(BoosterCatalog catalog, BoosterId id, Transform parent, string name)
            where TView : BoosterEffectView
        {
            if (!catalog.TryGet(id, out BoosterDefinition definition) || definition.EffectPrefab == null)
            {
                return null;
            }

            TView prefab = definition.EffectPrefab.GetComponent<TView>();
            return prefab != null ? new CastPool(prefab, parent, name) : null;
        }

        private void ThrowIfDisposed()
        {
            if (m_disposed) throw new ObjectDisposedException(nameof(BoosterCasts));
        }
    }
}
